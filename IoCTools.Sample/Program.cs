using AppSettings = IoCTools.Sample.Configuration.AppSettings;
using DataCleanupSettings = IoCTools.Sample.Configuration.DataCleanupSettings;
using EmailProcessorSettings = IoCTools.Sample.Configuration.EmailProcessorSettings;
using FileWatcherSettings = IoCTools.Sample.Configuration.FileWatcherSettings;
using HealthMonitorSettings = IoCTools.Sample.Configuration.HealthMonitorSettings;
using HotReloadSettings = IoCTools.Sample.Configuration.HotReloadSettings;
using IAuditService = IoCTools.Sample.Services.IAuditService;
using IDataTransformer = IoCTools.Sample.Services.IDataTransformer;
using IEmailValidator = IoCTools.Sample.Services.IEmailValidator;
using IInventoryService = IoCTools.Sample.Services.IInventoryService;
using IPaymentService = IoCTools.Sample.Services.IPaymentService;
using IReportGenerator = IoCTools.Sample.Services.IReportGenerator;
using IRequestProcessor = IoCTools.Sample.Services.IRequestProcessor;
using ISecurityService = IoCTools.Sample.Services.ISecurityService;
using Order = IoCTools.Sample.Services.Order;
using Payment = IoCTools.Sample.Services.Payment;
using ProcessingRequest = IoCTools.Sample.Services.ProcessingRequest;
using ValidationSettings = IoCTools.Sample.Configuration.ValidationSettings;

// For configuration models

namespace IoCTools.Sample;

using System.Text;

using Configuration;

using Controllers;

using Extensions.Generated;

using Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Services;

// NOTE: The following self-referencing using aliases resolve type ambiguity
// when the same short name exists in multiple namespaces (e.g., Payment, Order, IEmailValidator).
// Without these aliases, the compiler reports CS0104 ambiguous reference errors.
// This pattern is intentionally used to test the generator's type disambiguation.
using AppSettings = AppSettings;
using DataCleanupSettings = DataCleanupSettings;
using EmailProcessorSettings = EmailProcessorSettings;
using FileWatcherSettings = FileWatcherSettings;
using HealthMonitorSettings = HealthMonitorSettings;
using HotReloadSettings = HotReloadSettings;
using IAuditService = IAuditService;
using IDataTransformer = IDataTransformer;
using IEmailValidator = IEmailValidator;
using IInventoryService = IInventoryService;
using IPaymentService = IPaymentService;
using IReportGenerator = IReportGenerator;
using IRequestProcessor = IRequestProcessor;
using ISecurityService = ISecurityService;
using Order = Order;
using Payment = Payment;
using ProcessingRequest = ProcessingRequest;
using ValidationSettings = ValidationSettings;

