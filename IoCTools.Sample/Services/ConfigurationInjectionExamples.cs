namespace IoCTools.Sample.Services;

using System.Text.RegularExpressions;

using Abstractions.Annotations;

using IoCTools.Sample.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// COMPREHENSIVE CONFIGURATION INJECTION EXAMPLES
// This file demonstrates all configuration injection patterns supported by IoCTools
// Configuration classes are defined in IoCTools.Sample.Configuration.ConfigurationModels.cs

// === 1. BASIC CONFIGURATION INJECTION FOR PRIMITIVES ===

[Scoped]
[DependsOn<ILogger<DatabaseConnectionService>>]public partial class DatabaseConnectionService
{
    // Inject primitive connection string directly from appsettings.json
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [InjectConfiguration("Database:EnableRetry")]
    private readonly bool _enableRetry;

    [InjectConfiguration("Database:TimeoutSeconds")]
    private readonly int _timeoutSeconds;

    public async Task<bool> TestConnectionAsync()
    {
        _logger.LogInformation("Testing database connection with timeout: {Timeout}s, Retry: {EnableRetry}",
            _timeoutSeconds, _enableRetry);

        // Simulate connection test
        await Task.Delay(100);
        _logger.LogInformation("Connection string configured: {HasConnection}",
            !string.IsNullOrEmpty(_connectionString));
        return true;
    }

    public string GetConnectionInfo() =>
        $"Connection: {_connectionString[..20]}..., Timeout: {_timeoutSeconds}s, Retry: {_enableRetry}";
}

[DependsOn<ILogger<AppInfoService>>]public partial class AppInfoService
{
    // Mix of different primitive types from App section
    [InjectConfiguration("App:Name")] private readonly string _appName;

    [InjectConfiguration("App:IsProduction")]
    private readonly bool _isProduction;
    [InjectConfiguration("App:Price")] private readonly decimal _price;
    [InjectConfiguration("App:Version")] private readonly int _version;

    public void DisplayAppInfo()
    {
        _logger.LogInformation("App: {Name} v{Version}, Production: {IsProduction}, Price: ${Price}",
            _appName, _version, _isProduction, _price);
    }
}

// === 2. SECTION BINDING FOR COMPLEX OBJECTS ===

[Scoped]
[DependsOn<ILogger<ConfigurationEmailService>>]public partial class ConfigurationEmailService
{
    // Inject entire Email section as strongly-typed object
    [InjectConfiguration] private readonly EmailSettings _emailSettings;

    public async Task<bool> SendEmailAsync(string to,
        string subject,
        string body)
    {
        _logger.LogInformation("Sending email via {SmtpHost}:{Port} (SSL: {UseSsl}) from {FromAddress}",
            _emailSettings.SmtpHost, _emailSettings.SmtpPort, _emailSettings.UseSsl, _emailSettings.FromAddress);

        // Simulate email sending
        await Task.Delay(200);
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        return true;
    }

    public EmailSettings GetEmailConfiguration() => _emailSettings;
}

