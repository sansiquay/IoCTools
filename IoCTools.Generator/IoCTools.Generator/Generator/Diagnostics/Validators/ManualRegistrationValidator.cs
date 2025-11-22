namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using Microsoft.CodeAnalysis.CSharp;

internal static class ManualRegistrationValidator
{
    internal static void ValidateAllTrees(SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, string> serviceLifetimes)
    {
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
                if (name is not ("AddScoped" or "AddSingleton" or "AddTransient")) continue;

                var containing = methodSymbol.ContainingType?.ToDisplayString();
                if (containing == null || !containing.Contains("Microsoft.Extensions.DependencyInjection")) continue;

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

                var iocLifetime = GetLifetimeIfAttributed(implNamed) ?? GetLifetimeIfAttributed(svcNamed);
                if (iocLifetime == null)
                {
                    var diag = Diagnostic.Create(DiagnosticDescriptors.ManualRegistrationCouldUseAttributes,
                        invocation.GetLocation(), Normalize(svcNamed.ToDisplayString()), lifetime, Normalize(implNamed.ToDisplayString()));
                    context.ReportDiagnostic(diag);
                    continue;
                }

                var serviceTypeName = Normalize(svcNamed.ToDisplayString());
                var implTypeName = Normalize(implNamed.ToDisplayString());

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
