namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// This file contains diagnostic examples that demonstrate various IoCTools diagnostics.
/// These services will generate warnings/errors when building to help users understand
/// the diagnostic system and how to configure it.
/// 
/// To run these diagnostic examples:
/// 1. Build the project: dotnet build
/// 2. Check the build output for diagnostic messages
/// 3. Configure diagnostic severity in project file (see project comments below)
/// 
/// Example MSBuild properties to configure diagnostics:
/// 
/// <PropertyGroup>
///   <!-- Configure severity for missing implementations (default: Error) -->
///   <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
///
///   <!-- Configure severity for unregistered implementations (default: Error) -->
///   <IoCToolsManualSeverity>Info</IoCToolsManualSeverity>
///   
///   <!-- Disable all dependency validation diagnostics (default: false) -->
///   <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
/// </PropertyGroup>
/// 
/// Severity Options: Error, Warning, Info, Hidden
/// </summary>

// ============================================
// IOC001: Missing Implementation Examples
// ============================================

/// <summary>
///     IOC001: This service depends on interfaces that have no implementations
///     NOTE: No [Lifetime] attribute to prevent runtime failures while demonstrating build-time diagnostics
///     The generator will still create constructor and show diagnostic warnings, but won't register for DI
/// </summary>
// [Scoped] // ← Commented out to exclude from DI registration
[DependsOn<IMissingDataService,INonExistentRepository>(memberName1:"_dataService",memberName2:"_repository")]public partial class MissingImplementationService
{
}

// These interfaces intentionally have no implementations to trigger IOC001
public interface IMissingDataService
{
    Task<string> GetDataAsync();
}

public interface INonExistentRepository
{
    Task SaveAsync(string data);
}

// ============================================
// IOC002: Unregistered Implementation Examples
// ============================================

/// <summary>
///     IOC002: This service depends on implementations that exist but are not registered
///     NOTE: No [Lifetime] attribute to prevent runtime failures while demonstrating build-time diagnostics
///     The generator will still create constructor and show diagnostic warnings, but won't register for DI
/// </summary>
// [Scoped] // ← Commented out to exclude from DI registration
[DependsOn<IUnregisteredCalculator,IUnregisteredLogger>(memberName1:"_calculator",memberName2:"_logger")]public partial class UnregisteredDependencyService
{
}

// Interfaces with implementations that are NOT marked with Service attributes
public interface IUnregisteredCalculator
{
    int Add(int a,
        int b);
}

public interface IUnregisteredLogger
{
    void Log(string message);
}

// These implementations exist but are not registered (no attributes to trigger intelligent inference)
// This will trigger IOC002 diagnostics
public class UnregisteredCalculator : IUnregisteredCalculator
{
    public int Add(int a,
        int b) => a + b;
}

public class UnregisteredLogger : IUnregisteredLogger
{
    public void Log(string message) => Console.WriteLine(message);
}

// ============================================
// IOC012: Singleton Depends on Scoped Examples  
// ============================================
/// <summary>
/// IOC012: This service should trigger IOC012 (Singleton depending on Scoped services)
/// INTENTIONALLY PROBLEMATIC - will cause IOC012 diagnostic
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public partial class ProblematicSingletonService
//{
//    [Inject] private readonly IScopedDatabaseService _database; // IOC012: Singleton depending on Scoped
//}

/// <summary>
///     IOC012: Helper scoped service that the singleton incorrectly depends on
/// </summary>
[Scoped]
[DependsOn<ILogger<ScopedDatabaseService>>]public partial class ScopedDatabaseService : IScopedDatabaseService
{

    public async Task<string> GetUserDataAsync(int userId)
    {
        // Simulate database operation
        await Task.Delay(100);
        return $"User data for {userId}";
    }
}

public interface IScopedDatabaseService
{
    Task<string> GetUserDataAsync(int userId);
}

// ============================================
// IOC013: Singleton Depends on Transient Examples
// ============================================

