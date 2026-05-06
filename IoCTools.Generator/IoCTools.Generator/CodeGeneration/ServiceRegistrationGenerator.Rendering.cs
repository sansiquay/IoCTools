namespace IoCTools.Generator.CodeGeneration;

using System.Text;

using Microsoft.CodeAnalysis;

internal static partial class ServiceRegistrationGenerator
{
    private static string GenerateServiceRegistrationCode(ServiceRegistration service,
        HashSet<string> uniqueNamespaces,
        string indentation,
        bool shouldUseSimplifiedNamingForConsistency = false)
    {
        var registrationCode = new StringBuilder();

        var isConditionalService = service is ConditionalServiceRegistration;
        var hasConfigInjection = service.HasConfigurationInjection;

        var useGlobalQualifiedNames =
            (!isConditionalService || hasConfigInjection) && !shouldUseSimplifiedNamingForConsistency;

        var interfaceType = useGlobalQualifiedNames
            ? TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                service.InterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            : TypeNameSimplifier.SimplifyTypesForConditionalServices(
                service.InterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var classType = useGlobalQualifiedNames
            ? TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            : TypeNameSimplifier.SimplifyTypesForConditionalServices(
                service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        var interfaceTypeForRegistration = ConvertToOpenGeneric(interfaceType, service.InterfaceSymbol);
        var classTypeForRegistration = ConvertToOpenGeneric(classType, service.ClassSymbol);

        var lifetime = service.Lifetime;

        // The "BackgroundService" lifetime is a synthetic marker meaning "register via
        // services.AddHostedService<T>()". Only the concrete row (ClassSymbol == InterfaceSymbol)
        // should render that call. Interface rows for hosted-service classes that explicitly opt
        // into companion-interface registration via [RegisterAs<T>] (including the bridged
        // IHostedService row itself) fall through to the normal factory-bridge emission path.
        if (lifetime == "BackgroundService" &&
            SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
        {
            // Defense-in-depth: services.AddHostedService<T>() requires a closed generic. Selectors
            // are expected to skip open-generic IHostedService implementers upstream and surface
            // IOC073 there; emitting nothing here prevents invalid codegen if a future caller
            // bypasses that guard.
            if (service.ClassSymbol.TypeParameters.Length > 0)
                return string.Empty;

            // Defense-in-depth: the generated registration extension lives in a separate type and
            // namespace, so any link of the containing-type chain that is below 'internal' makes
            // the BackgroundService row unreferenceable (CS0122). Selectors are expected to skip
            // these upstream and surface IOC066; emitting nothing here protects against future
            // bypasses.
            if (!Analysis.TypeAnalyzer.IsAccessibleFromGeneratedRegistrationExtension(service.ClassSymbol))
                return string.Empty;

            var backgroundServiceType = isConditionalService
                ? TypeNameSimplifier.SimplifyTypesForConditionalServices(
                    service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                : TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                    service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            registrationCode.AppendLine($"{indentation}services.AddHostedService<{backgroundServiceType}>();");
            return registrationCode.ToString();
        }

        // Microsoft.Extensions.DependencyInjection does not allow open generic service descriptors
        // that use implementation factories. Fall back to direct open generic registrations instead.
        var openGenericAliasFactoryUnsupported =
            service.ClassSymbol.TypeParameters.Length > 0 &&
            !SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol);

        var needsFactoryPattern =
            !openGenericAliasFactoryUnsupported &&
            (service.UseSharedInstance || (service.HasConfigurationInjection && !isConditionalService));

        if (needsFactoryPattern)
        {
            if (service.ClassSymbol.TypeParameters.Length > 0)
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}(typeof({classTypeForRegistration}));");
                else
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}(typeof({interfaceTypeForRegistration}), provider => provider.GetRequiredService(typeof({classTypeForRegistration})));");
            }
            else
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                {
                    if (service.UseSharedInstance)
                        registrationCode.AppendLine($"{indentation}services.Add{lifetime}<{classType}>();");
                    else
                        registrationCode.AppendLine(
                            $"{indentation}services.Add{lifetime}<{classType}, {classType}>();");
                }
                else
                {
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}<{interfaceType}>(provider => provider.GetRequiredService<{classType}>());");
                }
            }
        }
        else
        {
            if (service.ClassSymbol.TypeParameters.Length > 0)
            {
                registrationCode.AppendLine(
                    $"{indentation}services.Add{lifetime}(typeof({interfaceTypeForRegistration}), typeof({classTypeForRegistration}));");
            }
            else
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                    registrationCode.AppendLine($"{indentation}services.Add{lifetime}<{classType}, {classType}>();");
                else
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}<{interfaceType}, {classType}>();");
            }
        }

        return registrationCode.ToString();
    }

    private static string ConvertToOpenGeneric(string typeName,
        INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeParameters.Length == 0)
            return typeName;

        var openBracketIndex = typeName.IndexOf('<');
        if (openBracketIndex >= 0)
        {
            var baseTypeName = typeName.Substring(0, openBracketIndex);
            var commaCount = typeSymbol.TypeParameters.Length - 1;
            var commas = commaCount > 0 ? new string(',', commaCount) : "";
            return $"{baseTypeName}<{commas}>";
        }

        return typeName;
    }

    private static string RemoveNamespacesAndDots(ISymbol serviceType,
        IEnumerable<string> uniqueNamespaces,
        bool forServiceRegistration = false) =>
        TypeDisplayUtilities.WithoutNamespaces(serviceType, uniqueNamespaces, forServiceRegistration);

    private static void GenerateCollectionWrapperRegistrations(
        List<ServiceRegistration> regularServices,
        StringBuilder registrations,
        HashSet<string> uniqueNamespaces)
    {
        uniqueNamespaces.Add("System.Collections.Generic");
        uniqueNamespaces.Add("System.Linq");

        var interfaceGroups = regularServices
            .Where(s => !SymbolEqualityComparer.Default.Equals(s.ClassSymbol, s.InterfaceSymbol))
            .GroupBy(s => s.InterfaceSymbol, SymbolEqualityComparer.Default)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var interfaceGroup in interfaceGroups)
        {
            var interfaceSymbol = interfaceGroup.First().InterfaceSymbol;
            var interfaceType = TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            registrations.AppendLine(
                $"         services.AddTransient<IList<{interfaceType}>>(provider => provider.GetServices<{interfaceType}>().ToList());");
            registrations.AppendLine(
                $"         services.AddTransient<IReadOnlyList<{interfaceType}>>(provider => provider.GetServices<{interfaceType}>().ToList());");
            registrations.AppendLine(
                $"         services.AddTransient<IReadOnlyCollection<{interfaceType}>>(provider => provider.GetServices<{interfaceType}>().ToList());");
        }
    }
}
