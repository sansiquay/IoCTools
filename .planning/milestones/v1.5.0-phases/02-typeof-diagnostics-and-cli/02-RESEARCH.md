# Phase 02: typeof() Diagnostics and CLI - Research

**Researched:** 2026-03-21
**Domain:** Roslyn source generator diagnostics (typeof() detection), .NET CLI output modes, terminal color
**Confidence:** HIGH

## Summary

This phase has two independent workstreams: (1) extending ManualRegistrationValidator to detect `typeof()`-based DI registrations and emit IOC090-094, and (2) adding CLI infrastructure for `--json`, `--verbose`, color output, wildcard filtering, profile service count, and a new `suppress` command.

The typeof() diagnostics workstream is well-scoped. The existing `ManualRegistrationValidator.ValidateAllTrees()` already walks all syntax trees and inspects `AddScoped/AddSingleton/AddTransient` invocations. It currently short-circuits when `typeArgsSymbol.Length == 0` (line 89), which is precisely the typeof() case. The extension adds an `else` branch that extracts types from `TypeOfExpressionSyntax` arguments. The `RegistrationSummaryBuilder.TryExtractTypeFromArgument()` in the CLI already demonstrates this exact syntax pattern (`typeOfExpr.Type.ToString()`), but the generator needs semantic resolution (`semanticModel.GetTypeInfo(typeOf.Type).Type`) rather than string extraction. Open generic detection (`typeof(IRepository<>)`) uses `OmittedTypeArgumentSyntax` children on `GenericNameSyntax` and `GetSymbolInfo()` instead of `GetTypeInfo()`.

The CLI workstream is broader but each piece is independent. No color infrastructure exists today -- all printers use `Console.WriteLine()` directly. `--json` and `--verbose` are cross-cutting concerns that touch every command's parse+execute flow. The `suppress` command is a new top-level command following the established pattern (parse args, create ProjectContext, execute, print, return exit code).