/// <summary>
///     IOC013: This Singleton service depends on Transient services (potential issue)
/// </summary>
[Singleton]
[DependsOn<ITransientNotificationService,ITransientUtility>(memberName1:"_notifications",memberName2:"_utility")]public partial class SingletonWithTransientDependencies
{
}

/// <summary>
///     IOC013: Transient services that singleton depends on
/// </summary>
[Transient]
public partial class TransientNotificationService : ITransientNotificationService
{
    public void SendNotification(string message)
    {
        // Simulate notification sending
        Console.WriteLine($"Notification: {message}");
    }
}

[Transient]
public partial class TransientUtility : ITransientUtility
{
    public string FormatMessage(string input) => $"[{DateTime.Now}] {input}";
}

public interface ITransientNotificationService
{
    void SendNotification(string message);
}

public interface ITransientUtility
{
    string FormatMessage(string input);
}

// ============================================
// IOC015: Inheritance Chain Lifetime Violations
// ============================================
/// <summary>
/// IOC015: Complex inheritance chain with lifetime violations  
/// INTENTIONALLY PROBLEMATIC - Singleton inheriting from service with Scoped dependencies
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public partial class SingletonServiceWithInheritance : BaseServiceWithScopedDependencies
//{
//    [Inject] private readonly ITransientUtility _additionalUtility;
//}

/// <summary>
///     IOC015: Base class with scoped dependencies (creates inheritance lifetime violation)
/// </summary>
[Scoped]
[DependsOn<IScopedDatabaseService,ILogger<BaseServiceWithScopedDependencies>>(memberName1:"_baseDatabase",memberName2:"_baseLogger")]public partial class BaseServiceWithScopedDependencies
{

    protected virtual void DoBaseWork()
    {
        // Base class functionality
    }
}

// ============================================
// IOC006-IOC009: Registration Conflict Examples
// ============================================

/// <summary>
///     IOC006: Duplicate dependency types in DependsOn attributes
///     IOC008: Duplicate type in single DependsOn attribute
/// </summary>
[Scoped]
[DependsOn<ILogger<ConflictingDependenciesService>>]
[DependsOn<IMemoryCache>]
[DependsOn<ILogger<ConflictingDependenciesService>>] // IOC006: Duplicate across attributes
[DependsOn<IMemoryCache, IMemoryCache>] // IOC008: Duplicate within single attribute
[DependsOn<ILogger<ConflictingDependenciesService>>]public partial class ConflictingDependenciesService
{
}

/// <summary>
///     IOC005: SkipRegistration without RegisterAsAll
///     IOC009: SkipRegistration for non-registered interface
/// </summary>
[Scoped]
[RegisterAsAll] // Fixed: Added registration attribute to resolve IOC004
[SkipRegistration<IDisposable>] // IOC005: No [RegisterAsAll] attribute
public partial class MisconfiguredRegistrationService : IDisposable
{
    public void Dispose()
    {
        // Cleanup
    }
}

/// <summary>
///     IOC009: SkipRegistration for interface not registered by RegisterAsAll
/// </summary>
[Scoped]
[RegisterAsAll]
[SkipRegistration<IDisposable>] // IOC009: IDisposable won't be registered by RegisterAsAll anyway
[NoAutoDepOpen(typeof(ILogger<>))] // Keep constructor parameter-less so Clone() below compiles unchanged under 1.6 auto-deps.
public partial class RedundantSkipRegistrationService : ICloneable
{
    public object Clone() => new RedundantSkipRegistrationService();
}

// ============================================
// IOC032-IOC034: Redundant Attribute Combination Examples
// ============================================

/// <summary>
///     IOC032: RegisterAs covers exactly the interfaces already implemented, so the attribute is redundant
///     The analyzer suggests removing [RegisterAs] unless you need selective registration.
/// </summary>
[RegisterAs<IRedundantSerializer>]
[DependsOn<ILogger<RedundantRegisterAsService>>]public partial class RedundantRegisterAsService : IRedundantSerializer
{

    public void Serialize(string payload) => _logger.LogInformation("Serializing {Payload}", payload);
}

