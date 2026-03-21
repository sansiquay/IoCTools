# Migration Guide

Migrating to IoCTools from manual DI registration or other dependency injection containers.

## From Manual DI

### Step 1: Add IoCTools packages

```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator --prerelease
```

### Step 2: Annotate services

Convert manual constructor injection to IoCTools attributes:

**Before:**
```csharp
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }
}

// Registration
services.AddScoped<IUserService, UserService>();
```

**After:**
```csharp
[Scoped]
[DependsOn<IUserRepository, ILogger<UserService>>]
public partial class UserService : IUserService
{
    // Constructor auto-generated, fields auto-generated
}

// One line replaces all registrations
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Step 3: Remove manual registrations

Delete lines like:
```csharp
// DELETE these
services.AddScoped<IUserService, UserService>();
services.AddScoped<IUserRepository, UserRepository>();
services.AddSingleton<ICacheService, CacheService>();
// ... dozens of lines
```

Replace with:
```csharp
// ADD this
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Step 4: Handle configuration

**Before:**
```csharp
// Options class
public class EmailOptions { public string FromAddress { get; set; } }

// Registration
services.Configure<EmailOptions>(configuration.GetSection("Email"));

// Usage
public class EmailService
{
    private readonly EmailOptions _options;
    public EmailService(IOptionsMonitor<EmailOptions> options)
    {
        _options = options.CurrentValue;
    }
}
```

**After:**
```csharp
// No options class needed
[DependsOnConfiguration<string>("Email:FromAddress")]
public partial class EmailService
{
    // _emailFromAddress auto-generated and injected
}
```

### Common Migration Patterns

| Manual DI | IoCTools |
|-----------|----------|
| `services.AddScoped<T, Impl>()` | `[Scoped] public partial class Impl : T` |
| `services.AddSingleton<T>()` | `[Singleton] public partial class T` |
| `IOptionsMonitor<T>` | `[DependsOnConfiguration<T>]` |
| `IConfiguration["key"]` | `[InjectConfiguration("key")]` |
| Manual constructor | `[DependsOn<T1, T2>]` |

---

## From Autofac

### Module registration

**Autofac:**
```csharp
public class ServicesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<UserService>()
            .As<IUserService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<EmailService>()
            .As<IEmailService>()
            .InstancePerLifetimeScope();
    }
}
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

// No modules needed
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Property injection

Autofac's property injection has no direct equivalent in IoCTools. Use constructor injection:

**Autofac:**
```csharp
builder.RegisterType<UserService>()
    .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
```

**IoCTools:**
```csharp
[Scoped]
[DependsOn<ILogger<UserService>, IUserRepository>]
public partial class UserService : IUserService
{
    // All dependencies via constructor (auto-generated)
}
```

---

## From StructureMap

### Registry DSL

**StructureMap:**
```csharp
public class ServiceRegistry : Registry
{
    public ServiceRegistry()
    {
        For<IUserService>().Use<UserService>().Scoped();
        For<IEmailService>().Use<EmailService>().Scoped();
    }
}
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Named Instances

StructureMap named instances use `[RegisterAs]` with multiple interfaces:

**StructureMap:**
```csharp
For<ILogger>().Use<ConsoleLogger>().Named("Console");
For<ILogger>().Use<FileLogger>().Named("File");
```

**IoCTools:**
```csharp
[RegisterAs<ILogger, IConsoleLogger>]
public partial class ConsoleLogger : ILogger, IConsoleLogger { }

[RegisterAs<ILogger, IFileLogger>]
public partial class FileLogger : ILogger, IFileLogger { }
```

---

## From Microsoft.Extensions.DependencyInjection (Manual)

The transition is straightforward since IoCTools uses the same container:

**Remove:**
```csharp
services.AddScoped<IUserService, UserService>();
services.AddScoped<IUserRepository, UserRepository>();
services.AddSingleton<ICacheService, CacheService>();
services.AddHttpClient();
services.Configure<EmailOptions>(configuration.GetSection("Email"));
```

**Add:**
```csharp
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

Add attributes to services, and registration happens automatically.

---

## From DryIoc

### Container setup

**DryIoc:**
```csharp
var container = new Container();
container.Register<IUserService, UserService>(Reuse.Scoped);
container.Register<IEmailService, EmailService>(Reuse.Scoped);
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Resolvable factories

DryIoc's `Func<T>` factories work with Microsoft.Extensions.DependencyInjection:

**DryIoc:**
```csharp
container.RegisterDelegate<IUserFactory>(c => () => c.Resolve<UserService>());
```

**IoCTools:**
```csharp
// Use Microsoft DI's built-in factory support
services.AddScoped<Func<IUserService>>(sp => () => sp.GetRequiredService<IUserService>());
```

---

## Migration Checklist

- [ ] Add IoCTools packages to all service projects
- [ ] Add `partial` modifier to service classes
- [ ] Add lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`)
- [ ] Replace manual constructors with `[DependsOn<>]` or `[Inject]`
- [ ] Replace `IOptions<T>` with `[DependsOnConfiguration<>]`
- [ ] Remove manual service registrations from startup
- [ ] Add `AddYourAssemblyRegisteredServices()` call
- [ ] Build and verify no IOC001/IOC002 diagnostics
- [ ] Run tests to ensure behavior unchanged
- [ ] Commit and deploy

---

## Troubleshooting Migration

### IOC001: No implementation found

**Cause:** Interface has no implementation or implementation lacks lifetime attribute.

**Fix:** Add `[Scoped]`/`[Singleton]`/`[Transient]` to implementation.

### IOC002: Implementation not registered

**Cause:** Implementation exists but no lifetime attribute.

**Fix:** Add lifetime attribute or `[ExternalService]` for manually registered services.

### IOC012: Singleton depends on Scoped

**Cause:** Lifetime mismatch in dependencies.

**Fix:** Change dependency to Singleton or use `IServiceProvider.CreateScope()`.

### IOC080: Service must be partial

**Cause:** Class uses IoCTools attributes but isn't `partial`.

**Fix:** Add `partial` modifier to class declaration.

## Related

- [Getting Started](getting-started.md) — IoCTools introduction
- [Attribute Reference](attributes.md) — All IoCTools attributes
- [Diagnostics Reference](diagnostics.md) — All diagnostic codes with fix guidance

---

**Back to [main README](../README.md)**
