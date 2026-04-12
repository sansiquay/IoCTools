namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class ManualRegistrationValidator
{
    internal static void ValidateAllTrees(SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        HashSet<string>? autoConfigOptionTypes = null)
    {
        autoConfigOptionTypes ??= new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol ??
                                   symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (methodSymbol == null) continue;
                var name = methodSymbol.Name;
                var containing = methodSymbol.ContainingType?.ToDisplayString();

                // Options duplication detection
                if (name is "AddOptions" or "Configure" or "ConfigureOptions" ||
                    name is "Bind" or "BindConfiguration" ||
                    name is "BindOptions")
                {
                    var typeArgs = methodSymbol.TypeArguments;
                    if (typeArgs.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
                        typeArgs = methodSymbol.ReducedFrom.TypeArguments;
                    if (typeArgs.Length > 0)
                    {
                        var opt = typeArgs[0];
                        var optName = Normalize(opt.ToDisplayString());
                        if (autoConfigOptionTypes.Contains(optName))
                        {
                            var diag = Diagnostic.Create(
                                ApplyManualSeverity(DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools, diagnosticConfig),
                                invocation.GetLocation(),
                                optName);
                            context.ReportDiagnostic(diag);
                            continue;
                        }
                    }

                    // Extension methods on OptionsBuilder<T>
                    var receiver = methodSymbol.ReducedFrom?.ReceiverType ?? methodSymbol.ReceiverType;
                    if (receiver != null && receiver.Name.StartsWith("OptionsBuilder", StringComparison.Ordinal) &&
                        receiver is INamedTypeSymbol { TypeArguments.Length: > 0 } optBuilder &&
                        optBuilder.TypeArguments[0] is ITypeSymbol optArg)
                    {
                        var optName = Normalize(optArg.ToDisplayString());
                        if (autoConfigOptionTypes.Contains(optName))
                        {
                            var diag = Diagnostic.Create(
                                ApplyManualSeverity(DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools, diagnosticConfig),
                                invocation.GetLocation(),
                                optName);
                            context.ReportDiagnostic(diag);
                            continue;
                        }
                    }
                }

                if (containing == null || !containing.Contains("Microsoft.Extensions.DependencyInjection")) continue;

                // Check for IServiceCollection.Add{Lifetime}() extension methods
                var isAddMethod = name is "AddScoped" or "AddSingleton" or "AddTransient";
                // Check for ServiceDescriptor.{Lifetime}() static factory methods (D-02)
                var isServiceDescriptorFactory = name is "Scoped" or "Singleton" or "Transient"
                    && methodSymbol.ContainingType?.Name == "ServiceDescriptor";
                if (!isAddMethod && !isServiceDescriptorFactory) continue;

                var lifetime = name switch
                {
                    "AddScoped" or "Scoped" => "Scoped",
                    "AddSingleton" or "Singleton" => "Singleton",
                    "AddTransient" or "Transient" => "Transient",
                    _ => null
                };
                if (lifetime == null) continue;

                var typeArgsSymbol = methodSymbol.TypeArguments;
                if (typeArgsSymbol.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
                    typeArgsSymbol = methodSymbol.ReducedFrom.TypeArguments;

                INamedTypeSymbol? svcNamed = null;
                INamedTypeSymbol? implNamed = null;
                bool isTypeOfRegistration = false;
                bool isOpenGenericTypeOfRegistration = false;

                if (typeArgsSymbol.Length > 0)
                {
                    // Generic type argument path (existing behavior)
                    svcNamed = typeArgsSymbol[0] as INamedTypeSymbol;
                    implNamed = typeArgsSymbol.Length > 1
                        ? typeArgsSymbol[1] as INamedTypeSymbol
                        : svcNamed;
                    if (svcNamed == null || implNamed == null) continue;
                }
                else
                {
                    // typeof() argument path (DIAG-01)
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count < 1) continue;

                    var svcType = ExtractTypeFromTypeOf(args[0], semanticModel);
                    var implType = args.Count >= 2
                        ? ExtractTypeFromTypeOf(args[1], semanticModel)
                        : svcType;

                    if (svcType == null || implType == null) continue;

                    svcNamed = svcType;
                    implNamed = implType;
                    isTypeOfRegistration = true;
                    isOpenGenericTypeOfRegistration =
                        IsOpenGenericTypeOf(args[0]) || (args.Count >= 2 && IsOpenGenericTypeOf(args[1]));
                    // Fall through to the shared diagnostic emission logic below
                }

                var serviceTypeName = Normalize(svcNamed.ToDisplayString());
                var implTypeName = Normalize(implNamed.ToDisplayString());
                var serviceLookupName = Normalize(svcNamed.OriginalDefinition.ToDisplayString());
                var implementationLookupName = Normalize(implNamed.OriginalDefinition.ToDisplayString());
                var openGenericMappingCoveredByIoCTools = !isOpenGenericTypeOfRegistration ||
                                                          IsOpenGenericMappingCoveredByIoCTools(svcNamed, implNamed);

                if (isTypeOfRegistration && isOpenGenericTypeOfRegistration && !openGenericMappingCoveredByIoCTools)
                {
                    var diag = Diagnostic.Create(
                        ApplyManualSeverity(DiagnosticDescriptors.OpenGenericTypeOfCouldUseAttributes, diagnosticConfig),
                        invocation.GetLocation(),
                        serviceTypeName);
                    context.ReportDiagnostic(diag);
                    continue;
                }

                var iocLifetime = GetLifetimeIfAttributed(implNamed) ?? GetLifetimeIfAttributed(svcNamed);
                if (iocLifetime == null && isOpenGenericTypeOfRegistration)
                    iocLifetime = GetLifetimeIfAttributed(implNamed.OriginalDefinition) ??
                                  GetLifetimeIfAttributed(svcNamed.OriginalDefinition);

                if (iocLifetime == null)
                {
                    if (serviceLifetimes.TryGetValue(
                            isOpenGenericTypeOfRegistration ? serviceLookupName : serviceTypeName,
                            out var mappedLifetime))
                        iocLifetime = mappedLifetime;
                    else if ((isOpenGenericTypeOfRegistration ? implementationLookupName : implTypeName) !=
                             (isOpenGenericTypeOfRegistration ? serviceLookupName : serviceTypeName) &&
                             serviceLifetimes.TryGetValue(
                                 isOpenGenericTypeOfRegistration ? implementationLookupName : implTypeName,
                                 out var implMappedLifetime))
                        iocLifetime = implMappedLifetime;
                }

                if (iocLifetime == null)
                {
                    var diag = Diagnostic.Create(
                        ApplyManualSeverity(
                            isTypeOfRegistration && isOpenGenericTypeOfRegistration
                                ? DiagnosticDescriptors.OpenGenericTypeOfCouldUseAttributes
                                : isTypeOfRegistration
                                ? DiagnosticDescriptors.TypeOfRegistrationCouldUseAttributes
                                : DiagnosticDescriptors.ManualRegistrationCouldUseAttributes,
                            diagnosticConfig),
                        invocation.GetLocation(),
                        isTypeOfRegistration && isOpenGenericTypeOfRegistration
                            ? new object[] { serviceTypeName }
                            : new object[] { serviceTypeName, lifetime, implTypeName });
                    context.ReportDiagnostic(diag);
                    continue;
                }

                if (iocLifetime == lifetime)
                {
                    var diag = Diagnostic.Create(
                        ApplyManualSeverity(
                            isTypeOfRegistration
                                ? DiagnosticDescriptors.TypeOfRegistrationDuplicatesIoCTools
                                : DiagnosticDescriptors.ManualRegistrationDuplicatesIoCTools,
                            diagnosticConfig),
                        invocation.GetLocation(), serviceTypeName, lifetime, implTypeName);
                    context.ReportDiagnostic(diag);
                }
                else
                {
                    var diag = Diagnostic.Create(
                        ApplyManualSeverity(
                            isTypeOfRegistration
                                ? DiagnosticDescriptors.TypeOfRegistrationLifetimeMismatch
                                : DiagnosticDescriptors.ManualRegistrationLifetimeMismatch,
                            diagnosticConfig),
                        invocation.GetLocation(), serviceTypeName, lifetime, iocLifetime);
                    context.ReportDiagnostic(diag);
                }
            }
        }

        static string Normalize(string name)
        {
            return name.StartsWith("global::", StringComparison.Ordinal)
                ? name.Substring("global::".Length)
                : name;
        }

        static string? GetLifetimeIfAttributed(INamedTypeSymbol symbol)
        {
            var (hasLifetime, _, _, _) = ServiceDiscovery.GetDirectLifetimeAttributes(symbol);
            return hasLifetime ? LifetimeUtilities.GetServiceLifetimeFromSymbol(symbol, "Scoped") : null;
        }

        static INamedTypeSymbol? ExtractTypeFromTypeOf(ArgumentSyntax arg, SemanticModel semanticModel)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                return null;
            // CRITICAL: GetTypeInfo on typeOfExpr.Type, NOT on typeOfExpr itself (Pitfall 1)
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            return typeInfo.Type as INamedTypeSymbol;
        }

        static bool IsOpenGenericTypeOf(ArgumentSyntax arg)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                return false;
            return typeOfExpr.Type
                .DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .Any(generic => generic.TypeArgumentList.Arguments.Any(a => a is OmittedTypeArgumentSyntax));
        }

        static bool IsOpenGenericMappingCoveredByIoCTools(INamedTypeSymbol serviceSymbol, INamedTypeSymbol implementationSymbol)
        {
            var serviceDefinition = serviceSymbol.OriginalDefinition;
            var implementationDefinition = implementationSymbol.OriginalDefinition;

            if (implementationDefinition.GetAttributes().Any(AttributeTypeChecker.IsNonGenericSkipRegistrationAttribute))
                return false;

            var registerAsAllAttribute = implementationDefinition.GetAttributes()
                .FirstOrDefault(attr => AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));

            if (registerAsAllAttribute != null)
            {
                var registrationMode = AttributeParser.GetRegistrationMode(registerAsAllAttribute);
                if (OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition))
                    return registrationMode is not "Exclusionary";

                if (ImplementsOpenGenericService(implementationDefinition, serviceDefinition))
                    return registrationMode is not "DirectOnly";
            }

            foreach (var registerAsAttribute in implementationDefinition.GetAttributes()
                         .Where(AttributeTypeChecker.IsRegisterAsAttribute))
            {
                if (OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition))
                    return false;

                if (RegisterAsCoversOpenGenericService(registerAsAttribute, serviceDefinition))
                    return true;
            }

            return OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition) &&
                   GetLifetimeIfAttributed(implementationDefinition) != null;
        }

        static bool ImplementsOpenGenericService(INamedTypeSymbol implementationSymbol, INamedTypeSymbol serviceSymbol)
        {
            return implementationSymbol.AllInterfaces.Any(@interface => OpenGenericSymbolsMatch(@interface, serviceSymbol));
        }

        static bool RegisterAsCoversOpenGenericService(AttributeData registerAsAttribute, INamedTypeSymbol serviceSymbol)
        {
            var typeArguments = registerAsAttribute.AttributeClass?.TypeArguments ?? default;
            return !typeArguments.IsDefaultOrEmpty && typeArguments.Any(typeArgument =>
                typeArgument is INamedTypeSymbol namedType && OpenGenericSymbolsMatch(namedType, serviceSymbol));
        }

        static bool OpenGenericSymbolsMatch(INamedTypeSymbol left, INamedTypeSymbol right)
        {
            return string.Equals(GetOpenGenericSymbolKey(left), GetOpenGenericSymbolKey(right), StringComparison.Ordinal);
        }

        static string GetOpenGenericSymbolKey(INamedTypeSymbol symbol)
        {
            var definition = symbol.OriginalDefinition;
            var containingTypes = new Stack<string>();
            var currentContainingType = definition.ContainingType;

            while (currentContainingType != null)
            {
                containingTypes.Push(currentContainingType.MetadataName);
                currentContainingType = currentContainingType.ContainingType;
            }

            var namespaceName = definition.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var containingTypePath = containingTypes.Count == 0
                ? string.Empty
                : string.Join("+", containingTypes) + "+";

            return namespaceName + "|" + containingTypePath + definition.MetadataName;
        }

        static DiagnosticDescriptor ApplyManualSeverity(DiagnosticDescriptor baseDescriptor,
            DiagnosticConfiguration diagnosticConfig)
        {
            return diagnosticConfig.ManualImplementationSeverityConfigured
                ? DiagnosticUtilities.CreateDynamicDescriptor(baseDescriptor, diagnosticConfig.ManualImplementationSeverity)
                : baseDescriptor;
        }
    }
}