public interface IRedundantSerializer
{
    void Serialize(string payload);
}

/// <summary>
///     IOC033: [Scoped] is redundant because DependsOn already implies service intent and uses Scoped by default.
///     Removing [Scoped] keeps the behavior but avoids the analyzer warning.
/// </summary>
[Scoped]
[DependsOn<IRedundantDependency>]
[DependsOn<ILogger<RedundantScopedWithDependsOnService>>]public partial class RedundantScopedWithDependsOnService : IRedundantScopedWithDependsOnService
{

    public void Execute() => _logger.LogInformation("Executing redundant scoped service");
}

public interface IRedundantScopedWithDependsOnService
{
    void Execute();
}

[Singleton]
public partial class RedundantDependency : IRedundantDependency
{
    public void Run()
    {
    }
}

public interface IRedundantDependency
{
    void Run();
}

/// <summary>
///     IOC034: RegisterAsAll already registers every implemented interface, so extra RegisterAs declarations are ignored.
///     Keep either RegisterAsAll or specific RegisterAs attributes, but not both.
/// </summary>
[RegisterAsAll]
[RegisterAs<IRedundantAllService>]
public partial class RegisterAsAllConflictService : IRedundantAllService
{
    public void Execute()
    {
    }
}

public interface IRedundantAllService
{
    void Execute();
}

// ============================================
// IOC014: Background Service Lifetime Examples
// ============================================

/// <summary>
///     IOC014: Background service with correct lifetime (fixed to be Singleton)
/// </summary>
[Singleton] // IOC014: Fixed - Background services should be Singleton  
[DependsOn<ILogger<IncorrectLifetimeBackgroundService>>]public partial class IncorrectLifetimeBackgroundService : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background service running at: {Time}", DateTimeOffset.Now);
            await Task.Delay(10000, stoppingToken);
        }
    }
}

/// <summary>
/// IOC011: Background service that's not marked as partial but needs dependency injection
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public class NonPartialBackgroundService : BackgroundService // IOC011: Missing 'partial' keyword
//{
//    [Inject] private readonly ILogger<NonPartialBackgroundService> _logger;
//    
//    protected override Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        return Task.CompletedTask;
//    }
//}

// ============================================
// IOC090-IOC094: typeof() Registration Diagnostics
// ============================================

/// <summary>
///     Example configuration method showing typeof() registrations
///     Run this to see IOC090-IOC094 diagnostics
///     </summary>
public static class TypeOfRegistrationExamples
{
    public static void ConfigureTypeOfRegistrations(IServiceCollection services)
    {
        // IOC090: typeof() registration could use IoCTools attributes
        // These implementations have no lifetime attributes
        services.AddScoped(typeof(ISomeInterface), typeof(SomeClassWithoutAttributes));
        services.AddSingleton(typeof(ISingletonService), typeof(SomeClassWithoutAttributes));

        // IOC091: typeof() duplicates IoCTools registration (SameLifetimeService has [Scoped])
        services.AddScoped(typeof(ISameLifetimeInterface), typeof(SameLifetimeService));

        // IOC092: typeof() lifetime mismatch (ScopedService is [Scoped] by IoCTools)
        services.AddTransient(typeof(IScopedInterface), typeof(ScopedService));

        // IOC094: Open generic registration without IoCTools intent - informational only
        services.AddScoped(typeof(ITypeOfRepository<>), typeof(TypeOfRepository<>));
    }
}

// Interfaces and classes for typeof() diagnostic examples

public interface ISomeInterface
{
    void DoWork();
}

/// <summary>
///     No IoCTools attributes - triggers IOC090 when registered via typeof()
///     </summary>
public class SomeClassWithoutAttributes : ISomeInterface
{
    public void DoWork() { }
}

public interface ISingletonService
{
    void DoWork();
}

