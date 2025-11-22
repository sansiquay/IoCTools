namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

internal static class ConfigurationBindingPresenceValidator
{
    internal static void Validate(SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ServiceClassInfo> services)
    {
        if (services.IsDefaultOrEmpty) return;

        var (configuredOptions, configuredOptionNames) = DiscoverConfiguredOptions(compilation);

        foreach (var service in services)
        {
            if (service.ClassDeclaration == null || service.SemanticModel == null) continue;

            var configs = ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(service.ClassSymbol,
                service.SemanticModel);

            foreach (var cfg in configs)
            {
                if (!cfg.Required) continue; // only warn for required entries
                if (!cfg.IsOptionsPattern) continue; // best-effort heuristic focuses on options pattern

                var optionsType = cfg.GetOptionsInnerType();
                if (optionsType == null) continue;

                var optionsName = optionsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (configuredOptions.Contains(optionsType, SymbolEqualityComparer.Default) ||
                    configuredOptionNames.Contains(optionsName)) continue;

                var location = ResolveLocation(cfg, service.ClassSymbol, service.ClassDeclaration);
                var sectionName = cfg.GetSectionName();
                var optionsTypeName = optionsType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationBindingMissing,
                    location,
                    sectionName,
                    optionsTypeName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static (HashSet<ITypeSymbol> Symbols, HashSet<string> Names) DiscoverConfiguredOptions(
        Compilation compilation)
    {
        var configured = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var configuredNames = new HashSet<string>(StringComparer.Ordinal);

        // Interface-based configurators are strong signals
        var optionConfiguratorInterfaces = new HashSet<string>
        {
            "Microsoft.Extensions.Options.IConfigureOptions`1",
            "Microsoft.Extensions.Options.IConfigureNamedOptions`1",
            "Microsoft.Extensions.Options.IValidateOptions`1"
        };

        var allTypes = new List<INamedTypeSymbol>();
        DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, allTypes);

        foreach (var type in allTypes)
        foreach (var iface in type.AllInterfaces)
        {
            var ifaceName = iface.OriginalDefinition.ToDisplayString();
            var isMatch = optionConfiguratorInterfaces.Contains(ifaceName) ||
                          (iface.Name.StartsWith("IConfigure", StringComparison.Ordinal) &&
                           iface.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Options");
            if (!isMatch) continue;
            if (iface is INamedTypeSymbol { TypeArguments.Length: > 0 } named && named.TypeArguments[0] != null)
            {
                configured.Add(named.TypeArguments[0]);
                configuredNames.Add(named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }

        // Method-based configuration discovery (Configure/AddOptions/BindConfiguration/Bind)
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var root = syntaxTree.GetRoot();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol != null)
                    RegisterFromMethodSymbol(symbol, configured, configuredNames);

                var target = invocation.Expression;
                if (target is GenericNameSyntax gname)
                    RegisterIfOptionsMethod(gname, semanticModel, configured, configuredNames);
                else if (target is MemberAccessExpressionSyntax { Name: GenericNameSyntax memberGeneric })
                    RegisterIfOptionsMethod(memberGeneric, semanticModel, configured, configuredNames);
                else if (target is MemberAccessExpressionSyntax memberAccess &&
                         memberAccess.Name is IdentifierNameSyntax)
                    RegisterConfigurationBinderCalls(invocation, semanticModel, configured, configuredNames);
            }

            foreach (var objCreation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (objCreation.Type is not GenericNameSyntax g) continue;
                if (g.Identifier.Text is "OptionsBuilder" or "OptionsWrapper" && g.TypeArgumentList.Arguments.Count > 0)
                {
                    var typeInfo = semanticModel.GetTypeInfo(g.TypeArgumentList.Arguments[0]);
                    if (typeInfo.Type != null)
                    {
                        configured.Add(typeInfo.Type);
                        configuredNames.Add(typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }
            }
        }

        return (configured, configuredNames);
    }

    private static void RegisterIfOptionsMethod(GenericNameSyntax gname,
        SemanticModel semanticModel,
        HashSet<ITypeSymbol> configured,
        HashSet<string> configuredNames)
    {
        if (!IsOptionsRegistrationName(gname.Identifier.Text)) return;
        if (gname.TypeArgumentList.Arguments.Count == 0) return;

        var typeInfo = semanticModel.GetTypeInfo(gname.TypeArgumentList.Arguments[0]);
        if (typeInfo.Type != null)
        {
            configured.Add(typeInfo.Type);
            configuredNames.Add(typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
    }

    private static bool IsOptionsRegistrationName(string identifier) => identifier is "Configure" or "PostConfigure"
        or "ConfigureOptions" or "PostConfigureAll" or "AddOptions" or "BindConfiguration" or "Bind" or "Get"
        or "GetValue";

    private static void RegisterConfigurationBinderCalls(InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        HashSet<ITypeSymbol> configured,
        HashSet<string> configuredNames)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null) return;

        // IConfiguration/IConfigurationSection extension methods from ConfigurationBinder
        if (symbol.ContainingType?.ToDisplayString() != "Microsoft.Extensions.Configuration.ConfigurationBinder")
            return;

        // Generic Get<T>(), Bind<T>(), GetValue<T>()
        if (symbol.IsGenericMethod && symbol.TypeArguments.Length > 0)
        {
            var typeArg = symbol.TypeArguments[0];
            configured.Add(typeArg);
            configuredNames.Add(typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            return;
        }

        // Bind(object instance) pattern: detect argument type if it's an object creation
        if (symbol.Name == "Bind" && invocation.ArgumentList.Arguments.Count > 0)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            var typeInfo = semanticModel.GetTypeInfo(firstArg);
            if (typeInfo.Type != null)
            {
                configured.Add(typeInfo.Type);
                configuredNames.Add(typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }
    }

    private static void RegisterFromMethodSymbol(IMethodSymbol symbol,
        HashSet<ITypeSymbol> configured,
        HashSet<string> configuredNames)
    {
        // OptionsBuilder<T>.Bind / BindConfiguration
        if (symbol.ReceiverType is INamedTypeSymbol receiver &&
            receiver.OriginalDefinition.ToDisplayString() == "Microsoft.Extensions.Options.OptionsBuilder`1")
            if (receiver.TypeArguments.Length > 0 && receiver.TypeArguments[0] != null)
            {
                configured.Add(receiver.TypeArguments[0]);
                configuredNames.Add(receiver.TypeArguments[0]
                    .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
    }

    private static Location ResolveLocation(ConfigurationInjectionInfo info,
        INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration)
    {
        // Attempt to locate the field on the class
        var field = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(f => string.Equals(f.Name, info.FieldName, StringComparison.Ordinal));
        var fieldLocation = field?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
        if (fieldLocation != null) return fieldLocation;

        // Fallback to first configuration attribute location
        var attr = classSymbol.GetAttributes()
            .FirstOrDefault(a => AttributeParser.IsDependsOnConfigurationAttribute(a) ||
                                 a.AttributeClass?.Name?.Contains("InjectConfiguration") == true);
        var attrLoc = attr?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation();
        if (attrLoc != null) return attrLoc;

        return classDeclaration.Identifier.GetLocation();
    }
}
