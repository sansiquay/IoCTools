namespace IoCTools.Tools.Cli;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using CommandLine;

using Generator.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class EvidencePrinter
{
    /// <summary>
    /// Builds fixture migration evidence from a test project.
    /// Requires --test-fixtures flag and --production-project to be set for full classification.
    /// </summary>
    public static async Task<EvidenceFixtureEvidence> BuildFixtureEvidenceAsync(
        ProjectContext context,
        EvidenceCommandOptions options,
        CancellationToken token)
    {
        var classifications = new List<EvidenceFixtureClassification>();

        // Load production project compilation if specified
        CSharpCompilation? productionCompilation = null;
        if (!string.IsNullOrEmpty(options.ProductionProjectPath))
        {
            try
            {
                var prodOptions = new CommonOptions(
                    options.ProductionProjectPath,
                    options.Common.Configuration,
                    options.Common.Framework,
                    false,
                    false);
                var prodContext = await ProjectContext.CreateAsync(prodOptions, token);
                productionCompilation = prodContext.Compilation;
                await prodContext.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Could not load production project: {ex.Message}");
            }
        }

        // Collect test classes in the current (test) project
        var testClasses = new List<INamedTypeSymbol>();
        var seenTestClasses = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);
            var typeDecls = tree.GetRoot(token).DescendantNodes().OfType<TypeDeclarationSyntax>();
            foreach (var typeDecl in typeDecls)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var assemblyName = symbol.ContainingAssembly?.Name ?? string.Empty;
                if (!assemblyName.EndsWith(".Tests", StringComparison.Ordinal) &&
                    !assemblyName.Contains(".Test"))
                    continue;

                // Check for test attributes (Fact, Test, TestMethod)
                var isTestClass = symbol.GetMembers().Any(m =>
                    m.GetAttributes().Any(a =>
                        a.AttributeClass?.Name.Contains("Fact") == true ||
                        a.AttributeClass?.Name.Contains("Test") == true));

                if (isTestClass && seenTestClasses.Add(symbol))
                    testClasses.Add(symbol);
            }
        }

        int safeCount = 0, partialCount = 0, harnessCount = 0, unknownCount = 0;

        foreach (var testClass in testClasses)
        {
            var (classification, serviceType, reason, matchedDeps, manualMocks) =
                ClassifyTestClass(testClass, context.Compilation, productionCompilation);

            var line = testClass.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line + 1;
            var filePath = testClass.Locations.FirstOrDefault()?.SourceTree?.FilePath;

            classifications.Add(new EvidenceFixtureClassification(
                testClass.ToDisplayString(),
                classification,
                serviceType,
                reason,
                matchedDeps,
                manualMocks,
                line,
                filePath));

            switch (classification)
            {
                case FixtureClassificationKind.SafeMigration: safeCount++; break;
                case FixtureClassificationKind.PartialMigration: partialCount++; break;
                case FixtureClassificationKind.SemanticHarness: harnessCount++; break;
                case FixtureClassificationKind.UnknownReview: unknownCount++; break;
            }
        }

        return new EvidenceFixtureEvidence(
            classifications,
            testClasses.Count,
            safeCount,
            partialCount,
            harnessCount,
            unknownCount);
    }

    private static (string classification, string? serviceType, string? reason, IReadOnlyList<string> matchedDeps, IReadOnlyList<string> manualMocks)
        ClassifyTestClass(
            INamedTypeSymbol testClass,
            CSharpCompilation testCompilation,
            CSharpCompilation? productionCompilation)
    {
        var className = testClass.ToDisplayString();

        // Check for semantic harness signals
        var harnessSignals = new[] { "WebApplicationFactory", "Harness", "InMemory", "Lease", "Observability" };
        foreach (var signal in harnessSignals)
        {
            if (className.Contains(signal, StringComparison.Ordinal))
                return (FixtureClassificationKind.SemanticHarness, null,
                    $"Test class contains semantic harness signal '{signal}'",
                    Array.Empty<string>(), Array.Empty<string>());

            if (testClass.BaseType != null && testClass.BaseType.Name.Contains(signal, StringComparison.Ordinal))
                return (FixtureClassificationKind.SemanticHarness, null,
                    $"Base type '{testClass.BaseType.Name}' is a semantic harness",
                    Array.Empty<string>(), Array.Empty<string>());
        }

        // Check for lifecycle methods
        var hasLifecycleMethods = testClass.GetMembers().Any(m =>
            m.Name is "InitializeAsync" or "DisposeAsync" or "Dispose" or "Cleanup" or "Setup");
        if (hasLifecycleMethods)
        {
            var hasComplexSetup = testClass.GetMembers().OfType<IMethodSymbol>()
                .Any(m => m.Name.Contains("Setup", StringComparison.Ordinal) ||
                          m.Name.Contains("Configure", StringComparison.Ordinal));
            if (hasComplexSetup)
                return (FixtureClassificationKind.SemanticHarness, null,
                    "Test class has lifecycle methods and complex setup",
                    Array.Empty<string>(), Array.Empty<string>());
        }

        // Get Mock<T> fields (must be from Moq namespace)
        var mockFields = testClass.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => IsMoqMock(f.Type) && !IsGeneratedMember(f))
            .ToList();

        if (mockFields.Count == 0)
        {
            return (FixtureClassificationKind.UnknownReview, null,
                "No manual Mock<T> fields found; class may not use mocks",
                Array.Empty<string>(), Array.Empty<string>());
        }

        var mockTypes = new Dictionary<ITypeSymbol, byte>(SymbolEqualityComparer.Default);
        foreach (var field in mockFields)
        {
            var t = ((INamedTypeSymbol)field.Type).TypeArguments[0];
            mockTypes[t] = 0;
        }
        var mockTypeNames = mockTypes.Keys
            .Select(GetTypeKey)
            .ToHashSet(StringComparer.Ordinal);

        bool HasMockFor(ITypeSymbol type) =>
            mockTypes.ContainsKey(type) ||
            mockTypeNames.Contains(GetTypeKey(type)) ||
            IsFixtureProvidedDependency(type);

        (string classification, string? serviceType, string? reason, IReadOnlyList<string> matchedDeps, IReadOnlyList<string> manualMocks)
            ClassifyAgainstService(INamedTypeSymbol service)
        {
            var constructor = service.Constructors
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault(c => !c.IsStatic);

            if (constructor == null)
            {
                return (FixtureClassificationKind.PartialMigration, service.ToDisplayString(),
                    "Service has resolvable mocks but no constructor parameters",
                    mockTypes.Keys.Select(t => t.Name).ToList(),
                    mockFields.Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}").ToList());
            }

            var constructorDepNames = constructor.Parameters
                .Select(p => GetTypeKey(p.Type))
                .ToHashSet(StringComparer.Ordinal);
            var hasExtraManualMocks = mockTypes.Keys
                .Any(mockType => !constructorDepNames.Contains(GetTypeKey(mockType)));
            var allDepsMatched = constructor.Parameters
                .All(p => HasMockFor(p.Type));

            if (allDepsMatched && !hasExtraManualMocks)
            {
                return (FixtureClassificationKind.SafeMigration, service.ToDisplayString(),
                    $"All {constructor.Parameters.Length} constructor dependencies have matching mocks",
                    mockTypes.Keys.Select(t => t.Name).ToList(),
                    Array.Empty<string>());
            }

            var matched = constructor.Parameters
                .Where(p => HasMockFor(p.Type))
                .Select(p => p.Type.Name)
                .ToList();
            var unmatched = constructor.Parameters
                .Where(p => !HasMockFor(p.Type))
                .Select(p => p.Type.Name)
                .ToList();

            var manualMockEvidence = mockFields
                .Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}")
                .ToList();

            var extraManualMockEvidence = mockFields
                .Where(f => !constructorDepNames.Contains(GetTypeKey(((INamedTypeSymbol)f.Type).TypeArguments[0])))
                .Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}")
                .ToList();

            var reason = allDepsMatched
                ? $"All {constructor.Parameters.Length} constructor dependencies have matching mocks, but {extraManualMockEvidence.Count} extra manual mock(s) remain: {string.Join(", ", extraManualMockEvidence)}"
                : $"Service has {constructor.Parameters.Length} deps, {matched.Count} matched, {unmatched.Count} unmatched: {string.Join(", ", unmatched)}";

            return (FixtureClassificationKind.PartialMigration, service.ToDisplayString(),
                reason,
                matched,
                allDepsMatched ? extraManualMockEvidence : manualMockEvidence);
        }

        (string classification, string? serviceType, string? reason, IReadOnlyList<string> matchedDeps, IReadOnlyList<string> manualMocks)
            ClassifyCoveredService(INamedTypeSymbol service)
        {
            var constructor = service.Constructors
                .OrderByDescending(c => c.Parameters.Length)
                .FirstOrDefault(c => !c.IsStatic);
            var constructorDepNames = constructor?.Parameters
                .Select(p => GetTypeKey(p.Type))
                .ToHashSet(StringComparer.Ordinal) ?? [];

            var remainingManualMocks = mockFields
                .Select(f => new
                {
                    Field = f,
                    MockType = ((INamedTypeSymbol)f.Type).TypeArguments[0]
                })
                .Where(x => constructorDepNames.Contains(GetTypeKey(x.MockType)) ||
                            !IsFixtureProvidedDependency(x.MockType))
                .Select(x => $"{x.MockType.Name} {x.Field.Name}")
                .ToList();

            if (remainingManualMocks.Count == 0)
            {
                return (FixtureClassificationKind.UnknownReview, service.ToDisplayString(),
                    "Class already uses [Cover<T>] and has no remaining manual fixture mocks",
                    Array.Empty<string>(), Array.Empty<string>());
            }

            return (FixtureClassificationKind.PartialMigration, service.ToDisplayString(),
                $"Class already uses [Cover<T>] but {remainingManualMocks.Count} manual mock(s) remain: {string.Join(", ", remainingManualMocks)}",
                constructor?.Parameters.Select(p => p.Type.Name).ToList() ?? [],
                remainingManualMocks);
        }

        var coveredService = testClass.GetAttributes()
            .Select(a => a.AttributeClass as INamedTypeSymbol)
            .Where(a => a is { Name: "CoverAttribute", TypeArguments.Length: 1 })
            .Select(a => a!.TypeArguments[0] as INamedTypeSymbol)
            .FirstOrDefault(s => s != null);

        if (coveredService != null)
            return ClassifyCoveredService(coveredService);

        var constructedServices = FindManuallyConstructedServices(testClass, testCompilation, productionCompilation);
        if (constructedServices.Count > 0)
        {
            var subjectName = TrimTestSuffix(testClass.Name);
            var preferred = constructedServices.FirstOrDefault(service =>
                string.Equals(subjectName, service.Name, StringComparison.Ordinal))
                ?? constructedServices.FirstOrDefault(service =>
                    testClass.Name.StartsWith(service.Name, StringComparison.Ordinal) ||
                    testClass.Name.Contains(service.Name, StringComparison.Ordinal))
                ?? constructedServices[0];

            return ClassifyAgainstService(preferred);
        }

        // Find matching services in test compilation or production compilation
        var matchingServices = FindMatchingServices(
            testCompilation, productionCompilation, mockTypes, mockTypeNames);

        if (matchingServices.Count == 0)
        {
            return (FixtureClassificationKind.UnknownReview, null,
                $"No matching service found for {mockFields.Count} mock(s)",
                Array.Empty<string>(),
                mockFields.Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}").ToList());
        }

        var matchingSubject = matchingServices.FirstOrDefault(service =>
            string.Equals(TrimTestSuffix(testClass.Name), service.Name, StringComparison.Ordinal));
        if (matchingSubject != null)
            return ClassifyAgainstService(matchingSubject);

        if (matchingServices.Count == 1)
        {
            var service = matchingServices[0];
            return ClassifyAgainstService(service);
        }

        if (matchingServices.Count > 10)
            return (FixtureClassificationKind.UnknownReview, null,
                $"Found {matchingServices.Count} potential service matches; mock overlap is too broad for automatic classification",
                mockTypes.Keys.Select(t => t.Name).ToList(),
                mockFields.Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}").ToList());

        return (FixtureClassificationKind.PartialMigration, matchingServices[0].ToDisplayString(),
            $"Found {matchingServices.Count} potential service matches; verify manually",
            mockTypes.Keys.Select(t => t.Name).ToList(),
            mockFields.Select(f => $"{((INamedTypeSymbol)f.Type).TypeArguments[0].Name} {f.Name}").ToList());
    }

    private static List<INamedTypeSymbol> FindManuallyConstructedServices(
        INamedTypeSymbol testClass,
        CSharpCompilation testCompilation,
        CSharpCompilation? productionCompilation)
    {
        var services = new List<INamedTypeSymbol>();
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var syntaxReference in testClass.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeDecl)
                continue;

            var semanticModel = testCompilation.GetSemanticModel(typeDecl.SyntaxTree);
            foreach (var creation in typeDecl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeInfo = semanticModel.GetTypeInfo(creation);
                if (typeInfo.Type is not INamedTypeSymbol createdType)
                    continue;

                if (LooksLikeTestClass(createdType) ||
                    IsMoqMock(createdType) ||
                    !LooksLikeFixtureService(createdType) ||
                    createdType.TypeKind != TypeKind.Class ||
                    createdType.ContainingNamespace?.ToDisplayString().StartsWith("System", StringComparison.Ordinal) == true)
                    continue;

                if (productionCompilation != null &&
                    !SymbolEqualityComparer.Default.Equals(createdType.ContainingAssembly, productionCompilation.Assembly))
                    continue;

                if (createdType.Constructors.Any(c => !c.IsStatic && c.Parameters.Length > 0) &&
                    seen.Add(createdType))
                    services.Add(createdType);
            }
        }

        return services;
    }

    private static List<INamedTypeSymbol> FindMatchingServices(
        CSharpCompilation testCompilation,
        CSharpCompilation? productionCompilation,
        Dictionary<ITypeSymbol, byte> mockTypes,
        HashSet<string> mockTypeNames)
    {
        var matches = new List<INamedTypeSymbol>();

        var compilations = productionCompilation != null
            ? new[] { productionCompilation }
            : new[] { testCompilation };

        foreach (var comp in compilations.Where(c => c != null))
        {
            foreach (var tree in comp!.SyntaxTrees)
            {
                var semanticModel = comp.GetSemanticModel(tree);
                var typeDecls = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var typeDecl in typeDecls)
                {
                    var serviceSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (serviceSymbol == null) continue;
                    if (LooksLikeTestClass(serviceSymbol)) continue;
                    if (!LooksLikeFixtureService(serviceSymbol)) continue;

                    var constructor = serviceSymbol.Constructors
                        .OrderByDescending(c => c.Parameters.Length)
                        .FirstOrDefault(c => !c.IsStatic);

                    if (constructor == null) continue;

                    var serviceDepsSet = constructor.Parameters.Select(p => p.Type).ToHashSet(SymbolEqualityComparer.Default);
                    var serviceDepNames = constructor.Parameters
                        .Select(p => GetTypeKey(p.Type))
                        .ToHashSet(StringComparer.Ordinal);
                    var overlaps = mockTypes.Keys.Any(k => serviceDepsSet.Contains(k)) ||
                                   mockTypeNames.Any(serviceDepNames.Contains);
                    if (overlaps)
                    {
                        if (mockTypes.Keys.All(m => serviceDepsSet.Contains(m)) ||
                            mockTypeNames.All(serviceDepNames.Contains))
                            matches.Insert(0, serviceSymbol);
                        else
                            matches.Add(serviceSymbol);
                    }
                }
            }

            if (matches.Count > 0)
                break;
        }

        return matches;
    }

    private static string TrimTestSuffix(string name)
    {
        if (name.EndsWith("Tests", StringComparison.Ordinal))
            return name[..^"Tests".Length];
        if (name.EndsWith("Test", StringComparison.Ordinal))
            return name[..^"Test".Length];

        return name;
    }

    private static bool LooksLikeFixtureService(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class)
            return false;

        if (symbol.BaseType?.ToDisplayString() == "System.Exception" ||
            InheritsFrom(symbol, "System.Exception"))
            return false;

        var name = symbol.Name;
        var excludedSuffixes = new[]
        {
            "Command", "Query", "Event", "Request", "Response", "Dto", "Contract",
            "Options", "Snapshot", "Exception", "Attribute"
        };
        if (excludedSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)))
            return false;

        var serviceSuffixes = new[]
        {
            "Service", "Handler", "Factory", "Processor", "Manager", "Registry",
            "Tracker", "Gate", "Scheduler", "Planner", "Dispatcher", "Probe",
            "Validator", "Publisher", "Projector", "Hydrator", "Initializer",
            "Reader", "Writer", "Client", "Adapter", "Resolver", "Mapper",
            "Builder", "Monitor", "Checker", "Coordinator", "Orchestrator",
            "Collector", "Provider", "Sink", "Policy", "Cache", "Index", "Store"
        };
        if (serviceSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)))
            return true;

        return symbol.GetAttributes().Any(a =>
        {
            var attributeName = a.AttributeClass?.Name;
            return attributeName is "ScopedAttribute" or "SingletonAttribute" or "TransientAttribute" ||
                   attributeName?.StartsWith("DependsOn", StringComparison.Ordinal) == true;
        });
    }

    private static bool InheritsFrom(INamedTypeSymbol symbol, string fullyQualifiedBaseType)
    {
        for (var current = symbol.BaseType; current != null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), fullyQualifiedBaseType, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string GetTypeKey(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static bool IsFixtureProvidedDependency(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        if (typeName.StartsWith("Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal) ||
            type is INamedTypeSymbol { Name: "ILogger", IsGenericType: true })
            return true;

        if (typeName == "Microsoft.Extensions.Configuration.IConfiguration" ||
            typeName == "System.TimeProvider")
            return true;

        if (type.Name == "IClock")
            return true;

        if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions<", StringComparison.Ordinal) ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<", StringComparison.Ordinal) ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static bool IsGeneratedMember(ISymbol symbol)
    {
        var sawSourceLocation = false;
        foreach (var location in symbol.Locations)
        {
            if (!location.IsInSource)
                continue;

            sawSourceLocation = true;
            var filePath = location.SourceTree?.FilePath;
            if (string.IsNullOrWhiteSpace(filePath))
                return true;

            var normalized = filePath.Replace('\\', '/');
            if (normalized.Contains("/obj/", StringComparison.Ordinal) ||
                normalized.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return !sawSourceLocation;
    }

    private static bool LooksLikeTestClass(INamedTypeSymbol symbol)
    {
        if (symbol.Name.EndsWith("Tests", StringComparison.Ordinal) ||
            symbol.Name.EndsWith("Test", StringComparison.Ordinal))
            return true;

        if (symbol.GetAttributes().Any(a =>
                a.AttributeClass is { Name: "CoverAttribute" } &&
                a.AttributeClass.ToDisplayString().Contains("IoCTools.Testing", StringComparison.Ordinal)))
            return true;

        return symbol.GetMembers().Any(m =>
            m.GetAttributes().Any(a =>
                a.AttributeClass?.Name.Contains("Fact", StringComparison.Ordinal) == true ||
                a.AttributeClass?.Name.Contains("Test", StringComparison.Ordinal) == true));
    }

    public static async Task<EvidenceBundle> BuildAsync(ProjectContext context,
        EvidenceCommandOptions options,
        CancellationToken token)
    {
        var started = Stopwatch.StartNew();
        var inspector = new ServiceFieldInspector(context.Project);
        var reports = await inspector.GetFieldReportsAsync(null, Array.Empty<string>(), token);
        var target = options.TypeName == null
            ? null
            : reports.FirstOrDefault(r => string.Equals(r.TypeName, options.TypeName, StringComparison.Ordinal));

        var artifactWriter = await GeneratorArtifactWriter.CreateAsync(context, options.OutputDirectory, token);
        var registrations = BuildRegistrations(context, artifactWriter);
        var diagnostics = await DiagnosticRunner.RunAsync(context, token);
        var validators = ValidatorInspector.DiscoverValidators(context.Compilation);
        var configuration = BuildConfiguration(reports, options.SettingsPath);
        var migrationHints = BuildMigrationHints(context, options.TypeName, token);
        var generatedArtifacts = BuildGeneratedArtifacts(artifactWriter);
        started.Stop();

        var serviceRegistrations = registrations
            .Where(r => string.Equals(r.kind, nameof(RegistrationKind.Service), StringComparison.Ordinal))
            .ToArray();
        var configurationRegistrations = registrations
            .Where(r => string.Equals(r.kind, nameof(RegistrationKind.Configuration), StringComparison.Ordinal))
            .ToArray();

        var explicitDeps = target == null
            ? Array.Empty<GeneratedFieldInfo>()
            : target.DependencyFields.Where(d => !IsAutoDep(d.Attribution)).ToArray();
        var autoDepFields = target == null
            ? Array.Empty<GeneratedFieldInfo>()
            : target.DependencyFields.Where(d => IsAutoDep(d.Attribution)).ToArray();

        var hideAuto = options.AutoDepsFlags.HideAutoDeps;
        var onlyAuto = options.AutoDepsFlags.OnlyAutoDeps;

        var typeEvidence = target == null
            ? null
            : new EvidenceTypeEvidence(
                target.TypeName,
                target.FilePath,
                (onlyAuto ? Array.Empty<GeneratedFieldInfo>() : explicitDeps)
                    .Select(d => new EvidenceDependency(d.FieldName, d.TypeName, d.Source, d.IsExternal))
                    .ToArray(),
                (onlyAuto ? Array.Empty<GeneratedFieldInfo>() : target.ConfigurationFields.ToArray())
                    .Select(c => new EvidenceConfigurationBinding(
                        c.FieldName,
                        c.TypeName,
                        string.IsNullOrWhiteSpace(c.ConfigurationKey) ? "<inferred>" : c.ConfigurationKey!,
                        c.Required == true,
                        c.SupportsReloading == true))
                    .ToArray(),
                FilterRegistrations(registrations, target.TypeName),
                (hideAuto ? Array.Empty<GeneratedFieldInfo>() : autoDepFields)
                    .Select(d => new EvidenceAutoDep(
                        d.TypeName,
                        d.Attribution!.Value.ToTag(),
                        SuppressHint(d.TypeName, d.Attribution.Value)))
                    .ToArray());

        // Build fixture migration evidence when --test-fixtures is specified
        EvidenceFixtureEvidence? fixtureEvidence = null;
        if (options.TestFixtures)
        {
            fixtureEvidence = await BuildFixtureEvidenceAsync(context, options, token);
        }

        return new EvidenceBundle(
            new EvidenceProject(
                context.Project.FilePath ?? options.Common.ProjectPath,
                context.Project.Name,
                options.Common.Configuration,
                options.Common.Framework),
            new EvidenceServices(
                serviceRegistrations.Length,
                configurationRegistrations.Length,
                registrations),
            typeEvidence,
            new EvidenceDiagnostics(
                diagnostics.Count,
                diagnostics.Count(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Count(d => string.Equals(d.Severity, "Warning", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Count(d => string.Equals(d.Severity, "Info", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Any(d => string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
                diagnostics.Select(d => new EvidenceDiagnostic(
                        d.Id,
                        d.Severity,
                        d.Message,
                        $"{d.FilePath}:{d.Line}:{d.Column}"))
                    .ToArray()),
            configuration,
            validators.Count == 0
                ? null
                : new EvidenceValidators(validators.Count, validators.Select(v => v.FullName).ToArray()),
            new EvidenceArtifacts(
                artifactWriter.OutputRoot,
                new EvidenceProfile(
                    started.Elapsed.TotalMilliseconds,
                    serviceRegistrations.Length,
                    configurationRegistrations.Length),
                generatedArtifacts,
                options.BaselineDirectory != null
                    ? BuildCompare(options.BaselineDirectory, artifactWriter.OutputRoot, generatedArtifacts)
                    : null),
            migrationHints,
            fixtureEvidence);
    }

    public static void Write(EvidenceBundle bundle,
        OutputContext output)
    {
        if (output.IsJson)
        {
            output.WriteJson(bundle);
            return;
        }

        output.WriteLine("Project");
        output.WriteLine($"  Path: {bundle.project.path}");
        output.WriteLine($"  Name: {bundle.project.name}");
        output.WriteLine($"  Configuration: {bundle.project.configuration}");
        if (!string.IsNullOrWhiteSpace(bundle.project.framework))
            output.WriteLine($"  Framework: {bundle.project.framework}");

        output.WriteLine(string.Empty);
        output.WriteLine("Services");
        output.WriteLine($"  Service registrations: {bundle.services.serviceCount}");
        output.WriteLine($"  Configuration bindings: {bundle.services.configurationCount}");
        foreach (var registration in bundle.services.registrations.Take(5))
        {
            var service = registration.serviceType ?? registration.implementationType ?? "(unknown)";
            var implementation = registration.implementationType ?? registration.serviceType ?? "(unknown)";
            var lifetime = registration.lifetime ?? registration.kind;
            output.WriteLine($"  - [{lifetime}] {service} => {implementation}");
        }

        if (bundle.typeEvidence != null)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Type Evidence");
            output.WriteLine($"  Service: {bundle.typeEvidence.typeName}");
            output.WriteLine($"  File: {bundle.typeEvidence.filePath}");
            output.WriteLine("  Dependencies:");
            if (bundle.typeEvidence.dependencies.Count == 0)
                output.WriteLine("    (none)");
            else
                foreach (var dependency in bundle.typeEvidence.dependencies)
                    output.WriteLine($"    - {dependency.typeName} => {dependency.fieldName} [{dependency.source}]");

            output.WriteLine("  Configuration:");
            if (bundle.typeEvidence.configuration.Count == 0)
                output.WriteLine("    (none)");
            else
                foreach (var config in bundle.typeEvidence.configuration)
                    output.WriteLine(
                        $"    - {config.typeName} => {config.fieldName} (key: {config.configurationKey}, {(config.required ? "required" : "optional")}{(config.supportsReloading ? ", reload" : string.Empty)})");

            if (bundle.typeEvidence.autoDeps.Count > 0)
            {
                output.WriteLine("  Auto-dependencies:");
                output.WriteLine("    Type | Source Tag | Suppress With");
                foreach (var autoDep in bundle.typeEvidence.autoDeps)
                    output.WriteLine($"    {autoDep.typeName} | {autoDep.source} | {autoDep.suppress}");
            }
        }

        output.WriteLine(string.Empty);
        output.WriteLine("Diagnostics");
        output.WriteLine(
            $"  Total: {bundle.diagnostics.total} (Errors: {bundle.diagnostics.errorCount}, Warnings: {bundle.diagnostics.warningCount}, Info: {bundle.diagnostics.infoCount})");

        output.WriteLine(string.Empty);
        output.WriteLine("Configuration");
        output.WriteLine($"  Required bindings: {bundle.configuration.requiredBindings}");
        output.WriteLine($"  Settings keys discovered: {bundle.configuration.settingsKeysDiscovered}");
        if (bundle.configuration.missingKeys.Count == 0)
        {
            output.WriteLine("  Missing keys: none");
        }
        else
        {
            output.WriteLine("  Missing keys:");
            foreach (var key in bundle.configuration.missingKeys)
                output.WriteLine($"    - {key}");
        }

        if (bundle.validators != null)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Validators");
            output.WriteLine($"  Count: {bundle.validators.count}");
        }

        output.WriteLine(string.Empty);
        output.WriteLine("Artifacts");
        output.WriteLine($"  Output directory: {bundle.artifacts.outputDirectory}");
        output.WriteLine($"  Generated artifacts: {bundle.artifacts.generatedArtifacts.Count}");
        foreach (var artifact in bundle.artifacts.generatedArtifacts.Take(5))
        {
            var fingerprintPrefix = artifact.fingerprint[..Math.Min(12, artifact.fingerprint.Length)];
            output.WriteLine($"  - {artifact.fileName} [{fingerprintPrefix}]");
        }

        if (bundle.artifacts.compare != null)
        {
            var changed = bundle.artifacts.compare.deltas.Count(delta =>
                !string.Equals(delta.status, "unchanged", StringComparison.OrdinalIgnoreCase));
            output.WriteLine($"  Compare baseline: {bundle.artifacts.compare.baselineDirectory}");
            output.WriteLine($"  Compare deltas: {changed} changed, added, or removed artifacts");
        }

        if (bundle.migrationHints.Count > 0)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Migration Hints");
            foreach (var hint in bundle.migrationHints)
                output.WriteLine($"  - {hint.message}");
        }

        if (bundle.fixtureEvidence != null)
        {
            output.WriteLine(string.Empty);
            output.WriteLine("Fixture Migration Evidence");
            output.WriteLine($"  Test classes analyzed: {bundle.fixtureEvidence.TotalTestClasses}");
            output.WriteLine($"  Safe migration: {bundle.fixtureEvidence.SafeCount}");
            output.WriteLine($"  Partial migration: {bundle.fixtureEvidence.PartialCount}");
            output.WriteLine($"  Semantic harness: {bundle.fixtureEvidence.SemanticHarnessCount}");
            output.WriteLine($"  Unknown/review: {bundle.fixtureEvidence.UnknownCount}");
            output.WriteLine(string.Empty);

            foreach (var cls in bundle.fixtureEvidence.Classifications.Take(10))
            {
                var badge = cls.Classification switch
                {
                    "safe-migration" => "[SAFE]",
                    "partial-migration" => "[PARTIAL]",
                    "semantic-harness" => "[HARNESS]",
                    _ => "[REVIEW]"
                };
                output.WriteLine($"  {badge} {cls.TestClass}");
                if (cls.ServiceType != null)
                    output.WriteLine($"       Service: {cls.ServiceType}");
                if (cls.Reason != null)
                    output.WriteLine($"       Reason: {cls.Reason}");
                if (cls.MatchedDependencies.Count > 0)
                    output.WriteLine($"       Matched deps: {string.Join(", ", cls.MatchedDependencies)}");
                if (cls.ManualMocks.Count > 0)
                    output.WriteLine($"       Manual mocks: {string.Join(", ", cls.ManualMocks)}");
            }

            if (bundle.fixtureEvidence.Classifications.Count > 10)
                output.WriteLine($"  ... and {bundle.fixtureEvidence.Classifications.Count - 10} more");
        }
    }

    private static bool IsAutoDep(AutoDepAttribution? attr) =>
        attr is { } a && a.Kind != AutoDepSourceKind.Explicit;

    private static bool IsMoqMock(ITypeSymbol type) =>
        type is INamedTypeSymbol
        {
            MetadataName: "Mock`1",
            TypeArguments.Length: 1
        } mockType &&
        mockType.ContainingNamespace.ToDisplayString() == "Moq";

    private static string SuppressHint(string depTypeName, AutoDepAttribution attribution) => attribution.Kind switch
    {
        AutoDepSourceKind.AutoBuiltinILogger => "[NoAutoDepOpen(typeof(ILogger<>))]",
        AutoDepSourceKind.AutoOpenUniversal => "[NoAutoDepOpen(typeof(<OpenShape>))]",
        AutoDepSourceKind.AutoUniversal => $"[NoAutoDep<{depTypeName}>]",
        AutoDepSourceKind.AutoTransitive => $"[NoAutoDep<{depTypeName}>]",
        AutoDepSourceKind.AutoProfile => $"[NoAutoDep<{depTypeName}>] or remove from profile",
        _ => string.Empty
    };

    private static IReadOnlyList<EvidenceRegistration> BuildRegistrations(ProjectContext context,
        GeneratorArtifactWriter artifacts)
    {
        var hintName = HintNameBuilder.GetExtensionHint(context.Project);
        if (!artifacts.TryGetFile(hintName, out var path))
            return Array.Empty<EvidenceRegistration>();

        var summary = RegistrationSummaryBuilder.Build(path!);
        return summary.Records.Select(record => new EvidenceRegistration(
                record.Kind.ToString(),
                record.ServiceType,
                record.ImplementationType,
                record.Lifetime,
                record.IsConditional,
                record.UsesFactory))
            .ToArray();
    }

    private static IReadOnlyList<EvidenceGeneratedArtifact> BuildGeneratedArtifacts(GeneratorArtifactWriter artifacts)
    {
        return artifacts.GetArtifacts()
            .Select(artifact => new EvidenceGeneratedArtifact(
                artifact.ArtifactId,
                Path.GetFileName(artifact.Path),
                artifact.Path,
                ComputeFingerprint(artifact.Path),
                new FileInfo(artifact.Path).Length))
            .ToArray();
    }

    private static IReadOnlyList<EvidenceRegistration> FilterRegistrations(IReadOnlyList<EvidenceRegistration> registrations,
        string typeName)
    {
        return registrations.Where(r =>
                TypeFilterUtility.Matches(r.serviceType, typeName) || TypeFilterUtility.Matches(r.implementationType, typeName))
            .ToArray();
    }

    private static EvidenceConfiguration BuildConfiguration(IReadOnlyList<ServiceFieldReport> reports,
        string? settingsPath)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var report in reports)
        foreach (var cfg in report.ConfigurationFields)
        {
            var key = string.IsNullOrWhiteSpace(cfg.ConfigurationKey)
                ? ConfigAuditPrinter.InferSectionKeyFromTypeName(cfg.TypeName)
                : cfg.ConfigurationKey!;
            keys.Add(key);
        }

        var settingsKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var doc = JsonDocument.Parse(stream);
                Flatten(doc.RootElement, settingsKeys, string.Empty);
            }
            catch
            {
                // Evidence output preserves discovered configuration truth rather than surfacing file read failures.
            }

        var missing = settingsKeys.Count == 0
            ? keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray()
            : keys.Where(k => !settingsKeys.Contains(k))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new EvidenceConfiguration(
            keys.Count,
            settingsKeys.Count,
            missing,
            keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void Flatten(JsonElement element,
        HashSet<string> keys,
        string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var next = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                    Flatten(property.Value, keys, next);
                }

                break;
            default:
                if (!string.IsNullOrEmpty(prefix))
                    keys.Add(prefix);
                break;
        }
    }

    private static IReadOnlyList<EvidenceMigrationHint> BuildMigrationHints(ProjectContext context,
        string? typeFilter,
        CancellationToken token)
    {
        var hints = new List<EvidenceMigrationHint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(token);
            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable, token) is not IFieldSymbol symbol)
                        continue;

                    var service = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", string.Empty, StringComparison.Ordinal);
                    if (!string.IsNullOrWhiteSpace(typeFilter) && !TypeFilterUtility.Matches(service, typeFilter!))
                        continue;

                    foreach (var attribute in symbol.GetAttributes())
                    {
                        var attributeName = attribute.AttributeClass?.Name;
                        if (attributeName is "InjectAttribute" or "Inject")
                        {
                            var key = $"{service}|Inject|{symbol.Name}";
                            if (seen.Add(key))
                            {
                                hints.Add(new EvidenceMigrationHint(
                                    service,
                                    "Inject",
                                    symbol.Name,
                                    $"{service} uses [Inject] field '{symbol.Name}'; never use [Inject] in new code. Prefer [DependsOn] for {symbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}."));
                            }
                        }
                        else if (attributeName is "InjectConfigurationAttribute" or "InjectConfiguration")
                        {
                            var configurationKey = attribute.ConstructorArguments.Length > 0 &&
                                                   attribute.ConstructorArguments[0].Value is string configuredKey
                                ? configuredKey
                                : null;
                            var key = $"{service}|InjectConfiguration|{symbol.Name}";
                            if (seen.Add(key))
                            {
                                var details = configurationKey == null
                                    ? string.Empty
                                    : $" for '{configurationKey}'";
                                hints.Add(new EvidenceMigrationHint(
                                    service,
                                    "InjectConfiguration",
                                    symbol.Name,
                                    $"{service} uses InjectConfiguration on '{symbol.Name}'{details}; never use InjectConfiguration in new code. Prefer [DependsOnConfiguration] or [DependsOnOptions]."));
                            }
                        }
                    }
                }
            }
        }

        return hints;
    }

    private static EvidenceCompare BuildCompare(string baselineDirectory,
        string outputDirectory,
        IReadOnlyList<EvidenceGeneratedArtifact> generatedArtifacts)
    {
        var changedArtifacts = new List<string>();
        var deltas = new List<EvidenceArtifactDelta>();
        var baselineArtifacts = new Dictionary<string, EvidenceGeneratedArtifact>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(baselineDirectory))
        {
            foreach (var path in Directory.GetFiles(baselineDirectory, "*.g.cs", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                baselineArtifacts[fileName] = new EvidenceGeneratedArtifact(
                    fileName,
                    fileName,
                    path,
                    ComputeFingerprint(path),
                    new FileInfo(path).Length);
            }
        }

        var currentArtifacts = generatedArtifacts.ToDictionary(artifact => artifact.artifactId, StringComparer.OrdinalIgnoreCase);
        var allKeys = new HashSet<string>(baselineArtifacts.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(currentArtifacts.Keys);

        foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            baselineArtifacts.TryGetValue(key, out var baselineArtifact);
            currentArtifacts.TryGetValue(key, out var currentArtifact);

            var status = baselineArtifact == null
                ? "added"
                : currentArtifact == null
                    ? "removed"
                    : string.Equals(baselineArtifact.fingerprint, currentArtifact.fingerprint, StringComparison.Ordinal)
                        ? "unchanged"
                        : "changed";

            if (!string.Equals(status, "unchanged", StringComparison.Ordinal))
                changedArtifacts.Add(key);

            deltas.Add(new EvidenceArtifactDelta(
                key,
                currentArtifact?.fileName ?? baselineArtifact?.fileName ?? key,
                status,
                baselineArtifact?.path,
                currentArtifact?.path,
                baselineArtifact?.fingerprint,
                currentArtifact?.fingerprint));
        }

        return new EvidenceCompare(outputDirectory, baselineDirectory, changedArtifacts, deltas);
    }

    private static string ComputeFingerprint(string path)
    {
        var hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