public interface ISameLifetimeInterface
{
    void DoWork();
}

/// <summary>
///     Has [Scoped] attribute, but typeof() registration duplicates it - triggers IOC091
///     </summary>
[Scoped]
[DependsOn<ILogger<SameLifetimeService>>]public partial class SameLifetimeService : ISameLifetimeInterface
{

    public void DoWork() => _logger.LogInformation("Working");
}

public interface IScopedInterface
{
    void DoWork();
}

/// <summary>
///     Registered as [Scoped] by IoCTools, but typeof() uses Transient - triggers IOC092
///     </summary>
[Scoped]
[DependsOn<ILogger<ScopedService>>]public partial class ScopedService : IScopedInterface
{

    public void DoWork() => _logger.LogInformation("Scoped service working");
}

/// <summary>
///     Open generic interface - triggers IOC094 when registered via typeof() without IoCTools intent
///     </summary>
public interface ITypeOfRepository<T>
{
    Task<T?> GetByIdAsync(int id);
}

/// <summary>
///     Open generic implementation - triggers IOC094 when registered via typeof() without IoCTools intent
///     </summary>
public class TypeOfRepository<T> : ITypeOfRepository<T>
{
    public Task<T?> GetByIdAsync(int id) => Task.FromResult<T?>(default);
}

// ============================================
// IOC020-IOC027: Advanced Conditional Service and System Diagnostics
// ============================================
/// <summary>
/// IOC021: ConditionalService attribute requires Service attribute
/// This class has //[ConditionalService] but is missing  attribute
/// </summary>
////[ConditionalService(Environment = "Development")]
// Missing  attribute intentionally to trigger IOC021
//public partial class ConditionalWithoutServiceAttribute
//{
//    [Inject] private readonly ILogger<ConditionalWithoutServiceAttribute> _logger;
//}

/// <summary>
///     IOC022: ConditionalService attribute has no conditions
///     This class has //[ConditionalService] but no Environment, ConfigValue, or condition properties
/// </summary>
//[ConditionalService] // No conditions specified - triggers IOC022
[DependsOn<ILogger<EmptyConditionalService>>]public partial class EmptyConditionalService
{
}

/// <summary>
///     IOC023: ConfigValue specified without Equals or NotEquals
///     This class has ConfigValue but no comparison condition
/// </summary>
//[ConditionalService(ConfigValue = "Features:SomeFeature")] // Missing Equals/NotEquals - triggers IOC023
[DependsOn<ILogger<ConfigValueWithoutComparisonService>>]public partial class ConfigValueWithoutComparisonService
{
}

/// <summary>
///     IOC024: Equals or NotEquals specified without ConfigValue
///     This class has comparison conditions but no ConfigValue
/// </summary>
//[ConditionalService(Equals = "true")] // Missing ConfigValue - triggers IOC024
[DependsOn<ILogger<ComparisonWithoutConfigValueService>>]public partial class ComparisonWithoutConfigValueService
{
}

/// <summary>
///     IOC025: ConfigValue is empty or whitespace
///     This class has empty ConfigValue
/// </summary>
//[ConditionalService(ConfigValue = "", Equals = "true")] // Empty ConfigValue - triggers IOC025
[DependsOn<ILogger<EmptyConfigValueService>>]public partial class EmptyConfigValueService
{
}

/// <summary>
///     Another IOC025 example with whitespace-only ConfigValue
/// </summary>
//[ConditionalService(ConfigValue = "   ", Equals = "true")] // Whitespace ConfigValue - triggers IOC025
[DependsOn<ILogger<WhitespaceConfigValueService>>]public partial class WhitespaceConfigValueService
{
}

/// <summary>
///     IOC026: Multiple ConditionalService attributes on same class
///     This class has multiple //[ConditionalService] attributes which may lead to unexpected behavior
///     Note: C# compiler temporarily blocks this, so one attribute is commented out
/// </summary>
//[ConditionalService(Environment = "Development")]
// //[ConditionalService(ConfigValue = "Features:EnableDev", Equals = "true")] // Multiple attributes - triggers IOC026
[DependsOn<ILogger<MultipleConditionalAttributesService>>]public partial class MultipleConditionalAttributesService
{
}

