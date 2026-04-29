// INTENTIONAL [Inject] USAGE — exists to demonstrate IOC095 diagnostic and code fix behavior.
// All other production Sample services migrated to [DependsOn<T>] in 1.6.0.
// This file will be removed in 1.7.0 when IOC095 becomes error-severity.
//
// Each class below is wired with a dedicated comment explaining why it retains [Inject].
// Together these cover the range of patterns that IOC095 + the code fix must handle:
//   1. Simple [Inject] ILogger<T> — the textbook logger pattern (universal auto-dep covers it).
//   2. [Inject][ExternalService] — field flagged as externally-provided.
//   3. [Inject] with a non-logger type needing member-name preservation.
//   4. [Inject] in an inheritance chain via protected base fields.

namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

#pragma warning disable CS0618 // Obsolete [Inject] usage is intentional in this file.
#pragma warning disable IOC095  // Same intent for the IoCTools deprecation diagnostic — also opts out of `migrate-inject`.

// 1. Simple ILogger<T> — the single most common [Inject] pattern.
//    Retained so IOC095 has a clear, minimal reproduction in the Sample project.
[Scoped]
public partial class InjectLoggerOnlyExample
{
    [Inject] private readonly ILogger<InjectLoggerOnlyExample> _logger;

    public void Run() => _logger.LogInformation("Running the [Inject] logger demonstration.");
}

// 2. [Inject] combined with [ExternalService] — the field flags an externally-provided instance.
//    Keeps coverage for the rewriter's handling of combined attributes.
[Scoped]
public partial class InjectWithExternalServiceExample
{
    [Inject] [ExternalService] private readonly IConfiguration _configuration;

    public string? GetConnectionString() => _configuration.GetConnectionString("Default");
}

// 3. Custom member-name preservation — the field name (_cache) differs from any auto-dep default
//    so the code fix must emit [DependsOn<IMemoryCache>(memberName1: "_cache")] to keep the
//    generated constructor parameter aligned with existing usages inside the class.
[Scoped]
public partial class InjectCustomMemberNameExample
{
    [Inject] private readonly IMemoryCache _cache;

    public T? Lookup<T>(string key) where T : class => _cache.Get<T>(key);
}

// 4. Inheritance-chain [Inject] using protected fields on an abstract base.
//    IOC095 intentionally does not fire on non-private fields — these live here as a static
//    reminder that the architectural-limit cases still require manual migration review.
public abstract partial class InjectInheritanceBaseExample
{
    [Inject] protected readonly ILogger<InjectInheritanceBaseExample> Logger;

    protected void LogStart(string operation) => Logger.LogInformation("Start {Operation}", operation);
}

[Scoped]
public partial class InjectInheritanceDerivedExample : InjectInheritanceBaseExample
{
    public void Execute() => LogStart(nameof(Execute));
}

#pragma warning restore IOC095
#pragma warning restore CS0618