[Scoped]
[DependsOn<ILogger<ConfigurationCacheService>>]public partial class ConfigurationCacheService
{
    // Inject Cache section with nested Redis settings
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;

    public void ConfigureCache()
    {
        _logger.LogInformation("Cache Provider: {Provider}, Expiration: {ExpirationMinutes}min, Max Items: {MaxItems}",
            _cacheSettings.Provider, _cacheSettings.ExpirationMinutes, _cacheSettings.MaxItems);

        if (_cacheSettings.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
            _logger.LogInformation("Redis Connection: {ConnectionString}", _cacheSettings.Redis.ConnectionString);
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        _logger.LogDebug("Getting cache value for key: {Key} using {Provider}", key, _cacheSettings.Provider);
        await Task.Delay(10); // Simulate cache lookup
        return null; // Simplified for demo
    }
}

// === 3. COLLECTIONS AND ARRAYS BINDING ===

[Scoped]
[DependsOn<ILogger<SecurityService>>]public partial class SecurityService
{
    // Inject arrays and lists from configuration
    [InjectConfiguration("Security:AllowedHosts")]
    private readonly string[] _allowedHosts;

    [InjectConfiguration("Security:FeatureFlags")]
    private readonly List<string> _featureFlags;

    [InjectConfiguration("Security:SupportedLanguages")]
    private readonly List<string> _supportedLanguages;

    [InjectConfiguration("Security:TrustedPorts")]
    private readonly List<int> _trustedPorts;

    public bool IsHostAllowed(string host)
    {
        var allowed = _allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("Host {Host} allowed: {Allowed}", host, allowed);
        return allowed;
    }

    public bool IsLanguageSupported(string language)
    {
        var supported = _supportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("Language {Language} supported: {Supported}", language, supported);
        return supported;
    }

    public bool IsFeatureEnabled(string feature)
    {
        var enabled = _featureFlags.Contains(feature, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug("Feature {Feature} enabled: {Enabled}", feature, enabled);
        return enabled;
    }

    public bool IsPortTrusted(int port)
    {
        var trusted = _trustedPorts.Contains(port);
        _logger.LogDebug("Port {Port} trusted: {Trusted}", port, trusted);
        return trusted;
    }

    public void DisplaySecurityConfig()
    {
        _logger.LogInformation(
            "Security Config - Hosts: {HostCount}, Languages: {LanguageCount}, Features: {FeatureCount}, Ports: {PortCount}",
            _allowedHosts.Length, _supportedLanguages.Count, _featureFlags.Count, _trustedPorts.Count);
    }
}

// === 4. OPTIONS PATTERN INTEGRATION ===

[Scoped]
[DependsOn<IOptions<AppSettings>,IOptionsMonitor<HotReloadSettings>,ILogger<OptionsPatternService>,IOptionsSnapshot<ValidationSettings>>(memberName1:"_appOptions",memberName2:"_hotReloadMonitor",memberName3:"_logger",memberName4:"_validationSnapshot")]public partial class OptionsPatternService
{

    public void DemonstrateOptionsPattern()
    {
        // IOptions<T> - Singleton, read once at startup
        var appSettings = _appOptions.Value;
        _logger.LogInformation("App Options: {Name} v{Version}, Production: {IsProduction}",
            appSettings.Name, appSettings.Version, appSettings.IsProduction);

        // IOptionsSnapshot<T> - Scoped, reloaded per request
        var validationSettings = _validationSnapshot.Value;
        _logger.LogInformation("Validation Options: MaxLength: {MaxLength}, MinLength: {MinLength}, Level: {Level}",
            validationSettings.MaxLength, validationSettings.MinLength, validationSettings.Level);

        // IOptionsMonitor<T> - Singleton with change notifications
        var hotReloadSettings = _hotReloadMonitor.CurrentValue;
        _logger.LogInformation("Hot Reload Setting: {Setting}", hotReloadSettings.Setting);
    }

    public void SetupHotReloadNotifications()
    {
        // IOptionsMonitor supports change notifications
        _hotReloadMonitor.OnChange(settings =>
        {
            _logger.LogInformation("Hot reload detected! New value: {Setting}", settings.Setting);
        });
    }
}

// === 5. HOT RELOADING WITH SupportsReloading = true ===

[Scoped]
[DependsOn<ILogger<HotReloadableService>>]public partial class HotReloadableService
{
    [InjectConfiguration("HotReload", SupportsReloading = true)]
    private readonly HotReloadSettings _hotReloadSettings;

    // Configuration injection with hot reloading support
    [InjectConfiguration("Reload:Setting", SupportsReloading = true)]
    private readonly string _reloadableSetting;

    private string _cachedValue = string.Empty;

    public void CheckForConfigurationChanges()
    {
        if (_cachedValue != _reloadableSetting)
        {
            _logger.LogInformation("Configuration changed! Old: '{OldValue}', New: '{NewValue}'",
                _cachedValue, _reloadableSetting);
            _cachedValue = _reloadableSetting;
        }

        _logger.LogInformation("Current hot reloadable setting: {Setting}", _hotReloadSettings.Setting);
    }

    public string GetCurrentValue() => _reloadableSetting;
}

// === 6. DEFAULT VALUES AND REQUIRED CONFIGURATION ===

[Scoped]
[DependsOn<ILogger<ConfigurationValidationService>>]public partial class ConfigurationValidationService
{
    [InjectConfiguration("Validation:DefaultFlag", DefaultValue = "true")]
    private readonly bool _defaultFlag;

    [InjectConfiguration("Validation:DefaultTimeout", DefaultValue = "30")]
    private readonly int _defaultTimeout;

    [InjectConfiguration("Validation:EmptySetting", Required = false)]
    private readonly string _emptySetting;

    // Optional with default values
    [InjectConfiguration("Validation:OptionalSetting", Required = false)]
    private readonly string _optionalSetting;

    // Required configuration with proper validation
    [InjectConfiguration("Validation:RequiredSetting", Required = true)]
    private readonly string _requiredSetting;

    // Configuration with fallback values
    [InjectConfiguration("Validation:SettingWithFallback", DefaultValue = "fallback")]
    private readonly string _settingWithFallback;

    [InjectConfiguration("Validation:WhitespaceSetting", Required = false)]
    private readonly string _whitespaceSetting;

    public void ValidateConfiguration()
    {
        _logger.LogInformation("Required Setting: {Required} (Length: {Length})",
            _requiredSetting, _requiredSetting.Length);

        _logger.LogInformation("Optional Setting: '{Optional}' (IsNull: {IsNull}, IsEmpty: {IsEmpty})",
            _optionalSetting, _optionalSetting is null, string.IsNullOrEmpty(_optionalSetting));

        _logger.LogInformation("Default Values - Timeout: {Timeout}, Flag: {Flag}",
            _defaultTimeout, _defaultFlag);

        _logger.LogInformation(
            "Validation Demo - TestValue: '{TestValue}', Empty: '{Empty}', Whitespace: '{Whitespace}'",
            _settingWithFallback, _emptySetting, _whitespaceSetting);
    }

    public bool IsConfigurationValid() => !string.IsNullOrWhiteSpace(_requiredSetting);
}

// === 7. MIXED CONFIGURATION INJECTION WITH REGULAR DI ===

public interface IConfigurationNotificationProvider
{
    Task SendNotificationAsync(string message);
}

[Scoped]
[DependsOn<ILogger<ConfigurationEmailNotificationProvider>>]public partial class ConfigurationEmailNotificationProvider : IConfigurationNotificationProvider
{
    [InjectConfiguration] private readonly EmailSettings _emailSettings;

    public async Task SendNotificationAsync(string message)
    {
        _logger.LogInformation("Sending email notification via {SmtpHost}", _emailSettings.SmtpHost);
        await Task.Delay(100); // Simulate sending
    }
}

[Scoped]
[DependsOn<ConfigurationCacheService,ConfigurationEmailService,ILogger<ComprehensiveBusinessService>,IConfigurationNotificationProvider>(memberName1:"_cacheService",memberName2:"_emailService",memberName3:"_logger",memberName4:"_notificationProvider")]public partial class ComprehensiveBusinessService
{
    [InjectConfiguration("App:Name")] private readonly string _appName;

    // Mix of configuration injection and regular DI
    [InjectConfiguration] private readonly DatabaseSettings _dbSettings;
    [InjectConfiguration] private readonly FeatureFlags _features;

    [InjectConfiguration("App:IsProduction")]
    private readonly bool _isProduction;

    public async Task ProcessBusinessOperationAsync(string operationId)
    {
        _logger.LogInformation("Processing operation {OperationId} in {AppName} (Production: {IsProduction})",
            operationId, _appName, _isProduction);

        // Use configuration-based decisions
        if (_features.EnableAdvancedLogging)
            _logger.LogInformation("Advanced logging enabled for operation {OperationId}", operationId);

        // Use database with configured settings
        _logger.LogInformation("Using database provider: {Provider} with {Timeout}s timeout",
            _dbSettings.Provider, _dbSettings.TimeoutSeconds);

        // Use injected services
        await _cacheService.GetAsync<object>($"operation:{operationId}");

        if (_features.NewPaymentProcessor == "enabled")
            await _notificationProvider.SendNotificationAsync(
                $"Operation {operationId} completed with new payment processor");

        // Use email service
        await _emailService.SendEmailAsync("admin@example.com", "Operation Complete",
            $"Operation {operationId} finished");
    }

    public void DisplayConfiguration()
    {
        _logger.LogInformation("=== COMPREHENSIVE CONFIGURATION DISPLAY ===");
        _logger.LogInformation("App: {AppName}, Production: {IsProduction}", _appName, _isProduction);
        _logger.LogInformation("Database: {Provider}, Connection: {Connection}",
            _dbSettings.Provider, _dbSettings.ConnectionString[..20] + "...");
        _logger.LogInformation("Features - AdvancedLogging: {AdvancedLogging}, PaymentProcessor: {PaymentProcessor}",
            _features.EnableAdvancedLogging, _features.NewPaymentProcessor);
    }
}

// === ADVANCED CONFIGURATION PATTERNS ===

[Scoped]
[DependsOn<ILogger<NestedConfigurationService>>]public partial class NestedConfigurationService
{
    [InjectConfiguration("Logging:Console:Enabled")]
    private readonly bool _consoleEnabled;

    [InjectConfiguration("Logging:Console:Format")]
    private readonly string _consoleFormat;

    // Demonstrate deeply nested configuration paths
    [InjectConfiguration("Logging:Custom:Level")]
    private readonly string _customLogLevel;

    [InjectConfiguration("Logging:Custom:IncludeTimestamp")]
    private readonly bool _includeTimestamp;

    [InjectConfiguration("Logging:File:Path")]
    private readonly string _logFilePath;

    [InjectConfiguration("Logging:File:MaxSizeMB")]
    private readonly int _maxLogSize;

    public void ConfigureCustomLogging()
    {
        _logger.LogInformation("Custom Logging Configuration:");
        _logger.LogInformation("- Level: {Level}, Timestamp: {Timestamp}", _customLogLevel, _includeTimestamp);
        _logger.LogInformation("- File: {FilePath} (Max: {MaxSize}MB)", _logFilePath, _maxLogSize);
        _logger.LogInformation("- Console: {Enabled} ({Format})", _consoleEnabled, _consoleFormat);
    }
}

[Scoped]
[DependsOn<ILogger<ConfigurationArrayService>>]public partial class ConfigurationArrayService
{
    [InjectConfiguration("Validation:AllowedCharacters")]
    private readonly string _allowedCharacters;

    [InjectConfiguration("Validation:MaxLength")]
    private readonly int _maxLength;

    // Complex array/list configurations
    [InjectConfiguration("Validation:Patterns")]
    private readonly List<string> _validationPatterns;

    public bool ValidateInput(string input)
    {
        if (input.Length > _maxLength)
        {
            _logger.LogWarning("Input too long: {Length} > {MaxLength}", input.Length, _maxLength);
            return false;
        }

        if (input.Any(c => !_allowedCharacters.Contains(c)))
        {
            _logger.LogWarning("Input contains invalid characters");
            return false;
        }

        foreach (var pattern in _validationPatterns)
            if (Regex.IsMatch(input, pattern))
            {
                _logger.LogDebug("Input matches validation pattern: {Pattern}", pattern);
                return true;
            }

        _logger.LogWarning("Input doesn't match any validation patterns");
        return false;
    }
}

// === DEMONSTRATION RUNNER SERVICE ===

[Scoped]
[DependsOn<ConfigurationArrayService,ComprehensiveBusinessService,ConfigurationCacheService,DatabaseConnectionService,ConfigurationEmailService,HotReloadableService,ILogger<ConfigurationDemoRunner>,NestedConfigurationService,OptionsPatternService,SecurityService,ConfigurationValidationService>(memberName1:"_arrayService",memberName2:"_businessService",memberName3:"_cacheService",memberName4:"_databaseService",memberName5:"_emailService",memberName6:"_hotReloadService",memberName7:"_logger",memberName8:"_nestedService",memberName9:"_optionsService",memberName10:"_securityService",memberName11:"_validationService")]public partial class ConfigurationDemoRunner
{

    public async Task RunAllConfigurationDemosAsync()
    {
        _logger.LogInformation("=== RUNNING CONFIGURATION INJECTION DEMOS ===");

        // 1. Basic primitives
        _logger.LogInformation("\n1. Testing basic configuration injection:");
        await _databaseService.TestConnectionAsync();
        _logger.LogInformation("Database info: {Info}", _databaseService.GetConnectionInfo());

        // 2. Section binding
        _logger.LogInformation("\n2. Testing section binding:");
        await _emailService.SendEmailAsync("test@example.com", "Demo", "Configuration test");
        _cacheService.ConfigureCache();

        // 3. Collections
        _logger.LogInformation("\n3. Testing collections and arrays:");
        _securityService.DisplaySecurityConfig();
        _logger.LogInformation("localhost allowed: {Allowed}", _securityService.IsHostAllowed("localhost"));
        _logger.LogInformation("NewPaymentProcessor enabled: {Enabled}",
            _securityService.IsFeatureEnabled("NewPaymentProcessor"));

        // 4. Options pattern
        _logger.LogInformation("\n4. Testing Options pattern:");
        _optionsService.DemonstrateOptionsPattern();
        _optionsService.SetupHotReloadNotifications();

        // 5. Hot reloading
        _logger.LogInformation("\n5. Testing hot reload:");
        _hotReloadService.CheckForConfigurationChanges();

        // 6. Validation
        _logger.LogInformation("\n6. Testing configuration validation:");
        _validationService.ValidateConfiguration();
        _logger.LogInformation("Configuration valid: {Valid}", _validationService.IsConfigurationValid());

        // 7. Mixed injection
        _logger.LogInformation("\n7. Testing mixed configuration + DI:");
        _businessService.DisplayConfiguration();
        await _businessService.ProcessBusinessOperationAsync("DEMO-001");

        // 8. Advanced patterns
        _logger.LogInformation("\n8. Testing advanced patterns:");
        _nestedService.ConfigureCustomLogging();
        _logger.LogInformation("Email validation: {Valid}", _arrayService.ValidateInput("test@example.com"));
        _logger.LogInformation("Invalid validation: {Valid}", _arrayService.ValidateInput("invalid-email"));

        _logger.LogInformation("\n=== CONFIGURATION DEMOS COMPLETED ===");
    }
}