/// <summary>
///     IOC027: Potential duplicate service registration scenarios
///     This demonstrates services that may be registered multiple times due to inheritance or attribute combinations
/// </summary>

// Base service with inheritance chain that could lead to duplicate registrations
[DependsOn<ILogger<BaseServiceForDuplication>>]public partial class BaseServiceForDuplication
{
}

// Derived service that might cause duplicate registration warnings  

[DependsOn<IMemoryCache>(memberName1:"_cache")]public partial class DerivedServiceCausingDuplication : BaseServiceForDuplication
{
}

// Service with complex registration patterns that could trigger IOC027
[RegisterAsAll] // Complex registration pattern might trigger IOC027 
[DependsOn<ILogger<ComplexRegistrationPatternService>>]public partial class ComplexRegistrationPatternService : IDisposable, ICloneable
{

    /// <summary>
    ///     ICloneable implementation - maintains interface contract by throwing exception.
    ///     Internal code should prefer TryClone() for Result-based error handling.
    public object Clone() =>
        throw new NotSupportedException(
            "Services with dependency injection cannot be cloned. Use the DI container to resolve instances.");

    public void Dispose()
    {
    }


    /// <summary>
    ///     Attempts to clone the service using Result pattern for error handling.
    ///     This is the preferred method for internal code that can handle Result types.
    /// </summary>
    /// <returns>Result indicating success with cloned object, or failure with diagnostic error</returns>
    public CloneResult TryClone()
    {
        return CloneResult.Failure("DI_SERVICE_CLONE_NOT_SUPPORTED",
            "Services with dependency injection cannot be cloned. Use the DI container to resolve instances.");
    }
}

// ============================================
// Additional Diagnostic Validation Examples
// ============================================

/// <summary>
///     Additional examples of diagnostic scenarios that may be encountered
///     These examples test edge cases and complex validation scenarios
/// </summary>
public static class AdditionalDiagnosticScenarios
{
    public static void ExplainDiagnosticScenarios()
    {
        // IOC020: Conditional Service conflicting conditions
        // IOC021: ConditionalService requires Service attribute - demonstrated above
        // IOC022: Empty ConditionalService conditions - demonstrated above
        // IOC023: ConfigValue without Equals/NotEquals - demonstrated above
        // IOC024: Equals/NotEquals without ConfigValue - demonstrated above
        // IOC025: Empty ConfigValue - demonstrated above
        // IOC026: Multiple ConditionalService attributes - demonstrated above
        // IOC027: Duplicate service registration patterns - demonstrated above
    }
}

// ============================================
// Demonstration Service
// ============================================

/// <summary>
///     Service that demonstrates how to properly handle diagnostic examples
/// </summary>
[Scoped]
[DependsOn<ILogger<DiagnosticDemonstrationService>>]public partial class DiagnosticDemonstrationService
{

    public void RunDiagnosticExamples()
    {
        _logger.LogInformation("Running diagnostic examples...");
        _logger.LogInformation("Check build output for diagnostic messages IOC001-IOC094");
        _logger.LogInformation("Configure diagnostic severity using MSBuild properties in the project file");
        _logger.LogInformation("Example: <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>");
    }
}

/// <summary>
///     Result type for clone operations that cannot use standard Result pattern due to interface constraints
/// </summary>
public class CloneResult
{
    private CloneResult(bool isSuccess,
        object? value,
        string errorCode,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public object? Value { get; }
    public string ErrorCode { get; }
    public string ErrorMessage { get; }

    public static CloneResult Success(object value) => new(true, value, string.Empty, string.Empty);

    public static CloneResult Failure(string errorCode,
        string errorMessage) => new(false, null, errorCode, errorMessage);
}