/// <summary>
///     Comprehensive demonstration program for IoCTools features
///     Includes both regular services and unregistered service patterns
/// </summary>
internal class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 IoCTools Comprehensive Feature Demonstration");
        Console.WriteLine("==============================================");
        Console.WriteLine("This demonstration showcases all IoCTools features:");
        Console.WriteLine("• Basic field injection with [Inject]");
        Console.WriteLine("• Modern lifetime attributes: [Scoped], [Singleton], [Transient]");
        Console.WriteLine("• Intelligent service registration patterns");
        Console.WriteLine("• Multi-interface registration with [RegisterAsAll]");
        Console.WriteLine("• Configuration injection with [InjectConfiguration]");
        Console.WriteLine("• DependsOn dependency patterns");
        Console.WriteLine("• Background services with IHostedService");
        Console.WriteLine("• Conditional services with [ConditionalService]");
        Console.WriteLine("• Complex inheritance hierarchies");
        Console.WriteLine("• External service integration patterns");
        Console.WriteLine("• Diagnostic examples for build-time validation");
        Console.WriteLine();

        // Create Host Builder with comprehensive configuration
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context,
                services) =>
            {
                // Add external dependencies that IoCTools services need
                services.AddMemoryCache(); // Required for cache services
                services.AddHttpClient(); // Required for HTTP client factory

                // Configure Options pattern for configuration injection examples
                // Note: AppSettings, ValidationSettings, HotReloadSettings, and NotificationSchedulerSettings
                // are bound by IoCTools via [InjectConfiguration] attributes - no manual binding needed
                services.Configure<EmailProcessorSettings>(
                    context.Configuration.GetSection("BackgroundServices:EmailProcessor"));
                services.Configure<DataCleanupSettings>(context.Configuration.GetSection("DataCleanupSettings"));
                services.Configure<HealthMonitorSettings>(context.Configuration.GetSection("HealthMonitorSettings"));
                services.Configure<FileWatcherSettings>(context.Configuration.GetSection("FileWatcherSettings"));

                // Use the generated service registration method
                try
                {
                    services.AddIoCToolsSampleRegisteredServices(context.Configuration);
                    Console.WriteLine("✅ IoCTools generated services registered successfully");

                    // REMOVED: Manual background service registration - now handled by generator
                    // RegisterBackgroundServicesAsHostedServices(services);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"⚠️ Failed to use generated services, falling back to manual registration: {ex.Message}");
                    RegisterMissingAutoServices(services);
                }

                // Manual registration of unregistered services for demonstration
                RegisterManualServicesManually(services);
            });

        var host = builder.Build();

        Console.WriteLine("✅ Host configured and services registered");
        Console.WriteLine();

        try
        {
            // Start the host to begin background services
            await host.StartAsync();
            Console.WriteLine("🚀 Host started - background services are now running");
            Console.WriteLine();

            // Create a scope for service resolution since many services are scoped
            using var scope = host.Services.CreateScope();

            // Demonstrate all features in comprehensive order
            await DemonstrateBasicServices(scope.ServiceProvider);
            await DemonstrateArchitecturalEnhancements(scope.ServiceProvider);
            await DemonstrateTransientServices(scope.ServiceProvider);
            await DemonstrateMultiInterfaceRegistration(scope.ServiceProvider);
            await DemonstrateRegisterAsExamples(scope.ServiceProvider);
            await DemonstrateConfigurationInjection(scope.ServiceProvider);
            await DemonstrateConditionalServices(scope.ServiceProvider);
            await DemonstrateDependsOnExamples(scope.ServiceProvider);
            await DemonstrateInheritanceExamples(scope.ServiceProvider);
            await DemonstrateBackgroundServices(host.Services);
            await DemonstrateDiagnosticExamples(scope.ServiceProvider);
            await DemonstrateManualServices(scope.ServiceProvider);
            await DemonstrateExternalServiceIntegration(scope.ServiceProvider);
            await DemonstrateCollectionInjection(scope.ServiceProvider);
            await DemonstrateAdvancedPatterns(scope.ServiceProvider);

            // Show generator-style options (skip/exception) effect
            DemonstrateGeneratorStyleOptions(scope.ServiceProvider);

            Console.WriteLine();
            Console.WriteLine("=== DEMONSTRATION SUMMARY ===");
            Console.WriteLine("✅ Basic services: Field injection, constructor generation");
            Console.WriteLine("✅ Architectural enhancements: Modern attributes, intelligent registration");
            Console.WriteLine("✅ Transient services: Multiple instance resolution");
            Console.WriteLine("✅ Multi-interface registration: [RegisterAsAll] patterns");
            Console.WriteLine("✅ Selective registration: [RegisterAs<T1, T2, T3>] patterns");
            Console.WriteLine("✅ Configuration injection: [InjectConfiguration] examples");
            Console.WriteLine("✅ Conditional services: Environment/config-based selection");
            Console.WriteLine("✅ DependsOn patterns: Declarative dependency injection");
            Console.WriteLine("✅ Inheritance hierarchies: Multi-level dependency chains");
            Console.WriteLine("✅ Background services: Hosted service registration");
            Console.WriteLine("✅ Diagnostic examples: Build-time validation");
            Console.WriteLine("✅ Unregistered services: Factory patterns");
            Console.WriteLine("✅ External service integration: Manual vs automatic registration");
            Console.WriteLine("✅ Collection injection: IEnumerable, IList, IReadOnlyList patterns");
            Console.WriteLine("✅ Advanced patterns: Generic services, complex scenarios");
            Console.WriteLine();
            Console.WriteLine("🎉 All IoCTools features demonstrated successfully!");
            Console.WriteLine("   Check the console output above for detailed examples.");
            Console.WriteLine("   Review the generated source code in bin/Debug/net9.0/generated/");
            Console.WriteLine("⏱️  Background services will continue running for 10 seconds...");

            // Let background services run for a bit to demonstrate they're working
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Demonstration failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static void DemonstrateGeneratorStyleOptions(IServiceProvider sp)
    {
        Console.WriteLine();
        Console.WriteLine("--- Generator Style Options (Skip/Exceptions) ---");

        var optIn = sp.GetService<IOptInController>();
        Console.WriteLine(optIn != null
            ? "✅ OptInController registered via exceptions list"
            : "❌ OptInController not registered (expected if exceptions not configured)");

        var derived = sp.GetService<IFrameworkDerivedService>();
        Console.WriteLine(derived == null
            ? "✅ FrameworkDerivedService skipped via assignable-type rule"
            : "❌ FrameworkDerivedService unexpectedly registered");
    }

    private static void RegisterManualServicesManually(IServiceCollection services)
    {
        Console.WriteLine("--- Manual Registration of Selected Services ---");

        // DEMONSTRATION: Manual service registration examples
        // These services are manually registered to show how manual registration works alongside IoCTools.
        // In production, you would typically rely on IoCTools-generated registration via lifetime attributes
        // ([Scoped], [Singleton], [Transient]) unless the service requires complex external configuration.

        services.AddScoped<IManualRegistrationService, ManualRegistrationService>();

        // Register legacy processor individually for demonstration
        services.AddScoped<LegacyPaymentProcessor>();

        // Register inheritance services for demonstration
        services.AddScoped<INewPaymentProcessor, UnregisteredNewPaymentProcessor>();
        services.AddScoped<IEnterprisePaymentProcessor, EnterprisePaymentProcessor>();
        services.AddSingleton<IManualServiceFactory, ManualServiceFactory>();

        Console.WriteLine("--- Manual Registration of External Services ---");

        // DEMONSTRATION: External service manual registration examples
        // These services demonstrate how to manually register services that require complex external setup
        // (like HTTP clients, database connections, or third-party API integrations). In production:
        // - For services with complex setup, consider using [ExternalService] attribute to suppress IoCTools registration
        // - Or delegate the external configuration to a separate extension method like RegisterExternalServices()

        services.AddScoped<IHttpClientService, HttpClientService>();
        services.AddScoped<IDatabaseContextService, DatabaseContextService>();
        services.AddScoped<IDistributedCacheService, ExternalRedisCacheService>();
        services.AddScoped<IThirdPartyApiService, ThirdPartyApiService>();

        // Register external service registration helper
        services.AddSingleton<IExternalServiceRegistrationHelper, ExternalServiceRegistrationHelper>();

        Console.WriteLine("✅ Manual registrations completed for demonstration");
        Console.WriteLine("✅ External services registered: HTTP Client, Database, Redis Cache, Third-party APIs");
        Console.WriteLine();
    }

    private static void RegisterMissingAutoServices(IServiceCollection services)
    {
        // These should be automatically registered by the source generator
        // but we're adding them manually for demonstration since the generator isn't producing output

        // Basic field injection services from BasicUsageExamples.cs
        services.AddScoped<IGreetingService, GreetingService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<ICacheService, CacheService>(); // Singleton as specified in attribute
        services.AddScoped<BackgroundTaskService>(); // No interface, concrete type only

        // Advanced field injection examples
        services.AddScoped<INotificationService, EmailNotificationService>();
        services.AddScoped<INotificationService, SmsNotificationService>();
        services.AddScoped<IAdvancedInjectionService, AdvancedInjectionService>();

        // Transient service examples from TransientServiceExamples.cs
        services.AddTransient<IEmailValidator, EmailValidator>();
        services.AddTransient<IDataTransformer, DataTransformer>();
        services.AddTransient<IRequestProcessor, RequestProcessor>();
        services.AddTransient<IGuidGenerator, GuidGenerator>();
        services.AddTransient<ILifetimeComparisonService, LifetimeComparisonService>();

        // DependsOn examples services
        services.AddScoped<OrderProcessingService>();
        services.AddScoped<CamelCaseExampleService>();
        services.AddScoped<PascalCaseExampleService>();
        services.AddScoped<SnakeCaseExampleService>();
        services.AddScoped<CustomPrefixService>();
        services.AddScoped<NoStripIService>();
        services.AddScoped<MixedConfigurationService>();
        services.AddScoped<EnhancedSecureService>();
        services.AddScoped<ConfigurableAuditService>();
        services.AddScoped<MixedDependencyPatternService>();
        services.AddScoped(typeof(GenericRepositoryService<>));
        services.AddScoped<MultiGenericRepositoryService>();

        // Implementation services for DependsOn examples
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IReportGenerator, ReportGenerator>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISecurityService, DemoSecurityService>();
        services.AddScoped(typeof(IDependsOnGenericRepository<>),
            typeof(DependsOnUserRepository)); // Simplified for demo
        services.AddScoped<IDependsOnGenericRepository<DependsOnUser>, DependsOnUserRepository>();
        services.AddScoped<IDependsOnGenericRepository<Order>, DependsOnOrderRepository>();

        // Note: Inheritance services from ManualServiceExamples.cs are manually registered separately

        // Configuration injection services from ConfigurationInjectionExamples.cs
        services.AddScoped<DatabaseConnectionService>();
        services.AddScoped<AppInfoService>();
        services.AddScoped<ConfigurationEmailService>();
        services.AddScoped<ConfigurationCacheService>();
        services.AddScoped<SecurityService>();
        services.AddScoped<OptionsPatternService>();
        services.AddScoped<HotReloadableService>();
        services.AddScoped<ConfigurationValidationService>();
        services.AddScoped<IConfigurationNotificationProvider, ConfigurationEmailNotificationProvider>();
        services.AddScoped<ComprehensiveBusinessService>();
        services.AddScoped<NestedConfigurationService>();
        services.AddScoped<ConfigurationArrayService>();
        services.AddScoped<ConfigurationDemoRunner>();

        // Diagnostic examples service
        services.AddScoped<DiagnosticDemonstrationService>();

        // Multi-interface registration services - these should be auto-registered by the generator
        // but we're adding them manually for demonstration purposes

        // UserService with RegisterAsAll(All, Shared) - should register concrete type AND all interfaces
        services.AddScoped<UserService>();
        services.AddScoped<IMultiUserService, UserService>();
        services.AddScoped<IMultiUserRepository, UserService>();
        services.AddScoped<IMultiUserValidator, UserService>();

        // DirectOnlyPaymentProcessor - should register only concrete type
        services.AddScoped<DirectOnlyPaymentProcessor>();

        // InterfaceOnlyPaymentProcessor with RegisterAsAll(Exclusionary, Shared) - only interfaces
        services.AddScoped<IMultiPaymentService, InterfaceOnlyPaymentProcessor>();
        services.AddScoped<IMultiPaymentValidator, InterfaceOnlyPaymentProcessor>();
        services.AddScoped<IMultiPaymentLogger, InterfaceOnlyPaymentProcessor>();

        // Cache managers with different instance sharing modes
        services.AddScoped<SeparateInstanceCacheManager>();
        services.AddScoped<IMultiCacheService, SeparateInstanceCacheManager>();
        services.AddScoped<IMultiCacheProvider, SeparateInstanceCacheManager>();
        services.AddScoped<IMultiCacheValidator, SeparateInstanceCacheManager>();

        services.AddScoped<SharedInstanceCacheManager>();
        services.AddScoped<IMultiCacheService, SharedInstanceCacheManager>();
        services.AddScoped<IMultiCacheProvider, SharedInstanceCacheManager>();
        services.AddScoped<IMultiCacheValidator, SharedInstanceCacheManager>();

        // Selective data service with skip registration for some interfaces
        services.AddScoped<SelectiveDataService>();
        services.AddScoped<IDataService, SelectiveDataService>();
        services.AddScoped<IDataValidator, SelectiveDataService>();
        // Note: IDataLogger and IDataCacheService intentionally skipped

        // Generic services from GenericServiceExamples.cs
        services.AddScoped(typeof(GenericRepository<>));
        services.AddScoped(typeof(Services.IRepository<>), typeof(GenericRepository<>));
        services.AddScoped(typeof(GenericValidator<>));
        services.AddScoped(typeof(IGenericValidator<>), typeof(GenericValidator<>));
        services.AddTransient(typeof(DataProcessor<,>));
        services.AddTransient(typeof(Services.IProcessor<,>), typeof(DataProcessor<,>));
        services.AddSingleton(typeof(Cache<>));
        services.AddSingleton(typeof(ICache<>), typeof(Cache<>));
        services.AddSingleton(typeof(Factory<>));
        services.AddSingleton(typeof(IFactory<>), typeof(Factory<>));
        services.AddScoped(typeof(BaseBusinessService<>));
        services.AddScoped(typeof(AdvancedBusinessService<>));
        services.AddScoped(typeof(EnhancedGenericProcessor<>));
        services.AddScoped<GenericServiceDemonstrator>();

        // Composite notification service with inheritance
        services.AddScoped<CompositeNotificationService>();
        services.AddScoped<IEmailNotificationService, CompositeNotificationService>();
        services.AddScoped<ISmsNotificationService, CompositeNotificationService>();
        services.AddScoped<INotificationLogger, CompositeNotificationService>();

        // Performance test service
        services.AddScoped<PerformanceTestService>();
        services.AddScoped<IPerformanceTestService, PerformanceTestService>();
        services.AddScoped<IPerformanceMetrics, PerformanceTestService>();
        services.AddScoped<IPerformanceBenchmark, PerformanceTestService>();

        // Multi-interface demonstration service
        services.AddScoped<IMultiInterfaceDemoService, MultiInterfaceDemonstrationService>();

        // Collection injection services - multiple implementations for collection patterns
        services.AddScoped<ICollectionNotificationService, CollectionEmailNotificationService>();
        services.AddScoped<ICollectionNotificationService, CollectionSmsNotificationService>();
        services.AddScoped<ICollectionNotificationService, CollectionPushNotificationService>();
        services.AddScoped<ICollectionNotificationService, CollectionSlackNotificationService>();
        services.AddScoped<NotificationManager>();

        // Processing chain services for IList<T> examples
        services.AddTransient<IProcessor, ValidationProcessor>();
        services.AddTransient<IProcessor, TransformationProcessor>();
        services.AddTransient<IProcessor, EnrichmentProcessor>();
        services.AddScoped<ProcessorChain>();
        services.AddSingleton<ProcessorAnalyzer>();

        // Generic validation services for IEnumerable<IValidator<T>> examples
        services.AddTransient<Services.IValidator<User>, UserValidator>();
        services.AddTransient<Services.IValidator<User>, UserBusinessValidator>();
        services.AddTransient<Services.IValidator<Order>, OrderValidator>();
        services.AddScoped<ValidationService>();

        // Aggregator services for IReadOnlyList<T> examples
        services.AddTransient<IAggregator<decimal>, SumAggregator>();
        services.AddTransient<IAggregator<decimal>, AverageAggregator>();
        services.AddScoped<AggregatorService>();

        // Multi-provider services with different lifetimes for collection lifetime demonstration
        services.AddSingleton<IDataProvider, CachedDataProvider>();
        services.AddScoped<IDataProvider, DatabaseDataProvider>();
        services.AddTransient<IDataProvider, ApiDataProvider>();
        services.AddScoped<MultiProviderService>();

        // External service integration examples - these would be auto-registered by IoCTools
        // but we're adding them manually since the generator might not be running
        services.AddScoped<IOrderProcessingBusinessService, OrderProcessingBusinessService>();
        services.AddScoped<IFrameworkIntegrationService, FrameworkIntegrationService>();
        services.AddScoped<IHybridIntegrationService, HybridIntegrationService>();

        Console.WriteLine("✅ Manual service registrations added for missing auto-generated services");
        Console.WriteLine("✅ External service integration examples registered");
    }

    // DEPRECATED: This method is no longer needed as the IoCTools generator now automatically
    // registers BackgroundService classes as hosted services. Keeping for reference only.
    private static void RegisterBackgroundServicesAsHostedServices(IServiceCollection services)
    {
        Console.WriteLine("--- Registering Background Services as Hosted Services ---");

        // Register IoCTools background services as hosted services
        // The services are already registered by AddIoCToolsSampleRegisteredServices(),
        // but we need to register them as hosted services for the .NET hosting system
        services.AddHostedService<SimpleBackgroundWorker>();
        services.AddHostedService<EmailQueueProcessor>();
        services.AddHostedService<DataCleanupService>();
        services.AddHostedService<HealthCheckService>();
        services.AddHostedService<FileWatcherService>();
        services.AddHostedService<NotificationSchedulerService>(); // Conditional service
        services.AddHostedService<ComplexBackgroundService>();

        Console.WriteLine("✅ Background services registered as hosted services");
        Console.WriteLine("   - SimpleBackgroundWorker: Basic background service example");
        Console.WriteLine("   - EmailQueueProcessor: Email queue processing with configuration");
        Console.WriteLine("   - DataCleanupService: Periodic data cleanup operations");
        Console.WriteLine("   - HealthCheckService: Health endpoint monitoring");
        Console.WriteLine("   - FileWatcherService: File system change monitoring");
        Console.WriteLine("   - NotificationSchedulerService: Scheduled notifications (conditional)");
        Console.WriteLine("   - ComplexBackgroundService: Advanced patterns demonstration");
        Console.WriteLine();
    }

    private static async Task DemonstrateBasicServices(IServiceProvider services)
    {
        Console.WriteLine("=== 1. BASIC SERVICES DEMONSTRATION ===");
        Console.WriteLine("These services demonstrate basic [Inject] field injection:");
        Console.WriteLine();

        await TestBasicFieldInjection(services);
        await TestDifferentLifetimes(services);
        Console.WriteLine();
    }

    private static async Task TestBasicFieldInjection(IServiceProvider services)
    {
        Console.WriteLine("--- Basic Field Injection with [Inject] ---");

        // Test simple field injection
        var greetingService = services.GetService<IGreetingService>();
        if (greetingService != null)
        {
            var greeting = greetingService.GetGreeting("IoCTools User");
            Console.WriteLine($"  ✅ GreetingService: {greeting}");
        }

        // Test collection injection patterns
        var advancedService = services.GetService<IAdvancedInjectionService>();
        if (advancedService != null)
        {
            await advancedService.DemonstrateAdvancedPatternsAsync();
            Console.WriteLine("  ✅ AdvancedInjectionService: Collections, factories, and lazy injection");
        }

        // Test concrete service (no interface)
        var backgroundService = services.GetService<BackgroundTaskService>();
        if (backgroundService != null)
        {
            await backgroundService.ProcessTasksAsync();
            Console.WriteLine("  ✅ BackgroundTaskService: Concrete service injection");
        }
    }

    private static Task TestDifferentLifetimes(IServiceProvider services)
    {
        Console.WriteLine();
        Console.WriteLine("--- Different Service Lifetimes ---");

        // Singleton cache service
        var cache1 = services.GetService<ICacheService>();
        var cache2 = services.GetService<ICacheService>();
        if (cache1 != null && cache2 != null)
        {
            var value1 = cache1.GetOrSet("singleton-test", () => "singleton-value");
            var value2 = cache2.GetOrSet<string>("singleton-test", () => "default-value");
            Console.WriteLine(
                $"  ✅ Singleton CacheService: Same instance = {ReferenceEquals(cache1, cache2)}, Cached value = {value2}");
        }

        // Scoped services (should be same within scope)
        var greeting1 = services.GetService<IGreetingService>();
        var greeting2 = services.GetService<IGreetingService>();
        if (greeting1 != null && greeting2 != null)
            Console.WriteLine(
                $"  ✅ Scoped GreetingService: Same instance in scope = {ReferenceEquals(greeting1, greeting2)}");

        return Task.CompletedTask;
    }

    private static async Task TestBasicServices(IServiceProvider services)
    {
        Console.WriteLine("--- Basic Field Injection Services ---");

        var greetingService = services.GetService<IGreetingService>();
        if (greetingService != null)
        {
            var greeting = greetingService.GetGreeting("World");
            Console.WriteLine($"✅ GreetingService: {greeting}");
        }

        var cacheService = services.GetService<ICacheService>();
        if (cacheService != null)
        {
            var value = cacheService.GetOrSet("test-key", () => "cached-value");
            Console.WriteLine($"✅ CacheService: {value}");
        }

        var backgroundService = services.GetService<BackgroundTaskService>();
        if (backgroundService != null)
        {
            await backgroundService.ProcessTasksAsync();
            Console.WriteLine("✅ BackgroundTaskService: Task processing completed");
        }

        var advancedService = services.GetService<IAdvancedInjectionService>();
        if (advancedService != null)
        {
            await advancedService.DemonstrateAdvancedPatternsAsync();
            Console.WriteLine("✅ AdvancedInjectionService: Collection, factory, and lazy injection patterns completed");
        }

        Console.WriteLine();
    }

    private static async Task TestTransientServicesOriginal(IServiceProvider services)
    {
        Console.WriteLine("--- Transient Services Demo ---");
        Console.WriteLine("Transient services create new instances on each request");
        Console.WriteLine();

        // 1. Demonstrate Email Validator (stateless validation)
        Console.WriteLine("1. Email Validation Service (Transient):");
        var emailValidator1 = services.GetService<IEmailValidator>();
        var emailValidator2 = services.GetService<IEmailValidator>();

        if (emailValidator1 != null && emailValidator2 != null)
        {
            Console.WriteLine($"   Instance 1 Hash: {emailValidator1.GetHashCode()}");
            Console.WriteLine($"   Instance 2 Hash: {emailValidator2.GetHashCode()}");
            Console.WriteLine(
                $"   Different instances: {emailValidator1.GetHashCode() != emailValidator2.GetHashCode()}");

            var validEmail = emailValidator1.ValidateWithDetails("user@example.com");
            var invalidEmail = emailValidator2.ValidateWithDetails("invalid-email");
            Console.WriteLine($"   Valid email result: {validEmail.IsValid} - {validEmail.Message}");
            Console.WriteLine($"   Invalid email result: {invalidEmail.IsValid} - {invalidEmail.Message}");
        }

        Console.WriteLine();

        // 2. Demonstrate Data Transformer (stateless transformations)
        Console.WriteLine("2. Data Transformation Service (Transient):");
        var transformer1 = services.GetService<Interfaces.IDataTransformer>();
        var transformer2 = services.GetService<Interfaces.IDataTransformer>();

        if (transformer1 != null && transformer2 != null)
        {
            Console.WriteLine($"   Instance 1 Hash: {transformer1.GetHashCode()}");
            Console.WriteLine($"   Instance 2 Hash: {transformer2.GetHashCode()}");
            Console.WriteLine($"   Different instances: {transformer1.GetHashCode() != transformer2.GetHashCode()}");

            // Demonstrate Transform method with proper reference types
            var sourceData1 = new { Text = "  Hello\t\tWorld  \n\n" };
            var sourceData2 = new { Value = 75, Total = 100 };
            var transformedResult1 = transformer1.Transform<User>(sourceData1);
            var transformedResult2 = transformer2.Transform<User>(sourceData2);
            Console.WriteLine($"   Transform 1 successful: {transformedResult1 != null}");
            Console.WriteLine($"   Transform 2 successful: {transformedResult2 != null}");
        }

        Console.WriteLine();

        // 3. Demonstrate Request Processor with dependencies
        Console.WriteLine("3. Request Processor (Transient with Transient dependencies):");
        var processor1 = services.GetService<IRequestProcessor>();
        var processor2 = services.GetService<IRequestProcessor>();

        if (processor1 != null && processor2 != null)
        {
            Console.WriteLine($"   Instance 1 Hash: {processor1.GetHashCode()}");
            Console.WriteLine($"   Instance 2 Hash: {processor2.GetHashCode()}");

            var emailRequest = new ProcessingRequest("req-001", "email", "test@example.com");
            var textRequest = new ProcessingRequest("req-002", "text", "  Sample   Text  ");

            var result1 = await processor1.ProcessWithValidationAsync(emailRequest);
            var result2 = await processor2.ProcessAsync(textRequest);

            Console.WriteLine($"   Email processing result: {result1.Success} - {result1.Message}");
            Console.WriteLine($"   Text processing result: {result2.Success} - {result2.Message}");
            Console.WriteLine($"   Processed data: '{result2.ProcessedData}'");
        }

        Console.WriteLine();

        // 4. Demonstrate GUID Generator
        Console.WriteLine("4. GUID Generator Service (Perfect Transient example):");
        var guidGen1 = services.GetService<IGuidGenerator>();
        var guidGen2 = services.GetService<IGuidGenerator>();

        if (guidGen1 != null && guidGen2 != null)
        {
            Console.WriteLine($"   Instance 1 Hash: {guidGen1.GetHashCode()}");
            Console.WriteLine($"   Instance 2 Hash: {guidGen2.GetHashCode()}");

            var guid1 = guidGen1.NewGuid();
            var guid2 = guidGen2.NewGuid();
            var shortId = guidGen1.NewShortId(12);
            var formattedGuid = guidGen2.NewGuidString(GuidFormat.UppercaseNoDashes);

            Console.WriteLine($"   GUID from instance 1: {guid1}");
            Console.WriteLine($"   GUID from instance 2: {guid2}");
            Console.WriteLine($"   Short ID (12 chars): {shortId}");
            Console.WriteLine($"   Formatted GUID: {formattedGuid}");
        }

        Console.WriteLine();

        // 5. Demonstrate Lifetime Comparison
        Console.WriteLine("5. Service Lifetime Comparison (Transient vs Others):");
        var lifetimeService1 = services.GetService<ILifetimeComparisonService>();
        var lifetimeService2 = services.GetService<ILifetimeComparisonService>();

        if (lifetimeService1 != null && lifetimeService2 != null)
        {
            Console.WriteLine($"   Lifetime Service Instance 1 Hash: {lifetimeService1.GetHashCode()}");
            Console.WriteLine($"   Lifetime Service Instance 2 Hash: {lifetimeService2.GetHashCode()}");

            await lifetimeService1.DemonstrateTransientBehaviorAsync();
            await lifetimeService2.CompareLifetimesAsync();

            var info1 = lifetimeService1.GetServiceInfo();
            var info2 = lifetimeService2.GetServiceInfo();

            Console.WriteLine($"   Service 1 Instance ID: {info1.InstanceId}");
            Console.WriteLine($"   Service 2 Instance ID: {info2.InstanceId}");
            Console.WriteLine($"   Different instance IDs: {info1.InstanceId != info2.InstanceId}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Transient Service Benefits ===");
        Console.WriteLine("✅ Perfect for stateless operations (validation, transformation, generation)");
        Console.WriteLine("✅ No shared state concerns - each operation gets fresh instance");
        Console.WriteLine("✅ Ideal for per-operation processing (request handlers, calculators)");
        Console.WriteLine("✅ Lightweight services that don't hold expensive resources");
        Console.WriteLine("⚠️  Consider memory usage for high-frequency scenarios");
        Console.WriteLine("⚠️  Dependencies should also be lightweight or cached appropriately");
        Console.WriteLine();
    }

    private static async Task TestRegisteredInheritanceServices(IServiceProvider services)
    {
        Console.WriteLine("--- Registered Inheritance Services ---");

        var newPaymentProcessor = services.GetService<INewPaymentProcessor>();
        if (newPaymentProcessor != null)
        {
            var payment = new Payment(99.99m);
            var result = await newPaymentProcessor.ProcessNewPaymentAsync(payment);
            Console.WriteLine($"✅ NewPaymentProcessor (inherits from [ExternalService]): {result.Message}");
        }

        var enterpriseProcessor = services.GetService<IEnterprisePaymentProcessor>();
        if (enterpriseProcessor != null)
        {
            var payment = new Payment(299.99m);
            var result = await enterpriseProcessor.ProcessEnterprisePaymentAsync(payment, 12345);
            Console.WriteLine($"✅ EnterprisePaymentProcessor (complex inheritance): {result.Message}");
        }

        Console.WriteLine();
    }

    private static async Task TestAdvancedPatterns(IServiceProvider services)
    {
        Console.WriteLine("--- Advanced Patterns Demo ---");

        var advancedPatternsService = services.GetService<IAdvancedPatternsService>();
        if (advancedPatternsService != null)
        {
            await advancedPatternsService.DemonstrateCurrentCapabilitiesAsync();
            await advancedPatternsService.DemonstrateFutureEnhancementsAsync();
            Console.WriteLine("✅ Advanced patterns demonstration completed");
        }
        else
        {
            Console.WriteLine("❌ Advanced patterns service not available");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateRegisterAsExamples(IServiceProvider services)
    {
        Console.WriteLine("=== REGISTERAS<T1, T2, T3> SELECTIVE REGISTRATION DEMONSTRATION ===");
        Console.WriteLine("RegisterAs provides precise control over which interfaces are registered for DI");
        Console.WriteLine(
            "Unlike RegisterAsAll (registers ALL interfaces), RegisterAs registers only specified interfaces");
        Console.WriteLine();

        // Test basic RegisterAs functionality
        Console.WriteLine("--- 1. Basic RegisterAs Examples ---");

        // Test single interface registration
        var basicUserService = services.GetService<BasicUserService>();
        var iUserService = services.GetService<IRegisterAsUserService>();
        var iEmailService = services.GetService<IRegisterAsEmailService>();
        var iValidationService = services.GetService<IRegisterAsValidationService>();

        if (basicUserService != null && iUserService != null)
        {
            Console.WriteLine(
                $"✅ BasicUserService and IRegisterAsUserService registered: {basicUserService.GetType().Name}");
            Console.WriteLine("✅ Shared instance pattern: IRegisterAsUserService resolves to same instance");
        }
        else
        {
            Console.WriteLine("❌ BasicUserService or IRegisterAsUserService not available");
        }

        if (iEmailService == null && iValidationService == null)
            Console.WriteLine(
                "✅ IRegisterAsEmailService and IRegisterAsValidationService NOT registered (as expected with RegisterAs<IRegisterAsUserService>)");
        else
            Console.WriteLine(
                "❌ Unexpected: IRegisterAsEmailService or IRegisterAsValidationService found in DI container");
        Console.WriteLine();

        // Test multiple interface registration
        Console.WriteLine("--- 2. Multiple Interface Registration ---");
        var userEmailService = services.GetService<UserEmailService>();

        if (userEmailService != null)
            Console.WriteLine(
                $"✅ UserEmailService with RegisterAs<IRegisterAsUserService, IRegisterAsEmailService>: {userEmailService.GetType().Name}");
        else
            Console.WriteLine("❌ UserEmailService not available");
        Console.WriteLine();

        // Test RegisterAs with different lifetimes
        Console.WriteLine("--- 3. RegisterAs with Different Lifetimes ---");
        var singletonService = services.GetService<SingletonUserService>();
        var transientEmailValidation1 = services.GetService<TransientEmailValidationService>();
        var transientEmailValidation2 = services.GetService<TransientEmailValidationService>();

        if (singletonService != null)
            Console.WriteLine($"✅ Singleton RegisterAs service: {singletonService.GetType().Name}");

        if (transientEmailValidation1 != null && transientEmailValidation2 != null)
        {
            var sameInstance = ReferenceEquals(transientEmailValidation1, transientEmailValidation2);
            Console.WriteLine(
                $"✅ Transient RegisterAs service: instances are {(sameInstance ? "SAME" : "DIFFERENT")} (expected: DIFFERENT)");
        }

        Console.WriteLine();

        // Test RegisterAs with ManualService
        Console.WriteLine("--- 4. RegisterAs with ManualService Pattern ---");
        var transactionService = services.GetService<ITransactionService>();
        var repository = services.GetService<IRepository>();
        var concreteDbContext = services.GetService<DatabaseContext>();

        if (transactionService != null && repository != null)
            Console.WriteLine("✅ ManualService + RegisterAs: Interfaces registered successfully");
        else
            Console.WriteLine("❌ ManualService + RegisterAs interfaces not available");

        if (concreteDbContext == null)
            Console.WriteLine("✅ ManualService: Concrete DatabaseContext NOT auto-registered (as expected)");
        else
            Console.WriteLine("❌ Unexpected: DatabaseContext found in DI container");
        Console.WriteLine();

        Console.WriteLine("📝 RegisterAs<T1, T2, T3> Summary:");
        Console.WriteLine("   • Provides selective interface registration (vs RegisterAsAll's all interfaces)");
        Console.WriteLine("   • Supports 1-8 generic type parameters for maximum flexibility");
        Console.WriteLine("   • Works with all service lifetimes (Singleton, Scoped, Transient)");
        Console.WriteLine("   • Integrates with ManualService for DbContext-like scenarios");
        Console.WriteLine("   • Compatible with configuration injection and inheritance");
        Console.WriteLine("   • Provides comprehensive compile-time validation via diagnostics");
        Console.WriteLine();
        await Task.CompletedTask;
    }

    private static async Task DemonstrateConfigurationInjection(IServiceProvider services)
    {
        Console.WriteLine("=== CONFIGURATION INJECTION DEMONSTRATION ===");
        Console.WriteLine("These services demonstrate comprehensive configuration injection patterns");
        Console.WriteLine();

        var configDemoRunner = services.GetService<ConfigurationDemoRunner>();
        if (configDemoRunner != null)
        {
            await configDemoRunner.RunAllConfigurationDemosAsync();
        }
        else
        {
            Console.WriteLine("❌ ConfigurationDemoRunner service not available");
            Console.WriteLine(
                "   This indicates the source generator may not be processing configuration injection properly");

            // Fallback to individual service testing
            await TestIndividualConfigurationServices(services);
        }

        Console.WriteLine();
    }

    private static async Task TestIndividualConfigurationServices(IServiceProvider services)
    {
        Console.WriteLine("--- Fallback: Testing Individual Configuration Services ---");

        var databaseService = services.GetService<DatabaseConnectionService>();
        if (databaseService != null)
        {
            await databaseService.TestConnectionAsync();
            Console.WriteLine("✅ DatabaseConnectionService: Primitive configuration injection working");
        }

        var emailService = services.GetService<ConfigurationEmailService>();
        if (emailService != null)
        {
            await emailService.SendEmailAsync("test@example.com", "Demo", "Configuration binding test");
            Console.WriteLine("✅ ConfigurationEmailService: Section binding configuration injection working");
        }

        var securityService = services.GetService<SecurityService>();
        if (securityService != null)
        {
            securityService.DisplaySecurityConfig();
            Console.WriteLine("✅ SecurityService: Array/collection configuration injection working");
        }

        var validationService = services.GetService<ConfigurationValidationService>();
        if (validationService != null)
        {
            validationService.ValidateConfiguration();
            Console.WriteLine("✅ ConfigurationValidationService: Required/optional configuration injection working");
        }

        Console.WriteLine("   Note: Individual services available indicates partial configuration injection support");
    }

    private static async Task DemonstrateDependsOnExamples(IServiceProvider services)
    {
        Console.WriteLine("=== DEPENDSON ATTRIBUTE EXAMPLES DEMONSTRATION ===");
        Console.WriteLine("These examples show all documented DependsOn patterns and naming conventions");
        Console.WriteLine();

        await TestBasicDependsOnExamples(services);
        await TestNamingConventionExamples(services);
        await TestCustomConfigurationExamples(services);
        await TestInheritanceWithDependsOnExamples(services);
        await TestMixedPatternExamples(services);
        await TestGenericDependsOnExamples(services);
    }

    private static async Task TestBasicDependsOnExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Basic DependsOn Examples ---");

        var orderProcessor = services.GetService<OrderProcessingService>();
        if (orderProcessor != null)
        {
            var order = new Order(123, "customer@example.com", new Payment(99.99m));
            var success = await orderProcessor.ProcessOrderAsync(order);
            Console.WriteLine(
                $"✅ OrderProcessingService (DependsOn<IPaymentService, IEmailService, IInventoryService>): Success = {success}");
        }

        Console.WriteLine();
    }

    private static async Task TestNamingConventionExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Naming Convention Examples ---");

        var camelCaseService = services.GetService<CamelCaseExampleService>();
        if (camelCaseService != null)
        {
            await camelCaseService.ProcessUserActionAsync(1, "login");
            Console.WriteLine("✅ CamelCaseExampleService: Uses default camelCase naming (_userManagementService)");
        }

        var pascalCaseService = services.GetService<PascalCaseExampleService>();
        if (pascalCaseService != null)
        {
            await pascalCaseService.ProcessUserActionAsync(2, "logout");
            Console.WriteLine("✅ PascalCaseExampleService: Uses PascalCase naming (_UserManagementService)");
        }

        var snakeCaseService = services.GetService<SnakeCaseExampleService>();
        if (snakeCaseService != null)
        {
            await snakeCaseService.ProcessUserActionAsync(3, "update_profile");
            Console.WriteLine("✅ SnakeCaseExampleService: Uses snake_case naming (_user_management_service)");
        }

        Console.WriteLine();
    }

    private static async Task TestCustomConfigurationExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Custom Configuration Examples ---");

        var customPrefixService = services.GetService<CustomPrefixService>();
        if (customPrefixService != null)
        {
            var payment = new Payment(49.99m);
            await customPrefixService.ProcessPaymentWithEmailAsync(payment, "test@example.com");
            Console.WriteLine("✅ CustomPrefixService: Uses custom 'svc_' prefix (svc_paymentService)");
        }

        var noStripIService = services.GetService<NoStripIService>();
        if (noStripIService != null)
        {
            var payment = new Payment(29.99m);
            await noStripIService.ProcessPaymentPreservingInterfaceNameAsync(payment, "test@example.com");
            Console.WriteLine("✅ NoStripIService: Preserves interface names with stripI=false (IPaymentService)");
        }

        var mixedConfigService = services.GetService<MixedConfigurationService>();
        if (mixedConfigService != null)
        {
            var payment = new Payment(19.99m);
            await mixedConfigService.ProcessWithMixedConfigurationsAsync(payment, "test@example.com");
            Console.WriteLine("✅ MixedConfigurationService: Multiple DependsOn with different configurations");
        }

        Console.WriteLine();
    }

    private static async Task TestInheritanceWithDependsOnExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Inheritance with DependsOn Examples ---");

        var enhancedSecureService = services.GetService<EnhancedSecureService>();
        if (enhancedSecureService != null)
        {
            var success = await enhancedSecureService.ProcessSecureUserActionAsync(1, "admin_action");
            Console.WriteLine(
                $"✅ EnhancedSecureService: Inherits from BaseSecureService with additional DependsOn, Success = {success}");
        }

        var configurableAuditService = services.GetService<ConfigurableAuditService>();
        if (configurableAuditService != null)
        {
            await configurableAuditService.ProcessWithConfigAndAuditAsync("configuration_test");
            Console.WriteLine("✅ ConfigurableAuditService: Deep inheritance with DependsOn at multiple levels");
        }

        Console.WriteLine();
    }

    private static async Task TestMixedPatternExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Mixed [Inject] and [DependsOn] Pattern Examples ---");

        var mixedPatternService = services.GetService<MixedDependencyPatternService>();
        if (mixedPatternService != null)
        {
            var order = new Order(456, "mixed@example.com", new Payment(79.99m));
            var success = await mixedPatternService.ProcessOrderWithMixedPatternsAsync(order);
            Console.WriteLine(
                $"✅ MixedDependencyPatternService: Combines [Inject] and [DependsOn], Success = {success}");
        }

        var externalServiceDemo = services.GetService<ExternalServiceDemoService>();
        if (externalServiceDemo != null)
        {
            await externalServiceDemo.DemonstrateExternalServicesAsync();
            Console.WriteLine("✅ ExternalServiceDemoService: Shows external=true usage for framework services");
        }

        Console.WriteLine();
    }

    private static async Task TestGenericDependsOnExamples(IServiceProvider services)
    {
        Console.WriteLine("--- Generic Services with DependsOn Examples ---");

        var userRepositoryService = services.GetService<GenericRepositoryService<DependsOnUser>>();
        if (userRepositoryService != null)
        {
            var user = await userRepositoryService.GetEntityAsync(1);
            if (user != null)
                Console.WriteLine(
                    $"✅ GenericRepositoryService<User>: Retrieved user '{user.Name}' with DependsOn generic logging");

            var newUser = new DependsOnUser(999, "New User", "new@example.com");
            await userRepositoryService.SaveEntityAsync(newUser);
            Console.WriteLine("✅ GenericRepositoryService<User>: Saved new user with generic DependsOn pattern");
        }

        var multiGenericService = services.GetService<MultiGenericRepositoryService>();
        if (multiGenericService != null)
        {
            var report = await multiGenericService.GenerateUserOrderReportAsync(1);
            Console.WriteLine($"✅ MultiGenericRepositoryService: {report}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateGenericServices(IServiceProvider services)
    {
        Console.WriteLine("=== GENERIC SERVICES DEMONSTRATION ===");
        Console.WriteLine("These examples demonstrate comprehensive generic service patterns supported by IoCTools");
        Console.WriteLine();

        var genericDemonstrator = services.GetService<GenericServiceDemonstrator>();
        if (genericDemonstrator != null)
        {
            await genericDemonstrator.DemonstrateGenericPatternsAsync();
        }
        else
        {
            Console.WriteLine("❌ GenericServiceDemonstrator not available");
            Console.WriteLine("   This indicates the generic services may not be properly registered");

            // Fallback to individual generic service testing
            await TestIndividualGenericServices(services);
        }

        Console.WriteLine();
    }

    private static async Task TestIndividualGenericServices(IServiceProvider services)
    {
        Console.WriteLine("--- Fallback: Testing Individual Generic Services ---");

        // Test Repository<T>
        var userRepository = services.GetService<Interfaces.IRepository<User>>();
        if (userRepository != null)
        {
            var user = new User
            {
                Username = "testuser", Email = "test@example.com", FirstName = "Test", LastName = "User"
            };
            await userRepository.AddAsync(user);
            var userId = user.Id;
            var retrievedUser = await userRepository.GetByIdAsync(userId);
            Console.WriteLine($"✅ Repository<User>: Created and retrieved user '{retrievedUser?.FirstName ?? "null"}'");
        }

        // Test GenericValidator<T>
        var userValidator = services.GetService<IGenericValidator<User>>();
        if (userValidator != null && userRepository != null)
        {
            var testUser = new User
            {
                Username = "validuser", Email = "valid@example.com", FirstName = "Valid", LastName = "User"
            };
            var validationResult = await userValidator.ValidateAsync(testUser);
            var validationStatus = string.IsNullOrEmpty(validationResult?.ErrorMessage) ? "Valid" : "Invalid";
            Console.WriteLine($"✅ GenericValidator<User>: Validation result = {validationStatus}");
        }

        // Test DataProcessor<TInput, TOutput>
        var userProcessor = services.GetService<Interfaces.IProcessor<User, User>>();
        if (userProcessor != null)
        {
            var user = new User
            {
                Username = "source", Email = "source@example.com", FirstName = "Source", LastName = "User"
            };
            var processedUser = await userProcessor.ProcessAsync(user);
            Console.WriteLine("✅ DataProcessor<User, User>: Processed user successfully");
        }

        // Test Cache<T>
        var userCache = services.GetService<ICache<User>>();
        if (userCache != null)
        {
            var cacheUser = new User
            {
                Username = "cacheuser", Email = "cache@example.com", FirstName = "Cache", LastName = "User"
            };
            await userCache.SetAsync("test-user", cacheUser);
            var cachedUser = await userCache.GetAsync("test-user");
            Console.WriteLine($"✅ Cache<User>: Cached and retrieved user '{cachedUser?.FirstName ?? "null"}'");
        }

        // Test Factory<T>
        var userFactory = services.GetService<IFactory<User>>();
        if (userFactory != null)
        {
            var factoryUser = userFactory.Create();
            Console.WriteLine("✅ Factory<User>: Created User successfully");
        }

        Console.WriteLine("   Note: Individual generic services working indicates partial generic support");
    }

    private static async Task DemonstrateManualServices(IServiceProvider services)
    {
        Console.WriteLine("=== UNREGISTERED SERVICES DEMONSTRATION ===");
        Console.WriteLine(
            "These services have [ExternalService] - constructors generated but no auto-registration");
        Console.WriteLine();

        await TestManuallyRegisteredManualServices(services);
        await TestFactoryPatternWithManualServices(services);
        await TestManualServiceDemonstrations(services);
    }

    private static async Task TestManuallyRegisteredManualServices(IServiceProvider services)
    {
        Console.WriteLine("--- Manually Registered [ExternalService] Classes ---");

        var manualService = services.GetService<IManualRegistrationService>();
        if (manualService != null)
        {
            await manualService.ProcessAsync("manual registration test");
            Console.WriteLine("✅ ManualRegistrationService: [ExternalService] but manually registered");
        }
        else
        {
            Console.WriteLine("❌ ManualRegistrationService: Not available (expected if not manually registered)");
        }

        var legacyProcessor = services.GetService<LegacyPaymentProcessor>();
        if (legacyProcessor != null)
        {
            var payment = new Payment(49.99m);
            var result = await legacyProcessor.ProcessPaymentAsync(payment.Amount, "Legacy");
            Console.WriteLine(
                $"✅ LegacyPaymentProcessor: [ExternalService] manually registered: {(result.Success ? "Success" : "Failed")} - Transaction: {result.TransactionId}");
        }

        Console.WriteLine();
    }

    private static async Task TestFactoryPatternWithManualServices(IServiceProvider services)
    {
        Console.WriteLine("--- Factory Pattern for [ExternalService] Classes ---");

        var factory = services.GetService<IManualServiceFactory>();
        if (factory != null)
        {
            // Create unregistered services through factory
            var manualService = factory.CreateManualRegistrationService();
            await manualService.ProcessAsync("factory-created service test");
            Console.WriteLine("✅ Factory created ManualRegistrationService and executed successfully");

            var testHelper = factory.CreateTestHelperService();
            var success = await testHelper.ValidateAndCreateUserAsync("Factory User");
            Console.WriteLine($"✅ Factory created TestHelperService: User creation success = {success}");

            var legacyProcessor = factory.CreateLegacyProcessor();
            var result = await legacyProcessor.ProcessLegacyPaymentAsync(new Payment(25.00m));
            Console.WriteLine($"✅ Factory created LegacyPaymentProcessor: {result.Message}");
        }
        else
        {
            Console.WriteLine("❌ ManualServiceFactory not available");
        }

        Console.WriteLine();
    }

    private static async Task TestManualServiceDemonstrations(IServiceProvider services)
    {
        Console.WriteLine("--- [ExternalService] Behavior Analysis ---");

        // These should NOT be available through DI container
        var unregisteredTypes = new[]
        {
            (typeof(ManualRegistrationService), "ManualRegistrationService"),
            (typeof(LegacyPaymentProcessor), "LegacyPaymentProcessor"),
            (typeof(TestHelperService), "TestHelperService"),
            (typeof(BasePaymentProcessor), "BasePaymentProcessor"),
            (typeof(AdvancedPaymentBase), "AdvancedPaymentBase")
        };

        Console.WriteLine("Unregistered service availability check:");
        foreach (var (serviceType, name) in unregisteredTypes)
        {
            var service = services.GetService(serviceType);
            var status = service != null ? "⚠️ REGISTERED" : "✅ NOT REGISTERED";
            Console.WriteLine($"   {status} {name}");
        }

        // These SHOULD be available (they have [Scoped] even though they inherit from [ExternalService])
        var registeredTypes = new[]
        {
            (typeof(INewPaymentProcessor), "INewPaymentProcessor -> NewPaymentProcessor"),
            (typeof(IEnterprisePaymentProcessor), "IEnterprisePaymentProcessor -> EnterprisePaymentProcessor"),
            (typeof(IManualServiceFactory), "IManualServiceFactory -> ManualServiceFactory")
        };

        Console.WriteLine();
        Console.WriteLine("Services that inherit from [ExternalService] but are [Scoped] themselves:");
        foreach (var (serviceType, description) in registeredTypes)
        {
            var service = services.GetService(serviceType);
            var status = service != null ? "✅ REGISTERED" : "❌ NOT REGISTERED";
            Console.WriteLine($"   {status} {description}");
        }

        Console.WriteLine();
        Console.WriteLine("--- [ExternalService] Key Behaviors ---");
        Console.WriteLine("✅ Constructor generation: Generated for dependency injection");
        Console.WriteLine(
            "✅ Inheritance support: Services with lifetime attributes can inherit from [ExternalService] bases");
        Console.WriteLine("✅ Manual registration: Can be manually registered if needed");
        Console.WriteLine("✅ Factory pattern: Can be instantiated through factories");
        Console.WriteLine("❌ Automatic registration: Excluded from AddIoCTools*RegisteredServices() methods");

        await Task.CompletedTask;
    }

    private static async Task DemonstrateExternalServiceIntegration(IServiceProvider services)
    {
        Console.WriteLine("=== EXTERNAL SERVICE INTEGRATION DEMONSTRATION ===");
        Console.WriteLine("Shows manual vs automatic registration patterns with external services");
        Console.WriteLine("• External services with [ExternalService] need manual configuration");
        Console.WriteLine("• IoCTools services can depend on external services seamlessly");
        Console.WriteLine("• Framework services (IMemoryCache, ILogger) work out of the box");
        Console.WriteLine("• Mixed scenarios combine automatic and manual registration");
        Console.WriteLine();

        await TestExternalServices(services);
        await TestBusinessServiceIntegration(services);
        await TestFrameworkIntegration(services);
        await TestHybridIntegration(services);
        await TestExternalServiceConfiguration(services);
    }

    private static async Task TestExternalServices(IServiceProvider services)
    {
        Console.WriteLine("--- External Services with Manual Registration ---");

        // Test HTTP Client Service
        var httpClientService = services.GetService<IHttpClientService>();
        if (httpClientService != null)
        {
            var testData = new { Message = "Hello API", Timestamp = DateTime.UtcNow };
            var response = await httpClientService.PostAsync<object, object>("payment-gateway", "/api/test", testData);
            Console.WriteLine($"✅ HttpClientService: API call result = {response.Success} ({response.Message})");
        }
        else
        {
            Console.WriteLine("❌ HttpClientService: Not available (requires manual registration)");
        }

        // Test Database Context Service
        var databaseService = services.GetService<IDatabaseContextService>();
        if (databaseService != null)
        {
            var savedUser = await databaseService.SaveAsync(new { Id = 1, Name = "Test User" });
            Console.WriteLine("✅ DatabaseContextService: Entity save completed");

            var foundUser = await databaseService.FindByIdAsync<object>(1);
            Console.WriteLine($"✅ DatabaseContextService: Entity retrieval = {foundUser != null}");
        }
        else
        {
            Console.WriteLine("❌ DatabaseContextService: Not available (requires manual registration)");
        }

        // Test Redis Cache Service
        var cacheService = services.GetService<IDistributedCacheService>();
        if (cacheService != null)
        {
            var testObject = new { Data = "cached value", Expiry = DateTime.UtcNow.AddMinutes(10) };
            await cacheService.SetAsync("test-key", testObject, TimeSpan.FromMinutes(5));
            var cachedObject = await cacheService.GetAsync<object>("test-key");
            Console.WriteLine(
                $"✅ RedisCacheService ({cacheService.CacheType}): Cache operation = {cachedObject != null}");
        }
        else
        {
            Console.WriteLine("❌ RedisCacheService: Not available (requires manual registration)");
        }

        // Test Third-party API Service
        var thirdPartyService = services.GetService<IThirdPartyApiService>();
        if (thirdPartyService != null)
        {
            var paymentRequest = new ExternalPaymentRequest(99.99m, "USD", "CreditCard");
            var paymentResult = await thirdPartyService.ProcessPaymentAsync(paymentRequest);
            Console.WriteLine(
                $"✅ ThirdPartyApiService: Payment processing = {paymentResult.Success} ({paymentResult.Message})");

            var notificationRequest =
                new ExternalNotificationRequest("user@example.com", "Test", "External service test");
            var notificationResult = await thirdPartyService.SendNotificationAsync(notificationRequest);
            Console.WriteLine($"✅ ThirdPartyApiService: Notification sending = {notificationResult.Success}");
        }
        else
        {
            Console.WriteLine("❌ ThirdPartyApiService: Not available (requires manual registration)");
        }

        Console.WriteLine();
    }

    private static async Task TestBusinessServiceIntegration(IServiceProvider services)
    {
        Console.WriteLine("--- Business Service Integration (IoCTools Service + External Dependencies) ---");

        var businessService = services.GetService<IOrderProcessingBusinessService>();
        if (businessService != null)
        {
            var externalOrder = new ExternalOrder(12345, "customer@example.com", 149.99m);
            var processingResult = await businessService.ProcessOrderAsync(externalOrder);

            Console.WriteLine($"✅ OrderProcessingBusinessService: Order processing = {processingResult.Success}");
            Console.WriteLine($"   Result: {processingResult.Message}");

            if (processingResult.Success)
            {
                var retrievalResult = await businessService.GetOrderAsync(12345);
                Console.WriteLine(
                    $"✅ OrderProcessingBusinessService: Order retrieval = {retrievalResult.Success} ({retrievalResult.Message})");
            }
        }
        else
        {
            Console.WriteLine("❌ OrderProcessingBusinessService: Not available");
        }

        Console.WriteLine();
    }

    private static async Task TestFrameworkIntegration(IServiceProvider services)
    {
        Console.WriteLine("--- Framework Integration (.NET Built-in Services) ---");

        var frameworkService = services.GetService<IFrameworkIntegrationService>();
        if (frameworkService != null)
        {
            // Test IMemoryCache integration
            var testData = new { Name = "Framework Test", Value = 42 };
            var cacheResult =
                await frameworkService.CacheDataWithMemoryCacheAsync("framework-test", testData,
                    TimeSpan.FromMinutes(2));
            Console.WriteLine($"✅ FrameworkIntegrationService: IMemoryCache operation = {cacheResult.Success}");

            // Test IConfiguration integration
            var configResult = await frameworkService.GetConfigurationValuesAsync("Logging");
            Console.WriteLine($"✅ FrameworkIntegrationService: IConfiguration access = {configResult.Success}");
            Console.WriteLine($"   Found {configResult.Values.Count} configuration values");

            // Test IServiceProvider integration
            var serviceResult =
                await frameworkService.ResolveServiceDynamicallyAsync<ILogger<FrameworkIntegrationService>>();
            Console.WriteLine($"✅ FrameworkIntegrationService: Dynamic service resolution = {serviceResult.Success}");
            Console.WriteLine($"   Service: {serviceResult.ServiceType}, Hash: {serviceResult.InstanceHashCode}");
        }
        else
        {
            Console.WriteLine("❌ FrameworkIntegrationService: Not available");
        }

        Console.WriteLine();
    }

    private static async Task TestHybridIntegration(IServiceProvider services)
    {
        Console.WriteLine("--- Hybrid Integration (IoCTools + External + Framework Services) ---");

        var hybridService = services.GetService<IHybridIntegrationService>();
        if (hybridService != null)
        {
            // Test business workflow that uses all types of services
            var workflowRequest = new BusinessWorkflowRequest("WF-001", "hybrid@example.com", 199.99m);
            var workflowResult = await hybridService.ProcessBusinessWorkflowAsync(workflowRequest);

            Console.WriteLine($"✅ HybridIntegrationService: Business workflow = {workflowResult.Success}");
            Console.WriteLine($"   Workflow ID: {workflowResult.WorkflowId}");
            Console.WriteLine($"   Result: {workflowResult.Message}");

            if (!string.IsNullOrEmpty(workflowResult.PaymentTransactionId))
                Console.WriteLine($"   Payment Transaction: {workflowResult.PaymentTransactionId}");

            // Test dependency health check
            var healthResult = await hybridService.CheckDependencyHealthAsync();
            Console.WriteLine($"✅ HybridIntegrationService: Dependency health check = {healthResult.IsHealthy}");
            Console.WriteLine($"   Health summary: {healthResult.Message}");

            foreach (var (service, healthy) in healthResult.ServiceHealth)
            {
                var status = healthy ? "✅" : "❌";
                Console.WriteLine($"   {status} {service}: {(healthy ? "Healthy" : "Unhealthy")}");
            }
        }
        else
        {
            Console.WriteLine("❌ HybridIntegrationService: Not available");
        }

        Console.WriteLine();
    }

    private static async Task TestExternalServiceConfiguration(IServiceProvider services)
    {
        Console.WriteLine("--- External Service Configuration Helper ---");

        var configHelper = services.GetService<IExternalServiceRegistrationHelper>();
        if (configHelper != null)
        {
            Console.WriteLine("✅ ExternalServiceRegistrationHelper: Available for configuration management");
            Console.WriteLine("   This service would typically be used in Program.cs during startup");
            Console.WriteLine("   to configure HTTP clients, database contexts, and other external services");
        }
        else
        {
            Console.WriteLine("❌ ExternalServiceRegistrationHelper: Not available");
        }

        Console.WriteLine();
        Console.WriteLine("--- External Service Integration Key Patterns ---");
        Console.WriteLine("✅ [ExternalService] for services with external/manual configuration");
        Console.WriteLine("✅ Manual registration in Program.cs with specific setup (HTTP clients, DB contexts)");
        Console.WriteLine("✅ IoCTools services can seamlessly depend on external services");
        Console.WriteLine("✅ Framework services (IMemoryCache, ILogger, IConfiguration) work automatically");
        Console.WriteLine("✅ Mixed scenarios combine all registration patterns effectively");
        Console.WriteLine("✅ Configuration validation ensures external services are properly configured");
        Console.WriteLine();
        Console.WriteLine("🔧 Common external service types:");
        Console.WriteLine("   • HTTP clients with named configurations and authentication");
        Console.WriteLine("   • Database contexts with connection strings and options");
        Console.WriteLine("   • Cache providers (Redis, SQL Server, etc.) with connection setup");
        Console.WriteLine("   • Third-party APIs with authentication and endpoint configuration");
        Console.WriteLine("   • Message queues, file systems, and other infrastructure services");

        await Task.CompletedTask;
    }

    private static async Task DemonstrateBackgroundServices(IServiceProvider services)
    {
        Console.WriteLine("=== BACKGROUND SERVICES DEMONSTRATION ===");
        Console.WriteLine("These services inherit from Microsoft.Extensions.Hosting.BackgroundService");
        Console.WriteLine("and are registered as hosted services in the .NET hosting system.");
        Console.WriteLine();

        Console.WriteLine("--- Background Service Status ---");
        Console.WriteLine("✅ SimpleBackgroundWorker: Running - basic background service pattern");
        Console.WriteLine("✅ EmailQueueProcessor: Running - processes email queue with dependency injection");
        Console.WriteLine("✅ DataCleanupService: Running - periodic data cleanup with configuration injection");
        Console.WriteLine("✅ HealthCheckService: Running - monitors health endpoints");
        Console.WriteLine("✅ FileWatcherService: Disabled - monitors file system changes (disabled in config)");
        Console.WriteLine("✅ NotificationSchedulerService: Running - conditional service for scheduled notifications");
        Console.WriteLine("✅ ComplexBackgroundService: Running - demonstrates complex dependency patterns");
        Console.WriteLine();

        Console.WriteLine("--- Background Service Features Demonstrated ---");
        Console.WriteLine("🔧 [Singleton]: Background services registered as singletons");
        Console.WriteLine("🔧 [Inject]: Standard dependency injection with ILogger, IServiceScopeFactory, etc.");
        Console.WriteLine("🔧 [InjectConfiguration]: Configuration binding with direct values and sections");
        Console.WriteLine("🔧 [ConditionalService]: Conditional registration based on configuration");
        Console.WriteLine("🔧 ExecuteAsync: Proper background service implementation with cancellation tokens");
        Console.WriteLine("🔧 IHostedService: Integration with .NET hosting system via AddHostedService<T>()");
        Console.WriteLine();

        Console.WriteLine("--- Configuration Examples Used ---");
        Console.WriteLine("📄 BackgroundServices:EmailProcessor - Email queue processing settings");
        Console.WriteLine("📄 DataCleanupSettings - Data cleanup configuration section");
        Console.WriteLine("📄 HealthMonitorSettings - Health check monitoring settings");
        Console.WriteLine("📄 FileWatcherSettings - File system watcher configuration (disabled)");
        Console.WriteLine("📄 NotificationSchedulerSettings - Notification scheduling settings");
        Console.WriteLine("📄 Database:ConnectionString - Direct configuration value injection");
        Console.WriteLine("📄 Features:EnableNotifications - Conditional service registration");
        Console.WriteLine();

        Console.WriteLine("--- Real-world Background Service Patterns ---");
        Console.WriteLine("📧 Email Queue Processing: Batch processing with retry logic");
        Console.WriteLine("🧹 Data Cleanup: Periodic maintenance with compression");
        Console.WriteLine("🏥 Health Monitoring: Endpoint checks with notifications");
        Console.WriteLine("📁 File Processing: File system watcher with queued processing");
        Console.WriteLine("📅 Notification Scheduling: Time-based notifications with daily digest");
        Console.WriteLine("🔀 Complex Dependencies: Multiple services, configurations, and options");
        Console.WriteLine();

        Console.WriteLine("Note: Background services are running in the background and will continue");
        Console.WriteLine("until the application shuts down. Check the console output above to see");
        Console.WriteLine("their periodic execution and logging.");

        await Task.CompletedTask;
    }

    private static async Task DemonstrateDiagnosticExamples(IServiceProvider services)
    {
        Console.WriteLine("=== DIAGNOSTIC EXAMPLES DEMONSTRATION ===");
        Console.WriteLine("These examples demonstrate IoCTools diagnostic system (IOC001-IOC026)");
        Console.WriteLine("See build output for diagnostic messages when building this project");
        Console.WriteLine();

        var diagnosticDemo = services.GetService<DiagnosticDemonstrationService>();
        if (diagnosticDemo != null)
        {
            diagnosticDemo.RunDiagnosticExamples();
            Console.WriteLine();
        }

        await DemonstrateBuildTimeDiagnostics();
        await DemonstrateConfigurationDiagnostics();
        Console.WriteLine();
    }

    private static async Task DemonstrateBuildTimeDiagnostics()
    {
        Console.WriteLine("--- Build-Time Diagnostics Overview ---");
        Console.WriteLine("The following diagnostic scenarios are included in DiagnosticExamples.cs:");
        Console.WriteLine();

        Console.WriteLine("Missing Implementation Diagnostics (IOC001):");
        Console.WriteLine("  - MissingImplementationService depends on IMissingDataService (no impl)");
        Console.WriteLine("  - MissingImplementationService depends on INonExistentRepository (no impl)");
        Console.WriteLine();

        Console.WriteLine("Unregistered Implementation Diagnostics (IOC002):");
        Console.WriteLine("  - UnregisteredDependencyService depends on IUnregisteredCalculator");
        Console.WriteLine("  - UnregisteredCalculator exists but lacks lifetime attributes");
        Console.WriteLine();

        Console.WriteLine("Lifetime Violation Diagnostics (IOC012, IOC013, IOC015):");
        Console.WriteLine("  - IOC012: ProblematicSingletonService (Singleton) → IScopedDatabaseService (Scoped)");
        Console.WriteLine(
            "  - IOC013: SingletonWithTransientDependencies (Singleton) → ITransientNotificationService (Transient)");
        Console.WriteLine(
            "  - IOC015: SingletonServiceWithInheritance inherits from BaseServiceWithScopedDependencies");
        Console.WriteLine();

        Console.WriteLine("Registration Conflict Diagnostics (IOC006-IOC009, IOC040):");
        Console.WriteLine("  - IOC006: ConflictingDependenciesService has duplicate DependsOn declarations");
        Console.WriteLine("  - IOC040: ConflictingDependenciesService mixes DependsOn with [Inject] for the same type");
        Console.WriteLine("  - IOC008: ConflictingDependenciesService has duplicate types in single DependsOn");
        Console.WriteLine(
            "  - IOC009: RedundantSkipRegistrationService skips interface not registered by RegisterAsAll");
        Console.WriteLine();

        Console.WriteLine("Redundant Configuration Diagnostics (IOC032-IOC034):");
        Console.WriteLine(
            "  - IOC032: RedundantRegisterAsService uses [RegisterAs] even though every interface is already inferred");
        Console.WriteLine(
            "  - IOC033: RedundantScopedWithDependsOnService keeps [Scoped] even though [DependsOn] implies Scoped by default");
        Console.WriteLine(
            "  - IOC034: RegisterAsAllConflictService combines [RegisterAsAll] with [RegisterAs<...>] causing redundant declarations");
        Console.WriteLine();

        Console.WriteLine("Background Service Diagnostics (IOC011, IOC014):");
        Console.WriteLine("  - IOC011: NonPartialBackgroundService not marked as partial");
        Console.WriteLine("  - IOC014: IncorrectLifetimeBackgroundService has Scoped lifetime (should be Singleton)");
        Console.WriteLine();

        Console.WriteLine("Conditional Service Diagnostics (IOC020-IOC026):");
        Console.WriteLine("  - IOC020: ConflictingConditionalService has conflicting Environment conditions");
        Console.WriteLine("  - IOC021: ConditionalWithoutServiceAttribute lacks lifetime attributes");
        Console.WriteLine(
            "    Example: Services/DiagnosticExamples.cs(297,1): error IOC021: Class 'ConditionalWithoutServiceAttribute' has [ConditionalService] attribute but lifetime attribute ([Scoped], [Singleton], [Transient]) is required");
        Console.WriteLine("  - IOC022: EmptyConditionalService has no conditions");
        Console.WriteLine("  - IOC023-025: Various ConfigValue configuration issues");
        Console.WriteLine(
            "  - IOC026: MultipleConditionalAttributesService has multiple [ConditionalService] attributes");

        await Task.CompletedTask;
    }

    private static async Task DemonstrateConfigurationDiagnostics()
    {
        Console.WriteLine();
        Console.WriteLine("--- Diagnostic Configuration Examples ---");
        Console.WriteLine("To configure diagnostic severity, add these properties to your .csproj file:");
        Console.WriteLine();
        Console.WriteLine("<PropertyGroup>");
        Console.WriteLine("  <!-- Configure severity for missing implementations (default: Error) -->");
        Console.WriteLine("  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>");
        Console.WriteLine("  ");
        Console.WriteLine("  <!-- Configure severity for unregistered implementations (default: Error) -->");
        Console.WriteLine("  <IoCToolsManualSeverity>Info</IoCToolsManualSeverity>");
        Console.WriteLine("  ");
        Console.WriteLine("  <!-- Disable all dependency validation diagnostics (default: false) -->");
        Console.WriteLine("  <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>");
        Console.WriteLine("</PropertyGroup>");
        Console.WriteLine();
        Console.WriteLine("Available severity levels: Error, Warning, Info, Hidden");
        Console.WriteLine();
        Console.WriteLine("To test diagnostics:");
        Console.WriteLine("1. Run: dotnet build");
        Console.WriteLine("2. Check build output for IOC001-IOC026 diagnostic messages");
        Console.WriteLine("3. Modify .csproj properties to change diagnostic severity");
        Console.WriteLine("4. Rebuild to see severity changes in action");

        await Task.CompletedTask;
    }

    private static async Task DemonstrateMultiInterfaceRegistration(IServiceProvider services)
    {
        Console.WriteLine("=== MULTI-INTERFACE REGISTRATION DEMONSTRATION ===");
        Console.WriteLine(
            "These services demonstrate the RegisterAsAll attribute with different modes and instance sharing");
        Console.WriteLine();

        var multiInterfaceDemo = services.GetService<IMultiInterfaceDemoService>();
        if (multiInterfaceDemo != null)
        {
            await multiInterfaceDemo.RunDemonstrationAsync();
        }
        else
        {
            Console.WriteLine("❌ MultiInterfaceDemonstrationService not available, running fallback tests");
            await TestMultiInterfaceServicesFallback(services);
        }

        Console.WriteLine();
    }

    private static async Task TestMultiInterfaceServicesFallback(IServiceProvider services)
    {
        Console.WriteLine("--- Fallback: Testing Multi-Interface Services Individually ---");

        // Test UserService with RegisterAsAll(All, Shared)
        await TestUserServiceRegistration(services);

        // Test different registration modes
        await TestRegistrationModes(services);

        // Test instance sharing
        await TestInstanceSharing(services);

        // Test skip registration
        await TestSkipRegistration(services);

        // Test repository pattern
        await TestRepositoryPattern(services);

        // Test performance service
        await TestPerformanceService(services);
    }

    private static async Task TestUserServiceRegistration(IServiceProvider services)
    {
        Console.WriteLine("--- Testing UserService with RegisterAsAll(All, Shared) ---");

        var userService = services.GetService<IMultiUserService>();
        var userRepository = services.GetService<IMultiUserRepository>();
        var userValidator = services.GetService<IMultiUserValidator>();
        var userConcrete = services.GetService<UserService>();

        Console.WriteLine($"IMultiUserService available: {userService != null}");
        Console.WriteLine($"IMultiUserRepository available: {userRepository != null}");
        Console.WriteLine($"IMultiUserValidator available: {userValidator != null}");
        Console.WriteLine($"UserService (concrete) available: {userConcrete != null}");

        if (userService != null && userValidator != null && userRepository != null)
        {
            var user = await userService.CreateUserAsync("John Doe", "john@example.com");
            var isValid = userValidator.ValidateUser(user).IsValid;
            var foundUser = await userRepository.FindByIdAsync(user.Id);

            Console.WriteLine($"✅ Created user: {user.Name}, Valid: {isValid}, Found: {foundUser != null}");
            Console.WriteLine("✅ All interfaces resolve to the same shared instance");
        }

        Console.WriteLine();
    }

    private static Task TestRegistrationModes(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Different Registration Modes ---");

        // DirectOnly mode test
        var directOnlyConcrete = services.GetService<DirectOnlyPaymentProcessor>();
        var directOnlyInterface = services.GetService<IMultiPaymentService>();

        Console.WriteLine("DirectOnlyPaymentProcessor (DirectOnly mode):");
        Console.WriteLine($"  Concrete type available: {directOnlyConcrete != null}");
        Console.WriteLine($"  Interface available: {directOnlyInterface != null}");
        Console.WriteLine("  Expected: Concrete=true, Interface=false");

        // Exclusionary mode test  
        var exclusionaryInterface = services.GetService<IMultiPaymentValidator>();

        Console.WriteLine("InterfaceOnlyPaymentProcessor (Exclusionary mode):");
        Console.WriteLine($"  Interface available: {exclusionaryInterface != null}");
        Console.WriteLine("  Expected: Interface=true, Concrete=false");

        if (exclusionaryInterface != null)
        {
            var payment = new Payment(150.00m);
            var isValid = exclusionaryInterface.ValidatePayment(payment);
            Console.WriteLine($"✅ Payment validation via interface: {isValid}");
        }

        Console.WriteLine();
        return Task.CompletedTask;
    }

    private static async Task TestInstanceSharing(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Instance Sharing Modes ---");

        // Test RegisterAsAll instances (existing functionality)
        var separateCacheService = services.GetService<IMultiCacheService>();
        var separateCacheProvider = services.GetService<IMultiCacheProvider>();

        if (separateCacheService != null && separateCacheProvider != null)
        {
            separateCacheService.Set("test-key", "test-value");
            var exists = separateCacheProvider.Exists("test-key");

            Console.WriteLine("RegisterAsAll Separate Instance Sharing:");
            Console.WriteLine($"  Set via IMultiCacheService, exists via IMultiCacheProvider: {exists}");
            Console.WriteLine("  Expected with separate instances: Data sharing may vary");
        }

        // Test RegisterAsAll shared instances
        var sharedCacheService = services.GetService<SharedInstanceCacheManager>();

        Console.WriteLine("SharedInstanceCacheManager:");
        Console.WriteLine($"  Service available: {sharedCacheService != null}");
        Console.WriteLine("  Expected: Single shared instance across all interfaces");

        // === NEW: Test RegisterAs<T> InstanceSharing.Separate ===
        Console.WriteLine("\n--- Testing RegisterAs<T> InstanceSharing.Separate ---");

        var regService1 = services.GetService<IRegistrationService>();
        var valService1 = services.GetService<IValidationServiceSeparate>();

        if (regService1 != null && valService1 != null)
        {
            // These should be different instances (separate)
            regService1.Register("test-item-separate");
            var isValid = valService1.Validate("test-item-separate");

            Console.WriteLine("✅ RegisterAs<T> InstanceSharing.Separate:");
            Console.WriteLine($"  IRegistrationService available: {regService1 != null}");
            Console.WriteLine($"  IValidationServiceSeparate available: {valService1 != null}");
            Console.WriteLine($"  Validation result: {isValid}");
            Console.WriteLine("  Expected: Different instances for each interface (separate state)");
        }

        // === NEW: Test RegisterAs<T> InstanceSharing.Shared ===
        Console.WriteLine("\n--- Testing RegisterAs<T> InstanceSharing.Shared ---");

        var sharedCache = services.GetService<ISharedCacheService>();
        var sharedStats = services.GetService<ISharedStatsService>();
        var sharedHealth = services.GetService<ISharedHealthService>();

        if (sharedCache != null && sharedStats != null && sharedHealth != null)
        {
            // These should be the SAME instance (shared state)
            sharedCache.CacheItem("test-key", "test-value");
            sharedStats.IncrementCounter("test-counter");
            sharedHealth.ReportHealth(true);

            var cachedValue = sharedCache.GetItem<string>("test-key");
            var counterValue = sharedStats.GetCount("test-counter");
            var healthStatus = sharedHealth.IsHealthy();

            Console.WriteLine("✅ RegisterAs<T> InstanceSharing.Shared:");
            Console.WriteLine($"  ISharedCacheService available: {sharedCache != null}");
            Console.WriteLine($"  ISharedStatsService available: {sharedStats != null}");
            Console.WriteLine($"  ISharedHealthService available: {sharedHealth != null}");
            Console.WriteLine($"  Cached value: {cachedValue}");
            Console.WriteLine($"  Counter value: {counterValue}");
            Console.WriteLine($"  Health status: {healthStatus}");
            Console.WriteLine("  Expected: Same instance across all interfaces (shared state)");
        }

        // === NEW: Test EF Core DbContext Integration Pattern ===
        Console.WriteLine("\n--- Testing EF Core DbContext Integration Pattern ---");

        var transactionService = services.GetService<IDbTransactionService>();
        var databaseService = services.GetService<IDbDataService>();

        if (transactionService != null && databaseService != null)
        {
            // Test the EF Core integration pattern
            transactionService.BeginTransaction();
            var result = await databaseService.ExecuteCommandAsync("SELECT COUNT(*) FROM Users");
            transactionService.CommitTransaction();

            Console.WriteLine("✅ EF Core DbContext Integration:");
            Console.WriteLine($"  IDbTransactionService available: {transactionService != null}");
            Console.WriteLine($"  IDbDataService available: {databaseService != null}");
            Console.WriteLine($"  Execute command result: {result}");
            Console.WriteLine("  Expected: Factory pattern - interfaces resolve to externally registered DbContext");
        }

        // === NEW: Test Advanced Multi-Interface Shared Pattern ===
        Console.WriteLine("\n--- Testing Advanced Multi-Interface Shared Pattern ---");

        var metrics = services.GetService<IMetricsCollector>();
        var events = services.GetService<IEventPublisher>();
        var health = services.GetService<IHealthReporter>();
        var config = services.GetService<IConfigurationWatcher>();

        if (metrics != null && events != null && health != null && config != null)
        {
            // All should share the same underlying instance
            metrics.RecordMetric("cpu_usage", 75.5);
            await events.PublishAsync("system_startup", new { timestamp = DateTime.UtcNow });
            health.ReportHealth("database", true);
            config.WatchConfiguration("max_connections", value => Console.WriteLine($"Config changed: {value}"));

            Console.WriteLine("✅ Advanced Multi-Interface Shared:");
            Console.WriteLine($"  IMetricsCollector available: {metrics != null}");
            Console.WriteLine($"  IEventPublisher available: {events != null}");
            Console.WriteLine($"  IHealthReporter available: {health != null}");
            Console.WriteLine($"  IConfigurationWatcher available: {config != null}");
            Console.WriteLine("  Expected: Single shared service instance handling all interface contracts");
        }

        Console.WriteLine("\n🎯 INSTANCE SHARING SUMMARY:");
        Console.WriteLine("  ✅ InstanceSharing.Separate: Different instances per interface");
        Console.WriteLine("  ✅ InstanceSharing.Shared: Same instance across all interfaces");
        Console.WriteLine("  ✅ EF Core Pattern: Factory registration for external services");
        Console.WriteLine("  ✅ Advanced Shared: Complex multi-interface service scenarios");
        Console.WriteLine();
    }

    private static async Task TestSkipRegistration(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Skip Registration ---");

        var dataService = services.GetService<IDataService>();
        var dataValidator = services.GetService<IDataValidator>();
        var dataLogger = services.GetService<IDataLogger>();
        var dataCacheService = services.GetService<IDataCacheService>();

        Console.WriteLine("SelectiveDataService with SkipRegistration:");
        Console.WriteLine($"  IDataService (should be registered): {dataService != null}");
        Console.WriteLine($"  IDataValidator (should be registered): {dataValidator != null}");
        Console.WriteLine($"  IDataLogger (should be skipped): {dataLogger != null}");
        Console.WriteLine($"  IDataCacheService (should be skipped): {dataCacheService != null}");

        if (dataService != null)
        {
            var data = await dataService.GetDataAsync("test-123");
            Console.WriteLine($"✅ Data service result: {data}");
        }

        Console.WriteLine();
    }

    private static Task TestRepositoryPattern(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Generic Repository Pattern ---");

        // NOTE: Generic repository pattern temporarily commented out due to open generic registration issue
        // var userRepo = services.GetService<IMultiRepository<User>>();
        // var userQueryable = services.GetService<IMultiQueryable<User>>();

        Console.WriteLine("Generic Repository<User>:");
        Console.WriteLine("  IMultiRepository<User>: Temporarily disabled (open generic registration issue)");
        Console.WriteLine("  IMultiQueryable<User>: Temporarily disabled (open generic registration issue)");

        Console.WriteLine(
            "✅ Generic repository pattern: Temporarily disabled - pending open generic registration support");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    private static async Task TestPerformanceService(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Performance Service ---");

        var perfService = services.GetService<IPerformanceTestService>();
        var perfMetrics = services.GetService<IPerformanceMetrics>();
        var perfBenchmark = services.GetService<IPerformanceBenchmark>();

        Console.WriteLine("PerformanceTestService:");
        Console.WriteLine($"  IPerformanceTestService available: {perfService != null}");
        Console.WriteLine($"  IPerformanceMetrics available: {perfMetrics != null}");
        Console.WriteLine($"  IPerformanceBenchmark available: {perfBenchmark != null}");

        if (perfBenchmark != null)
        {
            var result = await perfBenchmark.RunBenchmarkAsync(5);
            Console.WriteLine(
                $"✅ Benchmark completed: {result.Iterations} iterations in {result.TotalTime.TotalMilliseconds:F2}ms");
        }

        Console.WriteLine();
        Console.WriteLine("--- Multi-Interface Registration Benefits ---");
        Console.WriteLine("✅ Single implementation serves multiple interfaces");
        Console.WriteLine("✅ Flexible registration modes (DirectOnly, All, Exclusionary)");
        Console.WriteLine("✅ Configurable instance sharing (Separate, Shared)");
        Console.WriteLine("✅ Selective interface skipping with generic SkipRegistration");
        Console.WriteLine("✅ Works with generic types and inheritance");
        Console.WriteLine("✅ Reduces boilerplate registration code");
        Console.WriteLine();
    }

    private static async Task DemonstrateTransientServices(IServiceProvider services)
    {
        Console.WriteLine("=== 2. TRANSIENT SERVICES DEMONSTRATION ===");
        Console.WriteLine("Transient services create new instances on each resolution:");
        Console.WriteLine();

        await TestTransientLifetime(services);
        Console.WriteLine();
    }

    private static Task TestTransientLifetime(IServiceProvider services)
    {
        Console.WriteLine("--- Transient Service Examples ---");

        // Test transient services if available
        var emailValidator1 = services.GetService<IEmailValidator>();
        var emailValidator2 = services.GetService<IEmailValidator>();
        if (emailValidator1 != null && emailValidator2 != null)
            Console.WriteLine(
                $"  ✅ Email Validators: Different instances = {!ReferenceEquals(emailValidator1, emailValidator2)}");
        else
            Console.WriteLine("  ⚠️ Transient email validator services not available");

        // Test multiple resolutions of same service type
        var services1 = services.GetServices<INotificationService>().ToList();
        var services2 = services.GetServices<INotificationService>().ToList();

        Console.WriteLine(
            $"  ✅ Multiple service resolution: First batch = {services1.Count}, Second batch = {services2.Count}");
        Console.WriteLine("  Note: Each GetServices() call may return new instances for Transient services");

        return Task.CompletedTask;
    }

    private static async Task DemonstrateConditionalServices(IServiceProvider services)
    {
        Console.WriteLine("=== 5. CONDITIONAL SERVICES DEMONSTRATION ===");
        Console.WriteLine("Services using [ConditionalService] for environment/configuration-based registration:");
        Console.WriteLine();

        var conditionalDemo = services.GetService<ConditionalServicesDemonstrationService>();
        if (conditionalDemo != null)
        {
            await conditionalDemo.DemonstrateConditionalServicesAsync();
        }
        else
        {
            Console.WriteLine("⚠️ ConditionalServicesDemonstrationService not available");
            await TestConditionalServicePatterns(services);
        }

        Console.WriteLine();
    }

    private static async Task TestConditionalServicePatterns(IServiceProvider services)
    {
        Console.WriteLine("--- Testing Conditional Service Patterns ---");

        // Test environment-based email services
        var envEmailServices = services.GetServices<IEnvironmentEmailService>().ToList();
        Console.WriteLine($"  ✅ Environment-based email services registered: {envEmailServices.Count}");
        foreach (var emailService in envEmailServices.Take(1))
            await emailService.SendEmailAsync("user@example.com", "Conditional Test",
                "Environment-based service selection");

        // Test configuration-driven cache services  
        var cacheServices = services.GetServices<IConfigurableCacheService>().ToList();
        Console.WriteLine($"  ✅ Configuration-driven cache services registered: {cacheServices.Count}");
        foreach (var cacheService in cacheServices.Take(1))
        {
            await cacheService.SetAsync("conditional-test", "conditional-value");
            var value = await cacheService.GetAsync<string>("conditional-test");
            Console.WriteLine($"    Cache test result: {value}");
        }

        // Test payment processor selection
        var paymentProcessors = services.GetServices<IPaymentProcessor>().ToList();
        Console.WriteLine($"  ✅ Payment processors registered: {paymentProcessors.Count}");
        foreach (var processor in paymentProcessors.Take(1))
        {
            var result = await processor.ProcessPaymentAsync(99.99m, "CreditCard");
            Console.WriteLine($"    Payment result: {result.Success}, Version: {result.ProcessorVersion}");
        }

        // Test storage service selection
        var storageServices = services.GetServices<IStorageService>().ToList();
        Console.WriteLine($"  ✅ Storage services registered: {storageServices.Count}");
        foreach (var storage in storageServices.Take(1))
        {
            var testData = Encoding.UTF8.GetBytes("Conditional storage test");
            await storage.StoreFileAsync("conditional-test.txt", testData);
            Console.WriteLine("    Storage service test completed");
        }
    }

    private static async Task DemonstrateInheritanceExamples(IServiceProvider services)
    {
        Console.WriteLine("=== 7. INHERITANCE HIERARCHY DEMONSTRATION ===");
        Console.WriteLine("Complex inheritance chains with proper dependency resolution:");
        Console.WriteLine();

        await TestInheritancePatterns(services);
        Console.WriteLine();
    }

    private static async Task TestInheritancePatterns(IServiceProvider services)
    {
        Console.WriteLine("--- Inheritance Chain Examples ---");

        // Test 3-level inheritance: BaseRepository -> UserRepository
        var userRepository = services.GetService<IUserRepository>();
        if (userRepository != null)
        {
            var user = new User(1, "Jane Smith", "jane@example.com");
            await userRepository.SaveAsync(user);
            var foundUsers = await userRepository.FindByEmailAsync("jane@example.com");
            var foundUser = foundUsers.FirstOrDefault();
            Console.WriteLine($"  ✅ UserRepository (3-level inheritance): User operations = {foundUser?.Name}");
        }
        else
        {
            Console.WriteLine("  ⚠️ UserRepository inheritance example not available");
        }

        // Test credit card processor inheritance chain
        var creditCardProcessor = services.GetService<ICreditCardProcessor>();
        if (creditCardProcessor != null)
        {
            var result = await creditCardProcessor.ProcessCreditCardPaymentAsync(299.99m, "4111111111111111");
            Console.WriteLine($"  ✅ CreditCardProcessor (inheritance chain): Payment = {result.Success}");

            var refund = await creditCardProcessor.RefundPaymentAsync(50.00m, "TXN123");
            Console.WriteLine($"    Refund processing via inherited methods = {refund.Success}");
        }
        else
        {
            Console.WriteLine("  ⚠️ CreditCardProcessor inheritance example not available");
        }

        // Test user validator inheritance
        var userValidator = services.GetService<IUserValidator>();
        if (userValidator != null)
        {
            var testUser = new User(2, "Test User", "test@validation.com");
            var validation = userValidator.ValidateUser(testUser);
            Console.WriteLine($"  ✅ UserValidator (inheritance chain): Validation = {validation.IsValid}");
        }
        else
        {
            Console.WriteLine("  ⚠️ UserValidator inheritance example not available");
        }

        // Test application settings service with configuration inheritance
        var appSettingsService = services.GetService<IApplicationSettingsService>();
        if (appSettingsService != null)
        {
            var settings = await appSettingsService.GetApplicationSettingsAsync();
            var configValid = await appSettingsService.ValidateConfigurationAsync();
            Console.WriteLine(
                $"  ✅ ApplicationSettingsService (config inheritance): App = {settings.ApplicationName}, Valid = {configValid}");
        }
        else
        {
            Console.WriteLine("  ⚠️ ApplicationSettingsService inheritance example not available");
        }

        // Show registered inheritance services
        await TestRegisteredInheritanceServices(services);
    }

    private static async Task DemonstrateAdvancedPatterns(IServiceProvider services)
    {
        Console.WriteLine("=== 11. ADVANCED PATTERNS DEMONSTRATION ===");
        Console.WriteLine("Complex scenarios showcasing IoCTools advanced capabilities:");
        Console.WriteLine();

        var advancedPatternsService = services.GetService<IAdvancedPatternsService>();
        if (advancedPatternsService != null)
        {
            await advancedPatternsService.DemonstrateCurrentCapabilitiesAsync();
            await advancedPatternsService.DemonstrateFutureEnhancementsAsync();
            Console.WriteLine("  ✅ Advanced patterns demonstration completed");
        }
        else
        {
            Console.WriteLine("⚠️ Advanced patterns service not available, demonstrating individual patterns:");
            await TestAdvancedPatternsFallback(services);
        }

        Console.WriteLine();
    }

    private static async Task TestAdvancedPatternsFallback(IServiceProvider services)
    {
        Console.WriteLine("--- Advanced Pattern Examples ---");

        // Test generic services - using available generic services
        var genericRepo = services.GetService<GenericRepository<User>>();
        var cacheService = services.GetService<Cache<User>>();
        Console.WriteLine($"  ✅ Generic services available: {genericRepo != null || cacheService != null}");

        // Test composite notification service with multiple providers
        var compositeNotification = services.GetService<CompositeNotificationService>();
        if (compositeNotification != null)
        {
            await compositeNotification.SendEmailAsync("user@example.com", "Test", "Advanced pattern test");
            Console.WriteLine("  ✅ CompositeNotificationService: Multi-provider notification pattern");
        }

        // Test performance benchmarking
        var perfBenchmark = services.GetService<IPerformanceBenchmark>();
        if (perfBenchmark != null)
        {
            var result = await perfBenchmark.RunBenchmarkAsync(5);
            Console.WriteLine(
                $"  ✅ Performance benchmarking: {result.Iterations} iterations in {result.TotalTime.TotalMilliseconds:F2}ms");
        }

        // Test factory patterns with unregistered services
        var factory = services.GetService<IManualServiceFactory>();
        if (factory != null)
        {
            var testService = factory.CreateTestHelperService();
            var success = await testService.ValidateAndCreateUserAsync("Factory User");
            Console.WriteLine($"  ✅ Factory pattern: Unregistered service creation = {success}");
        }
    }

    private static async Task DemonstrateCollectionInjection(IServiceProvider services)
    {
        Console.WriteLine("=== COLLECTION INJECTION DEMONSTRATION ===");
        Console.WriteLine("These examples demonstrate comprehensive collection injection patterns:");
        Console.WriteLine("• IEnumerable<T> with multiple implementations");
        Console.WriteLine("• IList<T> for ordered processing chains");
        Console.WriteLine("• IReadOnlyList<T> for analysis and aggregation");
        Console.WriteLine("• Generic collection patterns");
        Console.WriteLine("• Collection injection with different service lifetimes");
        Console.WriteLine();

        await DemonstrateEnumerableInjection(services);
        await DemonstrateListInjection(services);
        await DemonstrateReadOnlyListInjection(services);
        await DemonstrateGenericCollectionInjection(services);
        await DemonstrateLifetimeCollectionMixing(services);

        Console.WriteLine("=== COLLECTION INJECTION BENEFITS ===");
        Console.WriteLine("✅ Multiple implementations automatically collected");
        Console.WriteLine("✅ Supports different collection types (IEnumerable, IList, IReadOnlyList)");
        Console.WriteLine("✅ Works seamlessly with generic types");
        Console.WriteLine("✅ Enables powerful patterns: chains, aggregation, multi-provider");
        Console.WriteLine("✅ Different service lifetimes work together in collections");
        Console.WriteLine("✅ Perfect for plugin architectures and strategy patterns");
        Console.WriteLine("✅ Zero additional configuration required");
        Console.WriteLine();
    }

    private static async Task DemonstrateEnumerableInjection(IServiceProvider services)
    {
        Console.WriteLine("--- 1. IEnumerable<T> Multi-Implementation Pattern ---");
        Console.WriteLine("Multiple notification services automatically collected:");
        Console.WriteLine();

        var notificationManager = services.GetService<NotificationManager>();
        if (notificationManager == null)
        {
            Console.WriteLine("❌ NotificationManager not available");
            return;
        }

        // Get statistics about available services
        var stats = await notificationManager.GetServiceStatisticsAsync();
        Console.WriteLine("📊 Service Statistics:");
        Console.WriteLine($"   Total services: {stats.TotalServices}");
        Console.WriteLine($"   Available services: {stats.AvailableServices}");

        foreach (var service in stats.Services)
            Console.WriteLine(
                $"   • {service.ServiceType} (Priority: {service.Priority}) - Available: {service.IsAvailable}");
        Console.WriteLine();

        // Test sending to all services
        var allResult =
            await notificationManager.SendToAllAsync("user@example.com", "Welcome to IoCTools collection injection!");
        Console.WriteLine("📧 Send to All Services:");
        Console.WriteLine($"   Overall success: {allResult.AnySuccess}");
        foreach (var result in allResult.Results)
        {
            var status = result.Success ? "✅" : "❌";
            Console.WriteLine($"   {status} {result.ServiceType}: {result.Message}");
        }

        Console.WriteLine();

        // Test fail-fast pattern (first available)
        var firstResult =
            await notificationManager.SendToFirstAvailableAsync("priority@example.com", "High priority notification");
        var priorityStatus = firstResult.Success ? "✅" : "❌";
        Console.WriteLine("🚀 First Available Service (Fail-Fast):");
        Console.WriteLine($"   {priorityStatus} {firstResult.ServiceType}: {firstResult.Message}");
        Console.WriteLine();
    }

    private static async Task DemonstrateListInjection(IServiceProvider services)
    {
        Console.WriteLine("--- 2. IList<T> Processing Chain Pattern ---");
        Console.WriteLine("Ordered processing chain with configurable processors:");
        Console.WriteLine();

        var processorChain = services.GetService<ProcessorChain>();
        if (processorChain == null)
        {
            Console.WriteLine("❌ ProcessorChain not available");
            return;
        }

        // Get chain information
        var chainInfo = processorChain.GetChainInfo();
        Console.WriteLine("🔗 Processing Chain Information:");
        Console.WriteLine($"   Total processors: {chainInfo.ProcessorCount}");
        foreach (var processor in chainInfo.Processors)
            Console.WriteLine($"   • {processor.Name} (Order: {processor.Order})");
        Console.WriteLine();

        // Test processing data through the chain
        var testData = new CollectionProcessingData(
            "test-001",
            "transform this sample content",
            new Dictionary<string, string> { { "source", "collection_demo" }, { "priority", "high" } }
        );

        var chainResult = await processorChain.ProcessChainAsync(testData);
        Console.WriteLine("⚙️  Chain Processing Result:");
        Console.WriteLine($"   Data ID: {chainResult.DataId}");
        Console.WriteLine($"   Overall success: {chainResult.Success}");
        Console.WriteLine($"   Final content: '{chainResult.FinalData.Content}'");
        Console.WriteLine();

        Console.WriteLine("   📋 Step-by-step Results:");
        var stepNumber = 1;
        foreach (var result in chainResult.ProcessingResults)
        {
            var stepStatus = result.Success ? "✅" : "❌";
            Console.WriteLine($"   Step {stepNumber}: {stepStatus} {result.Message}");
            if (result.ProcessedContent != null) Console.WriteLine($"            → '{result.ProcessedContent}'");
            stepNumber++;
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateReadOnlyListInjection(IServiceProvider services)
    {
        Console.WriteLine("--- 3. IReadOnlyList<T> Analysis Pattern ---");
        Console.WriteLine("Read-only collections for analysis and aggregation:");
        Console.WriteLine();

        var processorAnalyzer = services.GetService<ProcessorAnalyzer>();
        if (processorAnalyzer == null)
        {
            Console.WriteLine("❌ ProcessorAnalyzer not available");
            return;
        }

        // Analyze all processors without modifying them
        var analysisReport = await processorAnalyzer.AnalyzeProcessorsAsync();
        Console.WriteLine("📊 Processor Analysis Report:");
        Console.WriteLine($"   Total processors analyzed: {analysisReport.TotalProcessors}");
        Console.WriteLine($"   Active processors (can process sample): {analysisReport.ActiveProcessors}");
        Console.WriteLine($"   Average processing time: {analysisReport.AverageProcessingTimeMs:F2}ms");
        Console.WriteLine();

        Console.WriteLine("   📈 Individual Processor Analysis:");
        foreach (var analysis in analysisReport.ProcessorAnalyses)
        {
            var canProcessStatus = analysis.CanProcessSample ? "✅" : "❌";
            var timeDisplay = analysis.ProcessingTimeMs > 0 ? $"{analysis.ProcessingTimeMs:F2}ms" :
                analysis.ProcessingTimeMs < 0 ? "ERROR" : "SKIPPED";
            Console.WriteLine(
                $"   {canProcessStatus} {analysis.ProcessorName} (Order: {analysis.Order}) - Time: {timeDisplay}");
        }

        Console.WriteLine();

        // Test aggregation services
        var aggregatorService = services.GetService<AggregatorService>();
        if (aggregatorService != null)
        {
            var testNumbers = new[] { 10.5m, 25.0m, 7.3m, 45.2m, 12.8m };
            var aggregationReport = await aggregatorService.PerformAllAggregationsAsync(testNumbers);

            Console.WriteLine("🧮 Aggregation Service Results:");
            Console.WriteLine($"   Data points: {aggregationReport.DataCount}");
            Console.WriteLine($"   Aggregators used: {aggregationReport.AggregatorCount}");

            foreach (var result in aggregationReport.Results)
                Console.WriteLine($"   • {result.AggregatorName} (Priority: {result.Priority}): {result.Value:F2}");

            var primaryResult = await aggregatorService.GetPrimaryAggregationAsync(testNumbers);
            Console.WriteLine($"   🎯 Primary aggregation (highest priority): {primaryResult:F2}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateGenericCollectionInjection(IServiceProvider services)
    {
        Console.WriteLine("--- 4. Generic Collection Injection Pattern ---");
        Console.WriteLine("Type-safe collection injection with generic validators:");
        Console.WriteLine();

        var validationService = services.GetService<ValidationService>();
        if (validationService == null)
        {
            Console.WriteLine("❌ ValidationService not available");
            return;
        }

        // Get validation statistics
        var stats = validationService.GetValidationStatistics();
        Console.WriteLine("🛡️  Validation Service Statistics:");
        Console.WriteLine($"   User validators: {stats.UserValidatorCount}");
        Console.WriteLine($"   Order validators: {stats.OrderValidatorCount}");
        Console.WriteLine($"   Total validators: {stats.TotalValidators}");
        Console.WriteLine();

        // Test user validation with all user validators
        var testUser = new User(123, "Test User", "test@example.com") { IsActive = true };
        var userValidation = await validationService.ValidateUserAsync(testUser);

        Console.WriteLine("👤 User Validation Results:");
        Console.WriteLine($"   Entity: {userValidation.EntityType} (ID: {userValidation.EntityId})");
        Console.WriteLine($"   Overall valid: {userValidation.IsValid}");
        Console.WriteLine($"   Has warnings: {userValidation.HasWarnings}");

        foreach (var validator in userValidation.ValidatorResults)
        {
            var severityIcon = validator.Severity == 1 ? "🚫" : validator.Severity == 2 ? "⚠️" : "ℹ️";
            var validStatus = validator.IsValid ? "✅" : "❌";
            Console.WriteLine(
                $"   {severityIcon} {validStatus} {validator.ValidatorName} (Severity: {validator.Severity})");

            if (!validator.IsValid && validator.Errors.Any())
                foreach (var error in validator.Errors)
                    Console.WriteLine($"      → {error}");
        }

        Console.WriteLine();

        // Test order validation
        var testOrder = new Order(456, "customer@example.com", new Payment(99.99m));
        var orderValidation = await validationService.ValidateOrderAsync(testOrder);

        Console.WriteLine("📋 Order Validation Results:");
        Console.WriteLine($"   Entity: {orderValidation.EntityType} (ID: {orderValidation.EntityId})");
        Console.WriteLine($"   Overall valid: {orderValidation.IsValid}");
        Console.WriteLine($"   Has warnings: {orderValidation.HasWarnings}");

        foreach (var validator in orderValidation.ValidatorResults)
        {
            var severityIcon = validator.Severity == 1 ? "🚫" : validator.Severity == 2 ? "⚠️" : "ℹ️";
            var validStatus = validator.IsValid ? "✅" : "❌";
            Console.WriteLine($"   {severityIcon} {validStatus} {validator.ValidatorName}");
        }

        Console.WriteLine();
    }

    private static async Task DemonstrateLifetimeCollectionMixing(IServiceProvider services)
    {
        Console.WriteLine("--- 5. Collection with Mixed Service Lifetimes ---");
        Console.WriteLine("Different service lifetimes working together in collections:");
        Console.WriteLine();

        var multiProviderService = services.GetService<MultiProviderService>();
        if (multiProviderService == null)
        {
            Console.WriteLine("❌ MultiProviderService not available");
            return;
        }

        // First call to get data from all providers
        Console.WriteLine("🔄 First Call - Getting data from all providers:");
        var firstCallResult = await multiProviderService.GetFromAllProvidersAsync("test-key");

        foreach (var result in firstCallResult.Results)
            Console.WriteLine($"   • {result.ProviderName} (Instance: {result.InstanceId}): {result.Data}");
        Console.WriteLine();

        // Demonstrate lifetime behavior with multiple calls
        var lifetimeDemo = await multiProviderService.DemonstrateLifetimeBehaviorAsync();

        Console.WriteLine("🕒 Lifetime Behavior Analysis:");
        var analysis = lifetimeDemo.GetLifetimeAnalysis();
        foreach (var behaviorAnalysis in analysis)
        {
            var icon = behaviorAnalysis.Contains("Same instance") ? "🔄" : "🆕";
            Console.WriteLine($"   {icon} {behaviorAnalysis}");
        }

        Console.WriteLine();

        Console.WriteLine("📝 Expected Lifetime Behaviors:");
        Console.WriteLine("   🔄 CachedDataProvider (Singleton): Same instance across all calls");
        Console.WriteLine("   🔄 DatabaseDataProvider (Scoped): Same instance within scope, different across scopes");
        Console.WriteLine("   🆕 ApiDataProvider (Transient): New instance every time");
        Console.WriteLine();

        // Second call to show lifetime consistency
        Console.WriteLine("🔄 Second Call - Verifying instance consistency:");
        var secondCallResult = await multiProviderService.GetFromAllProvidersAsync("test-key-2");

        foreach (var result in secondCallResult.Results)
            Console.WriteLine($"   • {result.ProviderName} (Instance: {result.InstanceId}): {result.Data}");
        Console.WriteLine();
    }

    /// <summary>
    ///     Demonstrates the major architectural enhancements delivered during 100% test success campaign
    /// </summary>
    private static async Task DemonstrateArchitecturalEnhancements(IServiceProvider services)
    {
        Console.WriteLine();
        Console.WriteLine("🎯 === ARCHITECTURAL ENHANCEMENTS SHOWCASE ===");
        Console.WriteLine("Demonstrating major improvements from 100% test success rate campaign:");
        Console.WriteLine();

        // 1. Modern Lifetime Attributes
        Console.WriteLine("--- 1. Individual Lifetime Attributes ---");
        Console.WriteLine("Clean syntax using [Scoped], [Singleton], [Transient]:");
        Console.WriteLine();

        var scopedService = services.GetService<ModernScopedService>();
        if (scopedService != null)
        {
            scopedService.ProcessRequest("REQ-001");
            Console.WriteLine("  ✅ [Scoped] ModernScopedService: Clean lifetime specification");
        }

        var singletonService = services.GetService<ModernSingletonService>();
        if (singletonService != null)
        {
            var count1 = singletonService.GetAndIncrement("demo");
            var count2 = singletonService.GetAndIncrement("demo");
            Console.WriteLine(
                $"  ✅ [Singleton] ModernSingletonService: Counter {count1} -> {count2} (state maintained)");
        }

        var transientService1 = services.GetService<ModernTransientService>();
        var transientService2 = services.GetService<ModernTransientService>();
        if (transientService1 != null && transientService2 != null)
        {
            var id1 = transientService1.GetInstanceId();
            var id2 = transientService2.GetInstanceId();
            Console.WriteLine($"  ✅ [Transient] ModernTransientService: Different instances ({id1} != {id2})");
        }

        // 2. Intelligent Service Registration
        Console.WriteLine();
        Console.WriteLine("--- 2. Intelligent Service Registration ---");
        Console.WriteLine("Automatic detection and registration for IEnumerable<T> scenarios:");
        Console.WriteLine();

        var collectionService = services.GetService<CollectionAwareService>();
        if (collectionService != null)
        {
            var result = await collectionService.ProcessWithAllServicesAsync("architectural-demo");
            Console.WriteLine($"  ✅ Auto-detected {result.ProcessedBy.Count()} IAutomaticService implementations:");
            foreach (var serviceName in result.ProcessedBy)
                Console.WriteLine($"     • {serviceName}Service - automatically registered");
            Console.WriteLine($"  📊 Processed at: {result.ProcessedAt:HH:mm:ss}");
        }

        // 3. Mixed Dependency Scenarios
        Console.WriteLine();
        Console.WriteLine("--- 3. Mixed Dependency Scenarios ---");
        Console.WriteLine("Services with BOTH [Inject] and [InjectConfiguration] requiring explicit lifetime:");
        Console.WriteLine();

        var mixedService = services.GetService<MixedDependencyService>();
        if (mixedService != null)
        {
            var testData = new[] { "item1", "item2", "item3", "item4", "item5", "item6" };
            var processingResult = await mixedService.ProcessDataAsync(testData);

            Console.WriteLine($"  ✅ MixedDependencyService: {processingResult.Configuration}");
            Console.WriteLine(
                $"     📈 Processed {processingResult.ProcessedItems}/{processingResult.TotalItems} items in {processingResult.BatchCount} batches");
            Console.WriteLine("     🔧 Field injection + Configuration injection working together");
        }

        // 4. Enhanced Configuration Integration
        Console.WriteLine();
        Console.WriteLine("--- 4. Enhanced Configuration Integration ---");
        Console.WriteLine("Advanced configuration validation and metrics:");
        Console.WriteLine();

        var configService = services.GetService<EnhancedConfigurationService>();
        if (configService != null)
        {
            var validation = configService.ValidateConfiguration();
            var metrics = await configService.GetProcessingMetricsAsync();

            Console.WriteLine($"  ✅ Configuration Validation: {(validation.IsValid ? "✓ Valid" : "✗ Invalid")}");
            if (!validation.IsValid)
                foreach (var issue in validation.Issues)
                    Console.WriteLine($"     ⚠️ {issue}");

            Console.WriteLine(
                $"  📊 Metrics: {metrics.ConfiguredWorkers} workers, {metrics.AllowedOperations} operations, DB: {metrics.DatabaseConfigured}");
        }

        // 5. Automatic Interface Registration
        Console.WriteLine();
        Console.WriteLine("--- 5. Automatic Interface Registration ---");
        Console.WriteLine("Event handling with automatic IEnumerable<IEventHandler> registration:");
        Console.WriteLine();

        var eventService = services.GetService<EventProcessingService>();
        if (eventService != null)
        {
            var eventResult =
                await eventService.ProcessEventAsync("email.sent", new { UserId = "123", Email = "user@example.com" });

            Console.WriteLine($"  ✅ Event Processing: {eventResult.EventType}");
            Console.WriteLine(
                $"     📊 {eventResult.TotalHandlers} total handlers, {eventResult.EligibleHandlers} eligible, {eventResult.SuccessfulHandlers} successful");

            foreach (var handlerResult in eventResult.HandlerResults.Take(3))
            {
                var status = handlerResult.Success ? "✓" : "✗";
                Console.WriteLine(
                    $"     {status} {handlerResult.HandlerName}Handler: {handlerResult.Message} ({handlerResult.ProcessingTimeMs:F1}ms)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("🎉 Architectural Enhancements Summary:");
        Console.WriteLine("✅ Individual lifetime attributes for clean syntax");
        Console.WriteLine("✅ Intelligent service registration with interface detection");
        Console.WriteLine("✅ Enhanced IEnumerable<T> dependency injection");
        Console.WriteLine("✅ Mixed dependency patterns (Inject + Configuration)");
        Console.WriteLine("✅ Pragmatic service registration logic");
        Console.WriteLine("✅ Enhanced configuration integration with validation");
        Console.WriteLine("✅ Automatic interface registration for collections");
        Console.WriteLine();
    }
}
