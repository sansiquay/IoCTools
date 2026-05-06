namespace IoCTools.Generator.Generator;

using CodeGeneration;

using IoCTools.Generator.Diagnostics;

using Intent;

using Utilities;

internal static class RegistrationSelector
{
    internal static IEnumerable<ServiceRegistration> GetRegisterAsAllRegistrations(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime) =>
        ServiceRegistrationGenerator.GetMultiInterfaceRegistrations(classSymbol, registerAsAllAttribute, lifetime);

    internal static IEnumerable<ServiceRegistration> GetRegisterAsRegistrationsWithSourceContext(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAttribute,
        string lifetime,
        SourceProductionContext context) => ServiceRegistrationGenerator.GetRegisterAsRegistrationsWithSourceContext(
        classSymbol, registerAsAttribute, lifetime, context);

    internal static IReadOnlyList<INamedTypeSymbol> GetInterfacesForRegistration(
        INamedTypeSymbol classSymbol,
        Action<Diagnostic> reportDiagnostic,
        Func<INamedTypeSymbol, IEnumerable<INamedTypeSymbol>>? interfaceProvider = null)
    {
        try
        {
            return InterfaceDiscovery.GetAllInterfacesForService(classSymbol, interfaceProvider);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or ArgumentException)
        {
            reportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ServiceAnalysisFailure,
                classSymbol?.Locations.FirstOrDefault() ?? Location.None,
                classSymbol?.Name ?? "<unknown>",
                nameof(InterfaceDiscovery)));
            return Array.Empty<INamedTypeSymbol>();
        }
    }

    internal static IEnumerable<ServiceRegistration> GetDefaultRegistrations(
        INamedTypeSymbol classSymbol,
        string lifetime,
        SourceProductionContext context,
        Action<Diagnostic>? reportDiagnostic = null)
    {
        if (DependencySetUtilities.IsDependencySet(classSymbol)) return Enumerable.Empty<ServiceRegistration>();

        var results = new List<ServiceRegistration>();

        // Skip all registrations if non-generic SkipRegistration attribute is present
        var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
            .Any(AttributeTypeChecker.IsNonGenericSkipRegistrationAttribute);
        if (hasNonGenericSkipRegistration)
            return results;

        var hasConfigInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

        // Concrete class registration
        results.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime, false, hasConfigInjection));

        // Interface registrations using utility discovery
        var interfaces = GetInterfacesForRegistration(classSymbol, reportDiagnostic ?? context.ReportDiagnostic);
        foreach (var @interface in interfaces)
            results.Add(new ServiceRegistration(classSymbol, @interface, lifetime, false, hasConfigInjection));

        return results;
    }

    internal static IEnumerable<ServiceRegistration> GetConditionalServiceRegistrations(
        SemanticModel semanticModel,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SourceProductionContext context,
        string implicitLifetime)
    {
        if (DependencySetUtilities.IsDependencySet(classSymbol)) return Enumerable.Empty<ServiceRegistration>();

        var results = new List<ServiceRegistration>();

        // Check for SkipRegistration attribute - skip all registrations if present
        var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
            .Any(AttributeTypeChecker.IsNonGenericSkipRegistrationAttribute);
        if (hasNonGenericSkipRegistration)
            return results;

        // Validate conditional service attributes
        var conditionalServiceAttrs = classSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
            .ToList();

        foreach (var conditionalAttr in conditionalServiceAttrs)
        {
            var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    GeneratorDiagnostics.Report(context, "IOC020",
                        "ConditionalService validation error",
                        $"Class '{classSymbol.Name}': {error}");
        }

        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        var lifetime = hasLifetimeAttribute
            ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol, implicitLifetime)
            : implicitLifetime;

        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);

        // Microsoft.Extensions.DependencyInjection's services.AddHostedService<T>() requires a
        // closed type. Open-generic IHostedService implementers cannot be registered through it
        // because the type parameter cannot be supplied at the registration site. Skip the
        // implicit registration entirely; consumers must provide a closed subclass.
        if (isHostedService && classSymbol.TypeParameters.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.OpenGenericHostedServiceSkipped,
                classSymbol.Locations.FirstOrDefault() ?? Location.None,
                classSymbol.Name));
            return Enumerable.Empty<ServiceRegistration>();
        }

        // Generated registration extensions live in a separate type and namespace. If the hosted
        // service has effective accessibility below 'internal' anywhere in its containing-type
        // chain, services.AddHostedService<TImpl>() emission produces CS0122. Skip and surface
        // IOC066 so the omission is observable.
        if (isHostedService && !TypeAnalyzer.IsAccessibleFromGeneratedRegistrationExtension(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InaccessibleHostedServiceSkipped,
                classSymbol.Locations.FirstOrDefault() ?? Location.None,
                classSymbol.ToDisplayString(),
                EffectiveAccessibilityDescription(classSymbol)));
            return Enumerable.Empty<ServiceRegistration>();
        }

        var serviceRegisterAsAllAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
            AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
        var hasRegisterAs = classSymbol.GetAttributes().Any(AttributeTypeChecker.IsRegisterAsAttribute);
        var bridgesHostedService = isHostedService && (serviceRegisterAsAllAttr != null || hasRegisterAs);

        // When a hosted service explicitly opts into companion-interface registration via
        // [RegisterAs<T>] or [RegisterAsAll], keep the user's declared lifetime so the concrete
        // class is registered once (Singleton/Scoped/Transient) and IHostedService bridges to it
        // via a factory alongside the other interface registrations. Without [RegisterAs*],
        // preserve the implicit-hosted-service shape (services.AddHostedService<T>()).
        if (isHostedService && !bridgesHostedService) lifetime = "BackgroundService";

        foreach (var conditionalAttr in conditionalServiceAttrs)
        {
            var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);
            if (condition == null) continue;

            if (serviceRegisterAsAllAttr != null)
            {
                var multiInterfaceRegistrations =
                    ServiceRegistrationGenerator.GetMultiInterfaceRegistrationsForConditionalServices(
                        classSymbol, serviceRegisterAsAllAttr, lifetime);
                foreach (var registration in multiInterfaceRegistrations)
                    results.Add(new ConditionalServiceRegistration(
                        registration.ClassSymbol,
                        registration.InterfaceSymbol,
                        registration.Lifetime,
                        condition,
                        registration.UseSharedInstance,
                        registration.HasConfigurationInjection));
            }
            else
            {
                var hasConfigInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                var useSharedInstance = hasConfigInjection;
                results.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                    condition, useSharedInstance, hasConfigInjection));

                if (lifetime != "BackgroundService")
                {
                    var allInterfaces = GetInterfacesForRegistration(classSymbol, context.ReportDiagnostic);
                    foreach (var interfaceSymbol in allInterfaces)
                        results.Add(new ConditionalServiceRegistration(classSymbol, interfaceSymbol,
                            lifetime, condition, useSharedInstance, hasConfigInjection));
                }
            }

            if (bridgesHostedService)
            {
                var hostedServiceSymbol = TryGetIHostedServiceSymbol(classSymbol);
                if (hostedServiceSymbol != null)
                    results.Add(new ConditionalServiceRegistration(classSymbol, hostedServiceSymbol, lifetime,
                        condition, true, false));
            }
        }

        return results;
    }

    private static INamedTypeSymbol? TryGetIHostedServiceSymbol(INamedTypeSymbol classSymbol)
    {
        const string hostedServiceFullName = "Microsoft.Extensions.Hosting.IHostedService";
        foreach (var iface in classSymbol.AllInterfaces)
            if (iface.ToDisplayString() == hostedServiceFullName)
                return iface;
        return null;
    }

    private static string EffectiveAccessibilityDescription(INamedTypeSymbol type)
    {
        // Walk the containing-type chain and report the strictest link, which is what actually
        // gates accessibility from a sibling type/namespace.
        var strictest = type.DeclaredAccessibility;
        var current = type.ContainingType;
        while (current != null)
        {
            if (Rank(current.DeclaredAccessibility) < Rank(strictest))
                strictest = current.DeclaredAccessibility;
            current = current.ContainingType;
        }

        return strictest.ToString().ToLowerInvariant();

        static int Rank(Accessibility a) => a switch
        {
            Accessibility.Public => 5,
            Accessibility.Internal or Accessibility.ProtectedOrInternal => 4,
            Accessibility.Protected => 3,
            Accessibility.ProtectedAndInternal => 2,
            Accessibility.Private => 1,
            _ => 0
        };
    }

    internal static IEnumerable<ServiceRegistration> GetServicesToRegisterForSingleClass(
        SemanticModel semanticModel,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SourceProductionContext context,
        string implicitLifetime)
    {
        var serviceRegistrations = new List<ServiceRegistration>();

        try
        {
            if (classSymbol.IsStatic) return serviceRegistrations;
            if (classSymbol.IsAbstract) return serviceRegistrations;
            if (DependencySetUtilities.IsDependencySet(classSymbol)) return serviceRegistrations;

            // Global opt-out: honor non-generic [SkipRegistration] on any service kind (including BackgroundService)
            var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
                .Any(AttributeTypeChecker.IsNonGenericSkipRegistrationAttribute);
            if (hasNonGenericSkipRegistration)
                return serviceRegistrations;

            var hasRegisterAsAll = classSymbol.GetAttributes().Any(attr =>
                AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));

            var hasRegisterAs = classSymbol.GetAttributes()
                .Any(attr => AttributeTypeChecker.IsRegisterAsAttribute(attr));

            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
            var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
            var hasDependsOnAttribute = classSymbol.GetAttributes().Any(AttributeTypeChecker.IsDependsOnAttribute);
            var hasInjectConfigurationFields =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

            var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);

            // Microsoft.Extensions.DependencyInjection's services.AddHostedService<T>() requires
            // a closed type. Open-generic IHostedService implementers cannot be registered
            // through it because the type parameter cannot be supplied at the registration site.
            // Emitting AddHostedService<Foo<TContext>>() produces invalid C# (CS0246). Skip the
            // implicit registration entirely; consumers must provide a closed subclass to register.
            if (isHostedService && classSymbol.TypeParameters.Length > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.OpenGenericHostedServiceSkipped,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None,
                    classSymbol.Name));
                return serviceRegistrations;
            }

            // Generated registration extensions live in a separate type and namespace. If the
            // hosted service has effective accessibility below 'internal' anywhere in its
            // containing-type chain, services.AddHostedService<TImpl>() emission produces CS0122.
            // When the user has opted in with an explicit lifetime attribute, escalate the
            // severity to Warning — they asked for registration that the emission cannot deliver.
            if (isHostedService && !TypeAnalyzer.IsAccessibleFromGeneratedRegistrationExtension(classSymbol))
            {
                var descriptor = hasLifetimeAttribute
                    ? DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.InaccessibleHostedServiceSkipped,
                        DiagnosticSeverity.Warning)
                    : DiagnosticDescriptors.InaccessibleHostedServiceSkipped;
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None,
                    classSymbol.ToDisplayString(),
                    EffectiveAccessibilityDescription(classSymbol)));
                return serviceRegistrations;
            }

            var hasExplicitServiceIntent = ServiceIntentEvaluator.HasExplicitServiceIntent(
                classSymbol,
                hasInjectFields,
                hasInjectConfigurationFields,
                hasDependsOnAttribute,
                hasConditionalServiceAttribute,
                hasRegisterAsAll,
                hasRegisterAs,
                hasLifetimeAttribute,
                isHostedService,
                classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                classSymbol.Interfaces.Any());

            // Partial-with-interfaces case covered in ServiceIntentEvaluator

            if (!hasExplicitServiceIntent)
                return serviceRegistrations;

            var conditionalServiceAttrs = classSymbol.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
                .ToList();

            if (conditionalServiceAttrs.Any())
            {
                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
                    if (!validationResult.IsValid)
                        foreach (var error in validationResult.Errors)
                            GeneratorDiagnostics.Report(context, "IOC020",
                                "ConditionalService validation error",
                                $"Class '{classSymbol.Name}': {error}");
                }

                var lifetime = hasLifetimeAttribute
                    ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol, implicitLifetime)
                    : implicitLifetime;

                var serviceRegisterAsAllAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
                    AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));
                var hasRegisterAsConditional =
                    classSymbol.GetAttributes().Any(AttributeTypeChecker.IsRegisterAsAttribute);
                var bridgesHostedServiceConditional =
                    isHostedService && (serviceRegisterAsAllAttr != null || hasRegisterAsConditional);

                if (isHostedService && !bridgesHostedServiceConditional) lifetime = "BackgroundService";

                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);
                    if (condition == null) continue;

                    if (serviceRegisterAsAllAttr != null)
                    {
                        var multiInterfaceRegistrations =
                            ServiceRegistrationGenerator.GetMultiInterfaceRegistrationsForConditionalServices(
                                classSymbol, serviceRegisterAsAllAttr, lifetime);
                        foreach (var registration in multiInterfaceRegistrations)
                            serviceRegistrations.Add(new ConditionalServiceRegistration(
                                registration.ClassSymbol,
                                registration.InterfaceSymbol,
                                registration.Lifetime,
                                condition,
                                registration.UseSharedInstance,
                                registration.HasConfigurationInjection));
                    }
                    else
                    {
                        var hasConfigInjection =
                            ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                        var useSharedInstance = hasConfigInjection;
                        serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                            condition, useSharedInstance, hasConfigInjection));

                        if (lifetime != "BackgroundService")
                        {
                            var allInterfaces = GetInterfacesForRegistration(classSymbol, context.ReportDiagnostic);
                            foreach (var interfaceSymbol in allInterfaces)
                                serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol,
                                    interfaceSymbol,
                                    lifetime, condition, useSharedInstance, hasConfigInjection));
                        }
                    }

                    if (bridgesHostedServiceConditional)
                    {
                        var hostedServiceSymbol = TryGetIHostedServiceSymbol(classSymbol);
                        if (hostedServiceSymbol != null)
                            serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol,
                                hostedServiceSymbol, lifetime, condition, true, false));
                    }
                }

                return serviceRegistrations;
            }

            var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));

            var hasInjectConfigurationOnly =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

            var hasConditionalAttributes = classSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            if (hasConditionalAttributes) return serviceRegistrations;

            var lifetimeValue = hasLifetimeAttribute
                ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol, implicitLifetime)
                : implicitLifetime;

            var registerAsAttributesForBridge = classSymbol.GetAttributes()
                .Where(AttributeTypeChecker.IsRegisterAsAttribute)
                .ToList();
            var bridgesHostedService =
                isHostedService && (registerAsAllAttribute != null || registerAsAttributesForBridge.Any());

            // When a hosted service explicitly opts into companion-interface registration via
            // [RegisterAs<T>] or [RegisterAsAll], keep the user's declared lifetime so the
            // concrete class is registered once and IHostedService bridges to it via factory
            // alongside the other interface registrations. Without [RegisterAs*], preserve the
            // implicit-hosted-service shape (services.AddHostedService<T>()).
            if (isHostedService && !bridgesHostedService) lifetimeValue = "BackgroundService";

            var hasConfig = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

            if (registerAsAllAttribute != null)
            {
                // Use non-conditional multi-interface path for regular RegisterAsAll
                var multiRegs = ServiceRegistrationGenerator.GetMultiInterfaceRegistrations(
                    classSymbol, registerAsAllAttribute, lifetimeValue);
                foreach (var reg in multiRegs)
                    serviceRegistrations.Add(reg);
            }
            else
            {
                if (registerAsAttributesForBridge.Any())
                {
                    // Route through diagnostic-aware helper to emit IOC029/IOC030/IOC031 and correct sharing behavior
                    foreach (var registerAsAttr in registerAsAttributesForBridge)
                    {
                        var regs = GetRegisterAsRegistrationsWithSourceContext(classSymbol, registerAsAttr,
                            lifetimeValue, context);
                        serviceRegistrations.AddRange(regs);
                    }
                }
                else
                {
                    // Default behavior when no RegisterAs attributes are present: concrete + all interfaces
                    serviceRegistrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetimeValue, false,
                        hasConfig));
                    var interfaces = GetInterfacesForRegistration(classSymbol, context.ReportDiagnostic);
                    foreach (var @interface in interfaces)
                        serviceRegistrations.Add(new ServiceRegistration(classSymbol, @interface, lifetimeValue,
                            false, hasConfig));
                }
            }

            if (bridgesHostedService)
            {
                var hostedServiceSymbol = TryGetIHostedServiceSymbol(classSymbol);
                if (hostedServiceSymbol != null)
                    serviceRegistrations.Add(new ServiceRegistration(classSymbol, hostedServiceSymbol, lifetimeValue,
                        true, false));
            }
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC998", "Service registration processing error", ex.Message);
        }

        return serviceRegistrations;
    }
}
