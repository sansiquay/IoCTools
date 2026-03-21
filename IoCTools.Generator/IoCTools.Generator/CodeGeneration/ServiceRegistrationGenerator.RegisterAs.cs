namespace IoCTools.Generator.CodeGeneration;

using IoCTools.Generator.Utilities;

internal static partial class ServiceRegistrationGenerator
{
    private static bool IsRegisterAsAttribute(AttributeData attr)
        => AttributeTypeChecker.IsRegisterAsAttribute(attr);

    private static IEnumerable<ServiceRegistration> GetRegisterAsRegistrationsCore(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAttribute,
        string lifetime,
        ReportDiagnosticDelegate reportDiagnostic)
    {
        var registrations = new List<ServiceRegistration>();
        if (registerAsAttribute.AttributeClass?.TypeArguments == null ||
            !registerAsAttribute.AttributeClass.TypeArguments.Any()) return registrations;

        var instanceSharing = ExtractRegisterAsInstanceSharing(registerAsAttribute);
        var specifiedInterfaces = registerAsAttribute.AttributeClass.TypeArguments;
        var implementedInterfaces = GetAllInterfaces(classSymbol).ToList();
        var seenInterfaces = new HashSet<string>();
        var validSpecifiedInterfaces = new List<INamedTypeSymbol>();

        var hasConfigurationInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
        var skipConcreteRegistration = classSymbol.IsAbstract;

        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.ConditionalServiceAttribute));

        // For InstanceSharing.Shared: concrete class registration only with explicit lifetime or conditional
        // For InstanceSharing.Separate: always register concrete class when interfaces are specified
        var shouldRegisterConcreteClass = instanceSharing == "Shared"
            ? hasLifetimeAttribute || hasConditionalServiceAttribute
            : hasLifetimeAttribute || hasConditionalServiceAttribute || specifiedInterfaces.Any();

        foreach (var specifiedType in specifiedInterfaces)
        {
            if (specifiedType is not INamedTypeSymbol namedType) continue;
            var typeDisplayString = namedType.ToDisplayString();
            if (namedType.TypeKind != TypeKind.Interface)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RegisterAsNonInterfaceType,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None, classSymbol.Name, typeDisplayString);
                reportDiagnostic(diagnostic);
                continue;
            }

            if (!seenInterfaces.Add(typeDisplayString))
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RegisterAsDuplicateInterface,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None, classSymbol.Name, typeDisplayString);
                reportDiagnostic(diagnostic);
                continue;
            }

            var implementsInterface =
                implementedInterfaces.Any(impl => SymbolEqualityComparer.Default.Equals(impl, namedType));
            if (!implementsInterface)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RegisterAsInterfaceNotImplemented,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None, classSymbol.Name, typeDisplayString);
                reportDiagnostic(diagnostic);
                continue;
            }

            validSpecifiedInterfaces.Add(namedType);
        }

        if (instanceSharing == "Shared")
        {
            if (!skipConcreteRegistration && shouldRegisterConcreteClass)
            {
                var useSharedInstanceForConcrete = hasLifetimeAttribute;
                registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime,
                    useSharedInstanceForConcrete, hasConfigurationInjection));
            }

            foreach (var namedType in validSpecifiedInterfaces)
                registrations.Add(new ServiceRegistration(classSymbol, namedType, lifetime, true,
                    hasConfigurationInjection));
        }
        else
        {
            if (!skipConcreteRegistration && shouldRegisterConcreteClass)
                registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime, false,
                    hasConfigurationInjection));
            foreach (var namedType in validSpecifiedInterfaces)
            {
                var useSharedInstance = hasConfigurationInjection;
                registrations.Add(new ServiceRegistration(classSymbol, namedType, lifetime, useSharedInstance,
                    hasConfigurationInjection));
            }
        }

        return registrations;
    }

    internal static IEnumerable<ServiceRegistration> GetRegisterAsRegistrations(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAttribute,
        string lifetime,
        GeneratorExecutionContext context)
        => GetRegisterAsRegistrationsCore(classSymbol, registerAsAttribute, lifetime, context.ReportDiagnostic);

    internal static IEnumerable<ServiceRegistration> GetRegisterAsRegistrationsWithSourceContext(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAttribute,
        string lifetime,
        SourceProductionContext context)
        => GetRegisterAsRegistrationsCore(classSymbol, registerAsAttribute, lifetime, context.ReportDiagnostic);

    private static string ExtractRegisterAsInstanceSharing(AttributeData registerAsAttribute)
    {
        var sharingArg = registerAsAttribute.NamedArguments
            .FirstOrDefault(arg => arg.Key == "InstanceSharing" || arg.Key == "instanceSharing");
        if (sharingArg.Key != null)
        {
            var sharingValue = sharingArg.Value.Value?.ToString();
            if (sharingValue is "Separate" or "Shared") return sharingValue;
        }

        if (registerAsAttribute.ConstructorArguments.Length > 0)
        {
            var ctor = registerAsAttribute.ConstructorArguments[0].Value;
            var explicitSharing = ctor switch { 0 => "Separate", 1 => "Shared", _ => null };
            if (explicitSharing != null) return explicitSharing;
        }

        return "Separate";
    }
}