**Primary recommendation:** Implement typeof() diagnostics first (smaller, isolated, generator-only), then CLI infrastructure (`--json`/`--verbose`/color as cross-cutting), then filtering unification and `suppress` command.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Detect `services.AddScoped(typeof(IFoo), typeof(Foo))` -- the two-argument typeof overload for all three lifetimes
- **D-02:** Also detect `ServiceDescriptor.Scoped(typeof(IFoo), typeof(Foo))` static factory patterns -- same semantic intent, different syntax
- **D-03:** Skip `new ServiceDescriptor(typeof(...), typeof(...), ServiceLifetime.X)` constructor form -- rare in application code, can add later if requested
- **D-04:** Open generic typeof patterns (`typeof(IRepository<>)`) get IOC094 at **Info** severity -- IoCTools doesn't support open generics yet, so no actionable fix path
- **D-05:** All typeof diagnostics (IOC090-094) share the existing `IoCToolsManualSeverity` MSBuild knob -- same concern as IOC081-083, no separate configuration
- **D-06:** `--json` outputs **only** the JSON payload to stdout -- no headers, no summary lines. Warnings/errors go to stderr. Must work with `| jq .`
- **D-07:** `--verbose` writes process diagnostics (MSBuild restore, generator timing, resolved file paths) to **stderr** while keeping command output on stdout. `--json --verbose` works: JSON on stdout, debug info on stderr
- **D-08:** Color auto-detects terminal capability and follows the `NO_COLOR` convention (https://no-color.org/). Color on by default in interactive terminals, off when piped or when `NO_COLOR` env var is set. No explicit `--color`/`--no-color` flags
- **D-09:** Colored elements: diagnostic severity labels (red Error, yellow Warning, cyan Info), lifetime labels (green Singleton, blue Scoped, gray Transient), command headers. Data itself stays uncolored
- **D-10:** Simple wildcards only -- `*` (any chars) and `?` (single char). No regex
- **D-11:** Backward compatible -- bare names without wildcards keep existing exact match + suffix match behavior
- **D-12:** Unify the two divergent filter implementations (`ServiceFieldInspector.MatchesTypeName` and `RegistrationSummaryBuilder.TypeMatchesFilter`) into a single shared `TypeMatchesPattern()` method
- **D-13:** Use wildcard-to-regex conversion internally -- matches the `IoCToolsIgnoredTypePatterns` glob syntax already used in the generator's `GeneratorStyleOptions`
- **D-14:** Match against fully-qualified type name without `global::` prefix
- **D-15:** New top-level command `ioc-tools suppress` -- not a subcommand of `doctor`
- **D-16:** Default filter: `--severity warning,info` (excludes errors from suppression). Optional `--codes IOC035,IOC053` for explicit picks. Both flags can combine
- **D-17:** `--live` flag runs the generator first and suppresses only codes actually firing in the project -- natural follow-on to `doctor`
- **D-18:** Stdout by default for review/piping. `--output .editorconfig` to append with conflict detection (skips already-present rules, prints summary to stderr)
- **D-19:** Per-category groupings with comments -- organized by IoCTools.Lifetime, IoCTools.Registration, etc. with diagnostic title as inline comment
- **D-20:** Errors suppressed via explicit `--codes` get a louder comment: "suppressed explicitly (verify this is intentional)"

### Claude's Discretion
- Exact IOC090-094 message text and descriptions
- Internal structure of the shared `TypeMatchesPattern()` utility
- Color implementation approach (ANSI escape sequences vs Console.ForegroundColor)
- `suppress` command argument parsing details and edge case handling
- How `--verbose` timing information is formatted
- JSON schema structure for each command's `--json` output
- WhyPrinter fuzzy suggestion extraction for CLI-04

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DIAG-01 | typeof() argument parsing foundation added to ManualRegistrationValidator | ManualRegistrationValidator analysis (line 89 early return is the extension point); TypeOfExpressionSyntax.Type pattern; Pitfall 1 prevention |
| DIAG-02 | IOC090 -- typeof() interface-implementation registration could use IoCTools | Follows IOC086 pattern: type not in serviceLifetimes map, emit suggestion diagnostic |
| DIAG-03 | IOC091 -- typeof() registration duplicates IoCTools registration | Follows IOC081 pattern: same lifetime match against serviceLifetimes |
| DIAG-04 | IOC092 -- typeof() registration lifetime mismatch | Follows IOC082 pattern: different lifetime in serviceLifetimes |
| DIAG-05 | IOC094 -- Open generic typeof() could use IoCTools attributes | OmittedTypeArgumentSyntax detection; GetSymbolInfo for unbound generics; Info severity |
| DIAG-06 | Integration tests for all typeof() diagnostics | ManualRegistrationOverlapTests pattern; SourceGeneratorTestHelper.CompileWithGenerator |
| DIAG-07 | typeof() diagnostic examples added to sample project | DiagnosticExamples.cs extension; ManualServiceExamples.cs patterns |
| CLI-01 | --verbose flag for debugging | stderr-based diagnostic output; Stopwatch timing; cross-cutting flag in CommonOptions |
| CLI-02 | --json output mode for all commands | GraphPrinter JSON pattern (JsonSerializer.Serialize); stdout-only payload; stderr for errors |
| CLI-03 | Color-coded diagnostic output by severity | ANSI escape sequences; NO_COLOR convention; Console.IsOutputRedirected detection |
| CLI-04 | Fuzzy type suggestions extended to all commands | WhyPrinter.GetSuggestions pattern extraction to shared utility |
| CLI-05 | Wildcard/regex support in FilterByType | DiagnosticUtilities.CompileIgnoredTypePatterns pattern; unify MatchesTypeName + TypeMatchesFilter |
| CLI-06 | Service count added to profile command output | ProfilePrinter.Write extension; RegistrationSummaryBuilder.Build for count |
| CLI-07 | .editorconfig recipe generation for suppressing IoCTools diagnostics | New SuppressPrinter + ParseSuppress; DiagnosticRunner.RunAsync for --live mode |
</phase_requirements>

## Architecture Patterns

### typeof() Detection Extension Point

The existing `ManualRegistrationValidator.ValidateAllTrees()` processes invocations of `AddScoped/AddSingleton/AddTransient` on `IServiceCollection`. Currently (line 86-89), it extracts types exclusively from generic type arguments:

```csharp
var typeArgsSymbol = methodSymbol.TypeArguments;
if (typeArgsSymbol.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
    typeArgsSymbol = methodSymbol.ReducedFrom.TypeArguments;
if (typeArgsSymbol.Length == 0) continue; // <-- THIS is the typeof() escape hatch
```

The extension adds an `else` branch before `continue` that checks `invocation.ArgumentList.Arguments` for `TypeOfExpressionSyntax` nodes. The pattern:

```csharp
// After typeArgsSymbol.Length == 0 check, instead of continue:
if (typeArgsSymbol.Length == 0)
{
    // typeof() argument path
    var args = invocation.ArgumentList.Arguments;
    if (args.Count < 1) continue;

    var svcType = ExtractTypeFromTypeOf(args[0], semanticModel);
    var implType = args.Count >= 2
        ? ExtractTypeFromTypeOf(args[1], semanticModel)
        : svcType;

    if (svcType == null || implType == null) continue;

    // Check for open generics first
    if (IsOpenGenericTypeOf(args[0]) || (args.Count >= 2 && IsOpenGenericTypeOf(args[1])))
    {
        // IOC094: open generic typeof
        // ... emit diagnostic
        continue;
    }

    // Reuse existing IOC081/082/086 logic with svcType/implType
}
```

### ServiceDescriptor Static Factory Detection (D-02)

`ServiceDescriptor.Scoped(typeof(IFoo), typeof(Foo))` uses a different method target. The validator must also match:
- `containing` includes `Microsoft.Extensions.DependencyInjection.ServiceDescriptor`
- Method names: `Scoped`, `Singleton`, `Transient` (static factory methods on ServiceDescriptor)

This means the method name check expands from `AddScoped/AddSingleton/AddTransient` to also include `Scoped/Singleton/Transient` when the containing type is `ServiceDescriptor`.

### CLI Cross-Cutting Architecture

`--json` and `--verbose` are cross-cutting concerns. The cleanest approach:

1. Add `bool Json` and `bool Verbose` to `CommonOptions` record
2. Parse `--json` and `--verbose` as flags in `CommandLineParser.IsFlag()` and `NormalizeKey()`
3. Create an `OutputContext` utility that wraps stdout/stderr behavior:
   - When `--json` is active, human-readable output methods become no-ops and JSON is emitted at the end
   - When `--verbose` is active, debug messages go to `Console.Error`

```
Program.cs
  |
  CommonOptions (+ Json, Verbose flags)
  |
  OutputContext.Create(commonOptions)
  |
  Each command receives OutputContext
  |
  Printers check OutputContext.IsJson to switch behavior
```

### Color Implementation

Use ANSI escape sequences directly. This is the standard approach for .NET CLI tools and works cross-platform:

```csharp
internal static class AnsiColor
{
    private static bool _enabled = !Console.IsOutputRedirected
        && Environment.GetEnvironmentVariable("NO_COLOR") == null;

    public static string Red(string text) => _enabled ? $"\x1b[31m{text}\x1b[0m" : text;
    public static string Yellow(string text) => _enabled ? $"\x1b[33m{text}\x1b[0m" : text;
    public static string Cyan(string text) => _enabled ? $"\x1b[36m{text}\x1b[0m" : text;
    public static string Green(string text) => _enabled ? $"\x1b[32m{text}\x1b[0m" : text;
    public static string Blue(string text) => _enabled ? $"\x1b[34m{text}\x1b[0m" : text;
    public static string Gray(string text) => _enabled ? $"\x1b[90m{text}\x1b[0m" : text;
}
```

Key: `Console.IsOutputRedirected` returns `true` when stdout is piped. Combined with `NO_COLOR` env var check, this satisfies D-08 without any flags.

### Wildcard Filtering Unification

Two divergent filter implementations exist:

1. **`ServiceFieldInspector.MatchesTypeName()`** (line 150-157): Matches `symbol.Name`, fully-qualified format, or `TypeFormat` display string against exact filter string
2. **`RegistrationSummaryBuilder.TypeMatchesFilter()`** (line 182-198): Matches `typeName` via exact match or `.{filter}` suffix

Unified approach (shared `TypeMatchesPattern()` in a new `TypeFilterUtility` static class):

```csharp
internal static class TypeFilterUtility
{
    public static bool Matches(string? typeName, string pattern)
    {
        if (typeName == null || string.IsNullOrWhiteSpace(pattern)) return false;

        // Strip global:: prefix (D-14)
        if (typeName.StartsWith("global::"))
            typeName = typeName.Substring("global::".Length);

        // If no wildcards, use legacy behavior (D-11)
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return ExactOrSuffixMatch(typeName, pattern);

        // Wildcard-to-regex (D-13), reusing DiagnosticUtilities algorithm
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(typeName, regexPattern, RegexOptions.None);
    }

    private static bool ExactOrSuffixMatch(string typeName, string filter)
    {
        if (string.Equals(typeName, filter, StringComparison.Ordinal)) return true;
        // Short name match: "MyService" matches "Namespace.MyService"
        if (typeName.EndsWith("." + filter, StringComparison.Ordinal)) return true;
        // Name-only match (no namespace in filter)
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0 && string.Equals(typeName.Substring(lastDot + 1), filter, StringComparison.Ordinal))
            return true;
        return false;
    }
}
```

### Suppress Command Architecture

```
ioc-tools suppress --project <csproj> [--severity warning,info] [--codes IOC035,IOC053] [--live] [--output .editorconfig]
```

Flow:
1. Parse args via `CommandLineParser.ParseSuppress()`
2. Build diagnostic catalog (static) or run generator (if `--live`)
3. Filter diagnostics by `--severity` and/or `--codes`
4. Generate `.editorconfig` content grouped by category
5. Output to stdout or append to `--output` file

Catalog source: `DiagnosticDescriptors` static fields -- all IOC descriptors are `DiagnosticDescriptor` instances with Id, Severity, Category available. The `suppress` command can use reflection or a static catalog to enumerate them. For `--live`, reuse `DiagnosticRunner.RunAsync()` to get only codes that actually fired.

### Recommended File Structure

```
IoCTools.Tools.Cli/
  CommandLine/
    CommandLineParser.cs          # Add ParseSuppress(), --json/--verbose flags
    CommandOptions.cs             # New: SuppressCommandOptions record (or add to existing)
  Utilities/
    AnsiColor.cs                  # NEW: ANSI escape color utility
    TypeFilterUtility.cs          # NEW: Unified wildcard filter
    OutputContext.cs              # NEW: --json/--verbose output routing
    DiagnosticCatalog.cs          # NEW: Static catalog of all IOC descriptors
    SuppressPrinter.cs            # NEW: .editorconfig generation
    DoctorPrinter.cs              # MODIFY: Add color support
    GraphPrinter.cs               # MODIFY: Add --json passthrough
    ProfilePrinter.cs             # MODIFY: Add service count
    WhyPrinter.cs                 # MODIFY: Extract fuzzy suggestion to shared utility
  Program.cs                      # Add "suppress" case, wire OutputContext
  ServiceFieldInspector.cs        # MODIFY: Use TypeFilterUtility
  RegistrationSummaryBuilder.cs   # MODIFY: Use TypeFilterUtility

IoCTools.Generator/IoCTools.Generator/
  Generator/Diagnostics/
    Validators/ManualRegistrationValidator.cs  # MODIFY: Add typeof() detection
    Descriptors/RegistrationDiagnostics.cs     # ADD: IOC090-094 descriptors
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Wildcard matching | Custom string parsing | Regex from `Regex.Escape(pattern).Replace("\\*", ".*")` | Already proven in `DiagnosticUtilities.CompileIgnoredTypePatterns()`; edge cases handled |
| JSON serialization | Manual string building | `System.Text.Json.JsonSerializer.Serialize()` | Already used in `GraphPrinter`; handles escaping, nesting, arrays correctly |
| Terminal color detection | Manual platform checks | `Console.IsOutputRedirected` + `NO_COLOR` env var | Standard approach; works on Windows Terminal, iTerm, Linux terminals |
| typeof() type extraction | String parsing of syntax text | `semanticModel.GetTypeInfo(typeOfExpr.Type).Type` | String parsing fails for aliases, nested types, generics; semantic model resolves all |
| .editorconfig syntax | Custom format guessing | `dotnet_diagnostic.{id}.severity = {level}` | Standard .editorconfig analyzer rule format; well-documented |

## Common Pitfalls

### Pitfall 1: GetTypeInfo on Wrong Syntax Node for typeof()
**What goes wrong:** Calling `GetTypeInfo()` on `TypeOfExpressionSyntax` returns `System.Type`, not the inner type.
**How to avoid:** Navigate to `TypeOfExpressionSyntax.Type` first, then call `GetTypeInfo()` on that inner syntax node.
**Warning signs:** All typeof-based IOC diagnostics silently fail to fire; IOC086 fires instead.

### Pitfall 2: Open Generic typeof() Has OmittedTypeArgumentSyntax
**What goes wrong:** `typeof(IRepository<>)` contains `OmittedTypeArgumentSyntax`, and `GetTypeInfo()` returns null/error type.
**How to avoid:** Check for `GenericNameSyntax` with `OmittedTypeArgumentSyntax` children. Use `GetSymbolInfo()` for the `INamedTypeSymbol` (with `IsUnboundGenericType == true`). Route to IOC094 exclusively.
**Warning signs:** Tests with open generic typeof patterns crash or emit wrong diagnostics.
**Confidence:** MEDIUM -- needs spike validation per STATE.md blocker.

### Pitfall 3: --json Output Corruption from Mixed stdout Writes
**What goes wrong:** Printers write human-readable text to `Console.WriteLine()` (stdout). Adding `--json` means the same stdout must emit only valid JSON. If any printer writes non-JSON to stdout, `| jq .` breaks.
**How to avoid:** `OutputContext` must intercept all stdout writes. When `--json` is active, human output goes nowhere (or to stderr). JSON payload is written as a single `Console.WriteLine(json)` at the end of command execution.
**Warning signs:** `ioc-tools services --project X --json | jq .` fails with parse error.

### Pitfall 4: Color Codes in Piped Output
**What goes wrong:** ANSI escape sequences appear as garbage in non-terminal contexts (piped to file, captured in tests).
**How to avoid:** `Console.IsOutputRedirected` check + `NO_COLOR` env var. In tests, `CliTestHost` redirects Console, so `IsOutputRedirected` returns `true` and colors are automatically disabled.
**Warning signs:** Test assertions fail because output contains `\x1b[31m` escape sequences.

### Pitfall 5: ServiceDescriptor Factory Methods Have Different ContainingType
**What goes wrong:** `ServiceDescriptor.Scoped()` is a static method on `Microsoft.Extensions.DependencyInjection.ServiceDescriptor`, not an extension method on `IServiceCollection`. The existing `containing` check (`containing.Contains("Microsoft.Extensions.DependencyInjection")`) would match, but the `name` check (`AddScoped`) would not.
**How to avoid:** Add `Scoped/Singleton/Transient` to the method name check when `ContainingType.Name == "ServiceDescriptor"`.
**Warning signs:** `ServiceDescriptor.Scoped(typeof(IFoo), typeof(Foo))` not detected while `services.AddScoped(typeof(IFoo), typeof(Foo))` works.

### Pitfall 6: .editorconfig Append Duplicates Rules
**What goes wrong:** Running `suppress --output .editorconfig` twice appends duplicate rules.
**How to avoid:** Read existing .editorconfig content, parse for existing `dotnet_diagnostic.IOC*.severity` lines, skip already-present rules. Report skipped rules to stderr.
**Warning signs:** .editorconfig grows with each run; IDE shows duplicate rule warnings.

### Pitfall 7: Wildcard Filter Breaks Backward Compatibility
**What goes wrong:** Existing users pass `--type MyService` and get no matches because the new wildcard code treats it differently.
**How to avoid:** When pattern contains no `*` or `?` characters, fall back to the original exact + suffix match behavior (D-11).
**Warning signs:** Existing CLI test assertions fail after filter unification.

## Code Examples

### typeof() Type Extraction (Correct Pattern)
```csharp
// Source: Roslyn API + existing RegistrationSummaryBuilder.TryExtractTypeFromArgument pattern
static INamedTypeSymbol? ExtractTypeFromTypeOf(ArgumentSyntax arg, SemanticModel semanticModel)
{
    if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
        return null;

    var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
    return typeInfo.Type as INamedTypeSymbol;
}
```

### Open Generic Detection
```csharp
// Source: Roslyn syntax tree structure for typeof(IRepository<>)
static bool IsOpenGenericTypeOf(ArgumentSyntax arg)
{
    if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
        return false;

    return typeOfExpr.Type is GenericNameSyntax generic
        && generic.TypeArgumentList.Arguments.Any(a => a is OmittedTypeArgumentSyntax);
}
```

### DiagnosticDescriptor Pattern (IOC090-094)
```csharp
// Source: Existing RegistrationDiagnostics.cs 8-argument constructor pattern
public static readonly DiagnosticDescriptor TypeOfRegistrationCouldUseAttributes = new(
    "IOC090",
    "typeof() registration could use IoCTools attributes",
    "'{0}' is registered via typeof() as {1}, but the implementation '{2}' lacks IoCTools attributes. Consider adding [{1}] (and [RegisterAs]) instead.",
    "IoCTools.Registration",
    DiagnosticSeverity.Warning,
    true,
    "Prefer IoCTools lifetime attributes over typeof()-based manual registrations.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc090");
```

### .editorconfig Output Format
```ini
# IoCTools diagnostic suppressions
# Generated by: ioc-tools suppress

# IoCTools.Registration
dotnet_diagnostic.IOC035.severity = none  # Inject field can be simplified to DependsOn
dotnet_diagnostic.IOC047.severity = none  # Use params-style attribute arguments

# IoCTools.Lifetime
dotnet_diagnostic.IOC058.severity = none  # Apply lifetime attribute to shared base class
```

### JSON Output Pattern
```csharp
// Source: Existing GraphPrinter.cs JSON serialization
// Each command produces a typed payload object, serialized once at the end
var payload = new { services = records.Select(r => new { r.ServiceType, r.Lifetime, ... }) };
Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Console.ForegroundColor` for colors | ANSI escape sequences | .NET 6+ (2021) | Cross-platform, no global state mutation, works in redirected streams |
| Custom CLI arg parsing | System.CommandLine or custom | N/A | Project uses custom parser; consistent with existing architecture |
| `--no-color` flag | `NO_COLOR` env var convention | 2017+ (no-color.org) | Standard across ecosystems; no per-tool flags needed |

## Open Questions

1. **OmittedTypeArgumentSyntax Spike**
   - What we know: `typeof(IRepository<>)` should contain `OmittedTypeArgumentSyntax` and `GetSymbolInfo()` should return `INamedTypeSymbol` with `IsUnboundGenericType == true`
   - What's unclear: Exact Roslyn behavior confirmed only from documentation, not from a running test
   - Recommendation: Write a spike test early in implementation: compile source with `typeof(IRepository<>)` and inspect the syntax/semantic tree. This is flagged in STATE.md as a blocker. LOW risk of fundamental issue, but behavior should be verified before building IOC094.

2. **JSON Schema Consistency**
   - What we know: Each command has different output structure. `--json` must produce consistent, documented schemas.
   - What's unclear: Whether all commands need identical wrapper (e.g., `{ "command": "services", "data": [...] }`) or just command-specific payloads
   - Recommendation: Each command emits its own shape (services returns array of registrations, doctor returns array of diagnostics, etc.). No wrapper envelope needed -- simpler for `jq` consumers.

3. **Profile Service Count Source**
   - What we know: ProfilePrinter currently only shows timing. CLI-06 requires service count.
   - What's unclear: Whether to count from RegistrationSummaryBuilder (generated extension method parsing) or from ServiceFieldInspector (attribute-based discovery)
   - Recommendation: Use RegistrationSummaryBuilder since it reflects what the generator actually emits (the source of truth for registered services). Requires loading artifacts like the `services` command does.

## Sources

### Primary (HIGH confidence)
- `ManualRegistrationValidator.cs` (line 86-89) -- exact extension point for typeof() detection
- `RegistrationDiagnostics.cs` -- IOC081-086 descriptor patterns to follow for IOC090-094
- `DiagnosticUtilities.CompileIgnoredTypePatterns()` (line 70-98) -- proven wildcard-to-regex algorithm
- `GraphPrinter.cs` -- established JSON output pattern with `JsonSerializer`
- `WhyPrinter.cs` -- fuzzy suggestion implementation to extract for CLI-04
- `RegistrationSummaryBuilder.TryExtractTypeFromArgument()` (line 158-167) -- existing TypeOfExpressionSyntax handling in CLI
- `CommandLineParser.cs` -- established flag parsing pattern (IsFlag, NormalizeKey, TryCollectOptions)
- `CliTestHost.cs` -- test infrastructure for CLI commands with stdout/stderr capture
- `ManualRegistrationOverlapTests.cs` -- test pattern for diagnostic tests

### Secondary (MEDIUM confidence)
- `.planning/research/PITFALLS.md` (Pitfalls 1, 5) -- typeof() and open generic detection strategies
- NO_COLOR convention (https://no-color.org/) -- terminal color standard
- `Console.IsOutputRedirected` -- .NET API for detecting piped output

### Tertiary (LOW confidence)
- `OmittedTypeArgumentSyntax` behavior for open generics -- needs spike test validation

## Metadata

**Confidence breakdown:**
- typeof() diagnostics: HIGH -- extension point is clear, patterns established, test infrastructure exists
- CLI --json/--verbose: HIGH -- established patterns in codebase, standard .NET APIs
- Color output: HIGH -- ANSI escapes are well-understood, NO_COLOR is standard
- Wildcard filtering: HIGH -- algorithm already exists in generator codebase
- Suppress command: HIGH -- follows established command pattern, .editorconfig format is standard
- Open generic detection: MEDIUM -- needs spike validation of OmittedTypeArgumentSyntax behavior

**Research date:** 2026-03-21
**Valid until:** 2026-04-21 (stable domain, no external dependencies changing)
