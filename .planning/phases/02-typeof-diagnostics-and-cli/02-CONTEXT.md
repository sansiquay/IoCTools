# Phase 2: typeof() Diagnostics and CLI - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Add typeof() registration diagnostics (IOC090-094) and extend CLI with --json, --verbose, color output, filtering improvements, and a new `suppress` command for .editorconfig generation. No new service registration features — this is diagnostic expansion and CLI UX improvement.

</domain>

<decisions>
## Implementation Decisions

### typeof() Detection Scope
- **D-01:** Detect `services.AddScoped(typeof(IFoo), typeof(Foo))` — the two-argument typeof overload for all three lifetimes
- **D-02:** Also detect `ServiceDescriptor.Scoped(typeof(IFoo), typeof(Foo))` static factory patterns — same semantic intent, different syntax
- **D-03:** Skip `new ServiceDescriptor(typeof(...), typeof(...), ServiceLifetime.X)` constructor form — rare in application code, can add later if requested
- **D-04:** Open generic typeof patterns (`typeof(IRepository<>)`) get IOC094 at **Info** severity — IoCTools doesn't support open generics yet, so no actionable fix path
- **D-05:** All typeof diagnostics (IOC090-094) share the existing `IoCToolsManualSeverity` MSBuild knob — same concern as IOC081-083, no separate configuration

### CLI Output Modes
- **D-06:** `--json` outputs **only** the JSON payload to stdout — no headers, no summary lines. Warnings/errors go to stderr. Must work with `| jq .`
- **D-07:** `--verbose` writes process diagnostics (MSBuild restore, generator timing, resolved file paths) to **stderr** while keeping command output on stdout. `--json --verbose` works: JSON on stdout, debug info on stderr
- **D-08:** Color auto-detects terminal capability and follows the `NO_COLOR` convention (https://no-color.org/). Color on by default in interactive terminals, off when piped or when `NO_COLOR` env var is set. No explicit `--color`/`--no-color` flags
- **D-09:** Colored elements: diagnostic severity labels (red Error, yellow Warning, cyan Info), lifetime labels (green Singleton, blue Scoped, gray Transient), command headers. Data itself stays uncolored

### Wildcard Filtering
- **D-10:** Simple wildcards only — `*` (any chars) and `?` (single char). No regex
- **D-11:** Backward compatible — bare names without wildcards keep existing exact match + suffix match behavior
- **D-12:** Unify the two divergent filter implementations (`ServiceFieldInspector.MatchesTypeName` and `RegistrationSummaryBuilder.TypeMatchesFilter`) into a single shared `TypeMatchesPattern()` method
- **D-13:** Use wildcard-to-regex conversion internally — matches the `IoCToolsIgnoredTypePatterns` glob syntax already used in the generator's `GeneratorStyleOptions`
- **D-14:** Match against fully-qualified type name without `global::` prefix

### .editorconfig Suppress Command
- **D-15:** New top-level command `ioc-tools suppress` — not a subcommand of `doctor`
- **D-16:** Default filter: `--severity warning,info` (excludes errors from suppression). Optional `--codes IOC035,IOC053` for explicit picks. Both flags can combine
- **D-17:** `--live` flag runs the generator first and suppresses only codes actually firing in the project — natural follow-on to `doctor`
- **D-18:** Stdout by default for review/piping. `--output .editorconfig` to append with conflict detection (skips already-present rules, prints summary to stderr)
- **D-19:** Per-category groupings with comments — organized by IoCTools.Lifetime, IoCTools.Registration, etc. with diagnostic title as inline comment
- **D-20:** Errors suppressed via explicit `--codes` get a louder comment: "suppressed explicitly (verify this is intentional)"

### Claude's Discretion
- Exact IOC090-094 message text and descriptions
- Internal structure of the shared `TypeMatchesPattern()` utility
- Color implementation approach (ANSI escape sequences vs Console.ForegroundColor)
- `suppress` command argument parsing details and edge case handling
- How `--verbose` timing information is formatted
- JSON schema structure for each command's `--json` output
- WhyPrinter fuzzy suggestion extraction for CLI-04

</decisions>

<specifics>
## Specific Ideas

- typeof() detection extends ManualRegistrationValidator — same syntax tree walk, adding TypeOfExpressionSyntax node handling
- `suppress --live` reuses DiagnosticRunner to get actual firing diagnostics rather than static catalog
- The `doctor` → `suppress` flow is the intended developer journey
- Wildcard implementation should mirror GeneratorStyleOptions' existing glob pattern engine for consistency

</specifics>

<canonical_refs>
## Canonical References

### typeof() diagnostics
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Validators/ManualRegistrationValidator.cs` — Existing manual registration detection; extend for typeof() patterns
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs` — IOC081-086 definitions; add IOC090-094 here
- `IoCTools.Generator/IoCTools.Generator/Diagnostics/DiagnosticsRunner.cs` — Invokes ManualRegistrationValidator; will invoke typeof() validation

### CLI architecture
- `IoCTools.Tools.Cli/Program.cs` — Command dispatch switch; add `suppress` case
- `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs` — Custom argument parsing; add ParseSuppress()
- `IoCTools.Tools.Cli/Printers/GraphPrinter.cs` — Existing JSON output pattern via JsonSerializer
- `IoCTools.Tools.Cli/Printers/WhyPrinter.cs` — Fuzzy suggestion pattern to extract for CLI-04
- `IoCTools.Tools.Cli/Printers/DoctorPrinter.cs` — Diagnostic display; model for severity-based output

### Filtering
- `IoCTools.Tools.Cli/ServiceFieldInspector.cs` — `MatchesTypeName()` — one of two filter sites to unify
- `IoCTools.Tools.Cli/RegistrationSummaryBuilder.cs` — `TypeMatchesFilter()` and `FilterByType()` — second filter site
- `IoCTools.Generator/IoCTools.Generator/Utilities/GeneratorStyleOptions.cs` — Existing wildcard-to-regex for IoCToolsIgnoredTypePatterns

### Profile command
- `IoCTools.Tools.Cli/Printers/ProfilePrinter.cs` — Add service count output (CLI-06)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ManualRegistrationValidator.ValidateAllTrees()`: Proven syntax tree walk pattern — extend with `TypeOfExpressionSyntax` node detection
- `GraphPrinter` JSON serialization: `JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })` — template for all `--json` output
- `GeneratorStyleOptions` wildcard-to-regex: Existing glob translation — reuse algorithm in CLI's `TypeMatchesPattern()`
- `WhyPrinter` fuzzy matching: `OrdinalIgnoreCase` Contains + IndexOf pattern — extract for CLI-04
- `RegistrationSummaryBuilder.TryExtractTypeFromArgument()`: Already handles `TypeOfExpressionSyntax` → type name extraction

### Established Patterns
- Command structure: parse args → create ProjectContext → execute → print → return exit code
- Printers are stateless static classes with `Console.WriteLine()`
- All diagnostic descriptors follow 8-argument constructor pattern (added in Phase 1)
- `DiagnosticConfiguration` reads `build_property.*` MSBuild keys for severity overrides

### Integration Points
- `ManualRegistrationValidator` receives `serviceLifetimes` dictionary — typeof() detection plugs into same data flow
- `CommandLineParser` custom key-value parsing — new flags (`--json`, `--verbose`, `--severity`, `--codes`, `--live`) follow existing pattern
- `ProjectContext.CreateAsync()` — `suppress --live` reuses this for compilation access
- No color infrastructure exists — must be built from scratch (ANSI escapes or Console API)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-typeof-diagnostics-and-cli*
*Context gathered: 2026-03-21*
