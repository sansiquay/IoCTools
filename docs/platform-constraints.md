# Platform Constraints

IoCTools targets `netstandard2.0` for broad compatibility. This document explains what that means for your projects.

## Key Distinction

**The generator** targets `netstandard2.0` internally. **Your service code** can use any .NET version and C# features.

| What | Target | Constraints |
|------|--------|-------------|
| IoCTools.Generator (analyzer package) | netstandard2.0 | Limited to netstandard2.0 APIs |
| Your service code | Any .NET version | No constraints from IoCTools |
| IoCTools.Abstractions (attributes) | netstandard2.0 | Works on all .NET versions |

## What This Means for You

### Supported .NET Versions

Your projects using IoCTools can target:

- **.NET Framework** 4.6.1+
- **.NET Core** 2.0+
- **.NET 5+**
- **.NET 6+**
- **.NET 7+**
- **.NET 8+**
- **.NET 9+**

All modern C# language features are supported in your service code.

### C# Language Features in Your Services

Your service classes can use **any** C# features:

- Record types (`record class`, `record struct`)
- Init-only properties (`init` accessor)
- Required members (`required` modifier)
- Nullable reference types
- Pattern matching enhancements
- Primary constructors
- File-scoped namespaces
- Any future C# features

**The generator reads your code via Roslyn** — it doesn't execute your code, so it can analyze any C# syntax.

## Generator Limitations (Internal Only)

The IoCTools **generator** is limited to netstandard2.0 APIs. This affects:

1. **Generator code only** — Not your service code
2. **Roslyn APIs** — We use `Microsoft.CodeAnalysis.CSharp 4.5.0`
3. **No runtime execution** — Generator runs at compile time only

### Features Not Available in Generator

These features are **not available in the generator code internally**, but this doesn't affect you:

| Feature | Unavailable in Generator | Impact on You |
|---------|------------------------|---------------|
| Record types | `record struct` cannot be used | None — you use records in services freely |
| `HashCode.Combine()` | Not in netstandard2.0 | None — we use manual hash code implementation |
| `Span<T>` / `Memory<T>` | Limited availability | None — no performance impact on your code |
| `init` properties | Not available | None — internal implementation detail |

### Workarounds Used in Generator

The IoCTools generator uses standard patterns to work around netstandard2.0 limitations:

**Manual hash code (instead of `HashCode`):**
```csharp
// In generator (internal code)
public override int GetHashCode()
{
    return (Name?.GetHashCode() * 397) ^ (Lifetime?.GetHashCode() * 113);
}
```

**Regular classes (instead of records):**
```csharp
// In generator (internal code)
internal readonly struct ServiceClassInfo
{
    public INamedTypeSymbol ClassSymbol { get; }
    // ... manual implementation instead of record
}
```

These are **implementation details** that don't affect your service code.

## Cross-Assembly Scenarios

### Clean Architecture

IoCTools supports clean architecture where interfaces are in a separate assembly:

```csharp
// MyProject.Abstractions (separate assembly)
public interface IUserService { }

// MyProject.Services (references Abstractions)
[Scoped]
public partial class UserService : IUserService { }
```

**Configure ignored patterns:**
```xml
<PropertyGroup>
  <IoCToolsIgnoredTypePatterns>*.Abstractions.*;*.Contracts.*;*.Interfaces.*</IoCToolsIgnoredTypePatterns>
</PropertyGroup>
```

This tells IoCTools: "Don't warn about IOC001 for interfaces matching these patterns."

### External Dependencies

For dependencies registered by frameworks or third-party code:

```csharp
[ExternalService]
private readonly IHttpContextAccessor _accessor;
```

Or mark external implementations:
```csharp
[ExternalService]
public partial class FrameworkService : IFrameworkService { }
```

## Framework-Specific Notes

### ASP.NET Core

IoCTools works with all ASP.NET Core versions:

- Controllers (MVC)
- Minimal APIs
- Razor Pages
- Blazor
- gRPC services
- Background services (`IHostedService`)

**Default skip:** `ControllerBase` is skipped by default (not registered as a service).

### EF Core

`DbContext` classes should be registered via `AddDbContext`, not IoCTools:

```csharp
// Register EF Core normally
builder.Services.AddDbContext<MyDbContext>(options => ...);

// For DI-aware services using the context:
[RegisterAs<ITransactionService>(InstanceSharing.Shared)]
public partial class MyDbContext : DbContext, ITransactionService
{
    // No [Scoped] — registered by AddDbContext
}
```

### Blazor Server

Scoped services in Blazor Server require `IServiceProvider.CreateScope()` for components:

```csharp
// IOC012 may trigger for Singleton → Scoped in Blazor
[Scoped]
public partial class UserService
{
    // Works correctly in Blazor with proper scoping
}
```

## Performance Considerations

### Build Performance

IoCTools uses incremental generation for fast builds:

- **First build:** Full analysis (200-500ms for typical projects)
- **Incremental builds:** < 50ms for unchanged files
- **Diagnostics run:** Parallel validation across all services

### Runtime Performance

**Zero runtime overhead:**

- No reflection at startup
- No assembly scanning
- Generated code is plain C#
- Same performance as hand-written DI registration

## Version Compatibility

| IoCTools Version | .NET SDK | Roslyn Version |
|------------------|----------|----------------|
| 1.5.0 | .NET SDK 9.0.100+ | Microsoft.CodeAnalysis 4.5.0 |

**Minimum:** .NET SDK 6.0.100+ (for tooling)

## Troubleshooting

### "My .csproj is not recognized"

Ensure you're using a supported project type:
- SDK-style .csproj (.NET Core / .NET 5+)
- Modern .NET Framework project with SDK-style format
- Old .csproj format (upgrade to SDK-style)

### "Attribute not found"

Ensure both packages are referenced:
```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

### "Generated code not visible"

Generated files are in `obj/Debug/netX.X/generated/`:
```xml
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
<CompilerGeneratedFilesOutputPath>generated</CompilerGeneratedFilesOutputPath>
```

Or use the CLI:
```bash
ioc-tools fields-path --project MyProject.csproj
```

## Related

- [Getting Started](getting-started.md) — Introduction to IoCTools
- [Configuration](configuration.md) — MSBuild properties and ignored patterns
- [CLI Reference](cli-reference.md) — Debugging tools
- [Migration Guide](migration.md) — Migrating from other containers

---

**Back to [main README](../README.md)**
