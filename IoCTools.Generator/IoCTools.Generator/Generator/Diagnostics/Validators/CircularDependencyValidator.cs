using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Utilities;

namespace IoCTools.Generator.Generator.Diagnostics.Validators;

internal static class CircularDependencyValidator
{
    internal static void ValidateCircularDependenciesComplete(ReportDiagnosticDelegate reportDiagnostic,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        DiagnosticConfiguration diagnosticConfig)
    {
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var dependencyGraph = new Dictionary<string, List<string>>();
        var serviceNameToSymbolMap = new Dictionary<string, INamedTypeSymbol>();
        var interfaceToImplementationMap = new Dictionary<string, string>();
        var processedServices = new HashSet<string>();

        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;
            if (!processedServices.Add(serviceName)) continue;
            serviceNameToSymbolMap[serviceName] = serviceSymbol;

            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
            if (hasExternalServiceAttribute) continue;

            foreach (var implementedInterface in serviceSymbol.AllInterfaces)
            {
                var interfaceTypeName = implementedInterface.ToDisplayString();
                var interfaceName = TypeNameUtilities.ExtractServiceNameFromType(interfaceTypeName);
                if (interfaceName != null) interfaceToImplementationMap[interfaceName] = serviceName;
            }
        }

        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;
            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
            if (hasExternalServiceAttribute) continue;

            var dependencies = ServiceDependencyUtilities.GetAllDependenciesForService(serviceSymbol);
            foreach (var dependency in dependencies)
            {
                if (CollectionUtilities.IsCollectionTypeAdapted(dependency)) continue;
                if (FrameworkTypeUtilities.IsFrameworkType(dependency)) continue;

                var dependencyInterfaceName = TypeNameUtilities.ExtractServiceNameFromType(dependency);
                if (dependencyInterfaceName != null)
                {
                    if (!dependencyGraph.ContainsKey(serviceName))
                        dependencyGraph[serviceName] = new List<string>();

                    if (interfaceToImplementationMap.TryGetValue(dependencyInterfaceName, out var impl))
                        dependencyGraph[serviceName].Add(impl);
                    else
                        dependencyGraph[serviceName].Add(dependencyInterfaceName);
                }
            }
        }

        var circularDependencies = CircularDependencyDetector.DetectCircularDependencies(dependencyGraph);
        foreach (var cycle in circularDependencies)
        {
            var cycleServices = cycle.Split(new[] { " → " }, StringSplitOptions.RemoveEmptyEntries);
            var serviceForDiagnostic = cycleServices.FirstOrDefault(s => serviceNameToSymbolMap.ContainsKey(s));
            if (serviceForDiagnostic != null &&
                serviceNameToSymbolMap.TryGetValue(serviceForDiagnostic, out var symbol))
            {
                var location = symbol.Locations.FirstOrDefault() ?? Location.None;
                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.CircularDependency, diagnosticConfig.LifetimeValidationSeverity);
                var diagnostic = Diagnostic.Create(descriptor, location, cycle);
                reportDiagnostic(diagnostic);
            }
        }
    }
}
