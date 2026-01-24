namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;

using Microsoft.CodeAnalysis.CSharp;

internal static class ManualRegistrationValidator
{
    internal static void ValidateAllTrees(SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, string> serviceLifetimes,
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
                                DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools,
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
                                DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools,
                                invocation.GetLocation(),
                                optName);
                            context.ReportDiagnostic(diag);
                            continue;
                        }
                    }
                }

                if (containing == null || !containing.Contains("Microsoft.Extensions.DependencyInjection")) continue;

                if (name is not ("AddScoped" or "AddSingleton" or "AddTransient")) continue;

                var lifetime = name switch
                {
                    "AddScoped" => "Scoped",
                    "AddSingleton" => "Singleton",
                    "AddTransient" => "Transient",
                    _ => null
                };
                if (lifetime == null) continue;

                var typeArgsSymbol = methodSymbol.TypeArguments;
                if (typeArgsSymbol.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
                    typeArgsSymbol = methodSymbol.ReducedFrom.TypeArguments;
                if (typeArgsSymbol.Length == 0) continue;

                var svcNamed = typeArgsSymbol[0] as INamedTypeSymbol;
                var implNamed = typeArgsSymbol.Length > 1
                    ? typeArgsSymbol[1] as INamedTypeSymbol
                    : svcNamed;
                if (svcNamed == null || implNamed == null) continue;

                var serviceTypeName = Normalize(svcNamed.ToDisplayString());
                var implTypeName = Normalize(implNamed.ToDisplayString());

                var iocLifetime = GetLifetimeIfAttributed(implNamed) ?? GetLifetimeIfAttributed(svcNamed);

                if (iocLifetime == null)
                {
                    if (serviceLifetimes.TryGetValue(serviceTypeName, out var mappedLifetime))
                        iocLifetime = mappedLifetime;
                    else if (implTypeName != serviceTypeName &&
                             serviceLifetimes.TryGetValue(implTypeName, out var implMappedLifetime))
                        iocLifetime = implMappedLifetime;
                }

                if (iocLifetime == null)
                {
                    var diag = Diagnostic.Create(DiagnosticDescriptors.ManualRegistrationCouldUseAttributes,
                        invocation.GetLocation(), serviceTypeName, lifetime, implTypeName);
                    context.ReportDiagnostic(diag);
                    continue;
                }

                if (iocLifetime == lifetime)
                {
                    var diag = Diagnostic.Create(DiagnosticDescriptors.ManualRegistrationDuplicatesIoCTools,
                        invocation.GetLocation(), serviceTypeName, lifetime, implTypeName);
                    context.ReportDiagnostic(diag);
                }
                else
                {
                    var diag = Diagnostic.Create(DiagnosticDescriptors.ManualRegistrationLifetimeMismatch,
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
    }
}
