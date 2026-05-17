namespace IoCTools.Testing.CodeGeneration;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using IoCTools.Testing.Analysis;
using IoCTools.Testing.Models;
using IoCTools.Testing.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

internal static class FixtureEmitter
{
    public static void Emit(SourceProductionContext context, TestClassInfo testClass)
    {
        var parameters = ConstructorReader.GetConstructorParameters(testClass.ServiceSymbol);

        var hasFluentValidation = testClass.SemanticModel?.Compilation != null &&
            FluentValidationFixtureHelper.HasFluentValidationReference(testClass.SemanticModel.Compilation);

        var source = GenerateFixtureSource(testClass, parameters, hasFluentValidation);
        var fileName = $"{GetHintName(testClass.TestClassSymbol)}.Fixture.g.cs";
        context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateFixtureSource(
        TestClassInfo testClass,
        ImmutableArray<IParameterSymbol> parameters,
        bool hasFluentValidation)
    {
        var planned = FixtureMemberPlanner.Plan(parameters, testClass.LoggerProfile, testClass.ConcreteHandling);
        var sb = new StringBuilder();

        var classNs = testClass.TestClassNamespace;
        var hasNamespace = !string.IsNullOrEmpty(classNs) && !string.Equals(classNs, "<global namespace>", StringComparison.Ordinal);
        var baseIndent = hasNamespace ? "    " : "";
        var indent = baseIndent;

        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Usings must be emitted before the namespace so System resolves globally even
        // when the consumer has nested namespaces such as MyApp.System.
        var namespaces = CollectNamespaces(testClass.ServiceSymbol, planned);
        namespaces.Add("Moq");
        namespaces.Add("System");
        namespaces.Add("System.Linq");
        namespaces.Add("System.Threading");

        if (planned.Any(m => m.Role == ParameterRole.NullLogger))
            namespaces.Add("Microsoft.Extensions.Logging.Abstractions");

        foreach (var ns in namespaces.OrderBy(n => n))
        {
            sb.AppendLine($"using {ns};");
        }
        sb.AppendLine();

        // Namespace
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {classNs}");
            sb.AppendLine("{");
            sb.AppendLine();
        }

        foreach (var containingType in GetContainingTypes(testClass.TestClassSymbol))
        {
            sb.AppendLine($"{indent}{GetPartialTypeDeclaration(containingType)}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        var memberIndent = indent + "    ";
        var continuationIndent = memberIndent + "    ";

        // Class declaration
        sb.AppendLine($"{indent}{GetPartialTypeDeclaration(testClass.TestClassSymbol)}");
        sb.AppendLine($"{indent}{{");

        // Field declarations
        var ambiguousSimpleTypeNames = GetAmbiguousSimpleTypeNames(planned);

        foreach (var member in planned)
        {
            EmitFieldDeclaration(sb, member, memberIndent, ambiguousSimpleTypeNames);
        }
        sb.AppendLine();

        // Mock accessor properties (preferred API over underscore fields)
        foreach (var member in planned.Where(m => m.Role is not (ParameterRole.NullLogger or ParameterRole.TimeProvider or ParameterRole.ConcreteInstance)))
        {
            EmitMockAccessor(sb, member, memberIndent, ambiguousSimpleTypeNames);
        }
        sb.AppendLine();

        // Lazy Sut property
        var serviceName = GetTypeNameWithoutGlobal(testClass.ServiceSymbol);
        sb.AppendLine($"{memberIndent}private {serviceName}? _sut;");
        sb.AppendLine($"{memberIndent}private {serviceName} Sut => _sut ??= CreateSut();");
        sb.AppendLine();

        // CreateSut() factory
        if (planned.IsEmpty)
        {
            sb.AppendLine($"{memberIndent}public {serviceName} CreateSut() => new();");
        }
        else
        {
            sb.AppendLine($"{memberIndent}public {serviceName} CreateSut() => new(");
            var paramList = string.Join(",\n" + continuationIndent, planned.Select(m => m.ConstructorArgExpression));
            sb.AppendLine($"{continuationIndent}{paramList}");
            sb.AppendLine($"{memberIndent});");
        }
        sb.AppendLine();

        // Setup helper methods
        foreach (var member in planned)
        {
            EmitSetupHelper(sb, member, hasFluentValidation, memberIndent, ambiguousSimpleTypeNames);
        }

        // FluentValidation setup helpers
        if (hasFluentValidation)
        {
            foreach (var member in planned.Where(m => m.Role == ParameterRole.Validator))
            {
                var validatedTypeName = ((INamedTypeSymbol)member.Parameter.Type).TypeArguments[0].ToDisplayString();
                sb.AppendLine();
                var fvHelperSource = FluentValidationFixtureHelper.GenerateSetupHelpers(
                    member.FieldName,
                    validatedTypeName,
                    member.Parameter.Name);
                // Re-indent by replacing the helper's default member indentation.
                if (memberIndent != "        ")
                    fvHelperSource = fvHelperSource.Replace("        ", memberIndent);
                sb.Append(fvHelperSource);
            }
        }

        sb.AppendLine($"{indent}}}");
        foreach (var _ in GetContainingTypes(testClass.TestClassSymbol))
        {
            indent = indent.Length >= 4 ? indent[..^4] : string.Empty;
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    private static IEnumerable<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol symbol)
    {
        var stack = new Stack<INamedTypeSymbol>();
        var current = symbol.ContainingType;
        while (current != null)
        {
            stack.Push(current);
            current = current.ContainingType;
        }

        return stack;
    }

    private static string GetPartialTypeDeclaration(INamedTypeSymbol symbol)
    {
        var declaration = symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        var accessibility = GetAccessibility(symbol.DeclaredAccessibility);
        var typeKeyword = GetTypeKeyword(declaration);
        var identifier = declaration?.Identifier.Text ?? symbol.Name;
        var typeParameters = declaration?.TypeParameterList?.ToFullString().Trim() ?? string.Empty;
        var constraints = declaration?.ConstraintClauses.ToFullString().Trim() ?? string.Empty;
        var header = string.IsNullOrEmpty(accessibility)
            ? $"partial {typeKeyword} {identifier}{typeParameters}"
            : $"{accessibility} partial {typeKeyword} {identifier}{typeParameters}";

        return string.IsNullOrEmpty(constraints)
            ? header
            : $"{header} {constraints}";
    }

    private static string GetTypeKeyword(TypeDeclarationSyntax? declaration)
    {
        return declaration switch
        {
            RecordDeclarationSyntax record when record.ClassOrStructKeyword.Text.Length > 0 =>
                $"record {record.ClassOrStructKeyword.Text}",
            RecordDeclarationSyntax => "record",
            { } typeDeclaration => typeDeclaration.Keyword.Text,
            _ => "class",
        };
    }

    private static string GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => string.Empty,
        };
    }

    private static void EmitFieldDeclaration(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        ISet<string> ambiguousSimpleTypeNames)
    {
        switch (member.Role)
        {
            case ParameterRole.NullLogger:
            {
                // Emit NullLogger<T>.Instance field
                var loggerTypeName = GetTypeNameWithoutGlobal(member.Parameter.Type);
                var categoryName = GetTypeNameWithoutGlobal(((INamedTypeSymbol)member.Parameter.Type).TypeArguments[0]);
                sb.AppendLine($"{indent}private readonly {loggerTypeName} {member.FieldName} = Microsoft.Extensions.Logging.Abstractions.NullLogger<{categoryName}>.Instance;");
                break;
            }
            case ParameterRole.TimeProvider:
                sb.AppendLine($"{indent}private TimeProvider {member.FieldName} {{ get; set; }} = System.TimeProvider.System;");
                break;
            case ParameterRole.ConcreteInstance:
            {
                var paramType = GetTypeNameWithoutGlobal(member.Parameter.Type, ambiguousSimpleTypeNames);
                sb.AppendLine($"{indent}private {paramType} {member.FieldName} {{ get; set; }} = new();");
                break;
            }
            default:
            {
                var paramType = GetTypeNameWithoutGlobal(member.Parameter.Type, ambiguousSimpleTypeNames);
                sb.AppendLine($"{indent}private readonly Mock<{paramType}> {member.FieldName} = new();");
                break;
            }
        }
    }

    private static void EmitMockAccessor(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        ISet<string> ambiguousSimpleTypeNames)
    {
        var paramType = GetTypeNameWithoutGlobal(member.Parameter.Type, ambiguousSimpleTypeNames);
        var accessorName = GetMockAccessorName(member);
        sb.AppendLine($"{indent}private Mock<{paramType}> {accessorName} => {member.FieldName};");
    }

    private static string GetMockAccessorName(FixtureMember member)
    {
        var baseName = member.FieldName.StartsWith("_mock", StringComparison.Ordinal)
            ? member.FieldName.Substring("_mock".Length)
            : member.FieldName.TrimStart('_');

        if (string.IsNullOrEmpty(baseName))
            baseName = TypeNameUtilities.GetSimpleTypeName(member.Parameter.Type);

        return $"{baseName}Mock";
    }

    private static void EmitSetupHelper(
        StringBuilder sb,
        FixtureMember member,
        bool hasFluentValidation,
        string indent,
        ISet<string> ambiguousSimpleTypeNames)
    {
        switch (member.Role)
        {
            case ParameterRole.Configuration:
                EmitConfigurationHelper(sb, member, indent);
                break;
            case ParameterRole.Options:
                EmitOptionsHelper(sb, member, indent);
                break;
            case ParameterRole.NullLogger:
                // No setup helper for NullLogger (not a Mock)
                break;
            case ParameterRole.TimeProvider:
                EmitTimeProviderHelper(sb, member, indent);
                break;
            case ParameterRole.ConcreteInstance:
                EmitConcreteInstanceHelper(sb, member, indent, ambiguousSimpleTypeNames);
                break;
            default:
                EmitStandardHelper(sb, member, indent, ambiguousSimpleTypeNames);
                break;
        }
    }

    private static void EmitConcreteInstanceHelper(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        ISet<string> ambiguousSimpleTypeNames)
    {
        var paramType = GetTypeNameWithoutGlobal(member.Parameter.Type, ambiguousSimpleTypeNames);
        var useMethodName = member.SetupMethodName.StartsWith("Configure", StringComparison.Ordinal)
            ? "Use" + member.SetupMethodName.Substring("Configure".Length)
            : "Use" + TypeNameUtilities.GetSimpleTypeName(member.Parameter.Type);

        sb.AppendLine($"{indent}private {paramType} {useMethodName}({paramType} value)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    {member.FieldName} = value;");
        sb.AppendLine($"{indent}    return {member.FieldName};");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}private {paramType} {member.SetupMethodName}(Action<{paramType}> configure)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    configure({member.FieldName});");
        sb.AppendLine($"{indent}    return {member.FieldName};");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    private static void EmitTimeProviderHelper(StringBuilder sb, FixtureMember member, string indent)
    {
        sb.AppendLine($"{indent}private void UseTimeProvider(TimeProvider timeProvider) => {member.FieldName} = timeProvider;");
        sb.AppendLine();
    }

    private static void EmitStandardHelper(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        ISet<string> ambiguousSimpleTypeNames)
    {
        var paramType = GetTypeNameWithoutGlobal(member.Parameter.Type, ambiguousSimpleTypeNames);
        sb.AppendLine($"{indent}private void {member.SetupMethodName}(Action<Mock<{paramType}>> configure) => configure({member.FieldName});");
        sb.AppendLine();
    }

    private static void EmitConfigurationHelper(StringBuilder sb, FixtureMember member, string indent)
    {
        sb.AppendLine($"{indent}private void ConfigureIConfiguration(Func<string, object?> valueProvider)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x[It.IsAny<string>()])");
        sb.AppendLine($"{indent}        .Returns((string key) => valueProvider(key)?.ToString()!);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.GetSection(It.IsAny<string>()))");
        sb.AppendLine($"{indent}        .Returns((string key) =>");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            var section = new Mock<IConfigurationSection>();");
        sb.AppendLine($"{indent}            section.SetupGet(x => x.Key).Returns(key);");
        sb.AppendLine($"{indent}            section.SetupGet(x => x.Path).Returns(key);");
        sb.AppendLine($"{indent}            section.SetupGet(x => x.Value).Returns(valueProvider(key)?.ToString()!);");
        sb.AppendLine($"{indent}            return section.Object;");
        sb.AppendLine($"{indent}        }});");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}private void ConfigureIConfiguration(params (string Key, object? Value)[] values)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var map = values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine($"{indent}    IConfigurationSection BuildSection(string path)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var section = new Mock<IConfigurationSection>();");
        sb.AppendLine($"{indent}        var key = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;");
        sb.AppendLine($"{indent}        section.SetupGet(x => x.Key).Returns(key);");
        sb.AppendLine($"{indent}        section.SetupGet(x => x.Path).Returns(path);");
        sb.AppendLine($"{indent}        section.SetupGet(x => x.Value).Returns(() => map.TryGetValue(path, out var val) ? val?.ToString() : null);");
        sb.AppendLine($"{indent}        section.SetupSet(x => x.Value = It.IsAny<string?>()).Callback<string?>(value => map[path] = value);");
        sb.AppendLine($"{indent}        section.Setup(x => x[It.IsAny<string>()])");
        sb.AppendLine($"{indent}            .Returns((string childKey) =>");
        sb.AppendLine($"{indent}            {{");
        sb.AppendLine($"{indent}                var childPath = string.IsNullOrEmpty(path) ? childKey : $\"{{path}}:{{childKey}}\";");
        sb.AppendLine($"{indent}                return map.TryGetValue(childPath, out var childValue) ? childValue?.ToString() : null;");
        sb.AppendLine($"{indent}            }});");
        sb.AppendLine($"{indent}        section.Setup(x => x.GetSection(It.IsAny<string>()))");
        sb.AppendLine($"{indent}            .Returns((string childKey) => BuildSection(string.IsNullOrEmpty(path) ? childKey : $\"{{path}}:{{childKey}}\"));");
        sb.AppendLine($"{indent}        section.Setup(x => x.GetChildren())");
        sb.AppendLine($"{indent}            .Returns(() =>");
        sb.AppendLine($"{indent}            {{");
        sb.AppendLine($"{indent}                var prefix = string.IsNullOrEmpty(path) ? string.Empty : path + \":\";");
        sb.AppendLine($"{indent}                return map.Keys");
        sb.AppendLine($"{indent}                    .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))");
        sb.AppendLine($"{indent}                    .Select(k => k[prefix.Length..])");
        sb.AppendLine($"{indent}                    .Where(k => k.Length > 0)");
        sb.AppendLine($"{indent}                    .Select(k => k.Contains(':') ? k[..k.IndexOf(':')] : k)");
        sb.AppendLine($"{indent}                    .Distinct(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine($"{indent}                    .Select(childKey => BuildSection(string.IsNullOrEmpty(path) ? childKey : $\"{{path}}:{{childKey}}\"))");
        sb.AppendLine($"{indent}                    .ToArray();");
        sb.AppendLine($"{indent}            }});");
        sb.AppendLine($"{indent}        return section.Object;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x[It.IsAny<string>()])");
        sb.AppendLine($"{indent}        .Returns((string key) => map.TryGetValue(key, out var val) ? val?.ToString() : null);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.GetSection(It.IsAny<string>()))");
        sb.AppendLine($"{indent}        .Returns((string key) => BuildSection(key));");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.GetChildren())");
        sb.AppendLine($"{indent}        .Returns(() => map.Keys");
        sb.AppendLine($"{indent}            .Select(k => k.Contains(':') ? k[..k.IndexOf(':')] : k)");
        sb.AppendLine($"{indent}            .Distinct(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine($"{indent}            .Select(BuildSection)");
        sb.AppendLine($"{indent}            .ToArray());");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
        sb.AppendLine($"{indent}private void ConfigureConfiguration(Func<string, object?> valueProvider) => ConfigureIConfiguration(valueProvider);");
        sb.AppendLine();
        sb.AppendLine($"{indent}private void ConfigureConfiguration(params (string Key, object? Value)[] values) => ConfigureIConfiguration(values);");
        sb.AppendLine();
    }

    private static void EmitOptionsHelper(StringBuilder sb, FixtureMember member, string indent)
    {
        if (member.Parameter.Type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var optionsArg = namedType.TypeArguments[0];
            var optionsArgName = GetTypeNameWithoutGlobal(optionsArg);
            var useMethodName = $"Use{optionsArg.Name}";
            var configureMethodName = $"Configure{optionsArg.Name}";
            var canCreateOptionsValue = CanCreateOptionsValue(optionsArg);

            if (namedType.Name == "IOptionsMonitor")
            {
                // IOptionsMonitor<T>: Use and Configure helpers using CurrentValue
                sb.AppendLine($"{indent}private {optionsArgName} {useMethodName}({optionsArgName} value)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.CurrentValue).Returns(value);");
                sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Get(It.IsAny<string>())).Returns(value);");
                sb.AppendLine($"{indent}    return value;");
                sb.AppendLine($"{indent}}}");

                if (canCreateOptionsValue)
                {
                    sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}(Action<{optionsArgName}> configure)");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    var options = Microsoft.Extensions.Options.Options.Create(new {optionsArgName}());");
                    sb.AppendLine($"{indent}    configure(options.Value);");
                    sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.CurrentValue).Returns(options.Value);");
                    sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Get(It.IsAny<string>())).Returns(options.Value);");
                    sb.AppendLine($"{indent}    return options.Value;");
                    sb.AppendLine($"{indent}}}");
                }
                else
                {
                    EmitMonitorValueConfigureHelper(sb, member, indent, optionsArgName, configureMethodName);
                }
            }
            else
            {
                // IOptions<T> or IOptionsSnapshot<T>: Use and Configure helpers (Value-based)
                sb.AppendLine($"{indent}private {optionsArgName} {useMethodName}({optionsArgName} value)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Value).Returns(value);");
                sb.AppendLine($"{indent}    return value;");
                sb.AppendLine($"{indent}}}");

                if (canCreateOptionsValue)
                {
                    sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}(Action<{optionsArgName}> configure)");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    var options = Microsoft.Extensions.Options.Options.Create(new {optionsArgName}());");
                    sb.AppendLine($"{indent}    configure(options.Value);");
                    sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Value).Returns(options.Value);");
                    sb.AppendLine($"{indent}    return options.Value;");
                    sb.AppendLine($"{indent}}}");
                }
                else
                {
                    EmitValueConfigureHelper(sb, member, indent, optionsArgName, configureMethodName);
                }

                // IOptionsSnapshot<T> adds named Get overload
                if (namedType.Name == "IOptionsSnapshot")
                {
                    if (canCreateOptionsValue)
                    {
                        sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}(string name, Action<{optionsArgName}> configure)");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    var options = Microsoft.Extensions.Options.Options.Create(new {optionsArgName}());");
                        sb.AppendLine($"{indent}    configure(options.Value);");
                        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Get(name)).Returns(options.Value);");
                        sb.AppendLine($"{indent}    return options.Value;");
                        sb.AppendLine($"{indent}}}");
                    }
                    else
                    {
                        EmitSnapshotValueConfigureHelper(sb, member, indent, optionsArgName, configureMethodName);
                    }
                }
            }

            sb.AppendLine();
        }
    }

    private static void EmitValueConfigureHelper(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        string optionsArgName,
        string configureMethodName)
    {
        sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}({optionsArgName} value, Action<{optionsArgName}> configure)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    configure(value);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Value).Returns(value);");
        sb.AppendLine($"{indent}    return value;");
        sb.AppendLine($"{indent}}}");
    }

    private static void EmitMonitorValueConfigureHelper(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        string optionsArgName,
        string configureMethodName)
    {
        sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}({optionsArgName} value, Action<{optionsArgName}> configure)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    configure(value);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.CurrentValue).Returns(value);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Get(It.IsAny<string>())).Returns(value);");
        sb.AppendLine($"{indent}    return value;");
        sb.AppendLine($"{indent}}}");
    }

    private static void EmitSnapshotValueConfigureHelper(
        StringBuilder sb,
        FixtureMember member,
        string indent,
        string optionsArgName,
        string configureMethodName)
    {
        sb.AppendLine($"{indent}private {optionsArgName} {configureMethodName}(string name, {optionsArgName} value, Action<{optionsArgName}> configure)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    configure(value);");
        sb.AppendLine($"{indent}    {member.FieldName}.Setup(x => x.Get(name)).Returns(value);");
        sb.AppendLine($"{indent}    return value;");
        sb.AppendLine($"{indent}}}");
    }

    private static bool CanCreateOptionsValue(ITypeSymbol optionsType)
    {
        if (optionsType is not INamedTypeSymbol namedType || namedType.IsAbstract)
            return false;

        if (HasRequiredMembers(namedType))
            return false;

        if (namedType.TypeKind == TypeKind.Struct)
            return true;

        return namedType.InstanceConstructors.Any(ctor =>
            ctor.Parameters.Length == 0 &&
            ctor.DeclaredAccessibility == Accessibility.Public);
    }

    private static bool HasRequiredMembers(INamedTypeSymbol type)
    {
        for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers().OfType<IPropertySymbol>().Any(m => m.IsRequired))
                return true;
            if (current.GetMembers().OfType<IFieldSymbol>().Any(m => m.IsRequired))
                return true;
        }

        return false;
    }

    private static string GetHintName(INamedTypeSymbol testClassSymbol)
    {
        var displayName = testClassSymbol
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty);
        var chars = displayName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }

    private static HashSet<string> CollectNamespaces(ITypeSymbol serviceType, ImmutableArray<FixtureMember> members)
    {
        var namespaces = new HashSet<string>();
        CollectNamespacesForType(serviceType, namespaces);
        foreach (var member in members)
        {
            CollectNamespacesForType(member.Parameter.Type, namespaces);
        }
        return namespaces;
    }

    private static void CollectNamespacesForType(ITypeSymbol type, HashSet<string> namespaces)
    {
        var current = type;
        while (current != null)
        {
            var containingNamespace = current.ContainingNamespace;
            var ns = containingNamespace?.ToString();
            if (!string.IsNullOrEmpty(ns) && containingNamespace is { IsGlobalNamespace: false })
                namespaces.Add(ns);
            if (current is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                    CollectNamespacesForType(typeArg, namespaces);
            }
            current = current.ContainingType;
        }
    }

    private static ISet<string> GetAmbiguousSimpleTypeNames(ImmutableArray<FixtureMember> members)
    {
        return members
            .GroupBy(m => TypeNameUtilities.GetSimpleTypeName(m.Parameter.Type), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static string GetTypeNameWithoutGlobal(
        ITypeSymbol type,
        ISet<string>? ambiguousSimpleTypeNames = null)
    {
        var simpleName = TypeNameUtilities.GetSimpleTypeName(type);
        var qualification = ambiguousSimpleTypeNames?.Contains(simpleName) == true
            ? SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
            : SymbolDisplayTypeQualificationStyle.NameAndContainingTypes;

        var format = new SymbolDisplayFormat(
            typeQualificationStyle: qualification,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
        var name = type.ToDisplayString(format);
        return name.Replace("global::", "");
    }
}

internal enum LoggerProfile
{
    Mock,
    NullLogger,
}

internal enum OptionsHelperProfile
{
    Full,
    Minimal,
}

/// <summary>
/// Internal mirror of the public <c>IoCTools.Testing.Annotations.ConcreteHandling</c> enum,
/// used by pipeline + planner to decide whether concrete-class params are emitted as real
/// instances (Auto, default) or as Mock&lt;T&gt; substitutes (ForceMock).
/// </summary>
internal enum ConcreteHandlingMode
{
    Auto,
    ForceMock,
}
