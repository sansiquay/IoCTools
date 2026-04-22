# IoCTools CLI Reference

The `ioc-tools` command-line tool interrogates your project with the real IoCTools generator, showing exactly what the build produced without spelunking through `obj/`.

For `1.5.1`, use the CLI to audit and migrate toward `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`. `[Inject]` and `InjectConfiguration` remain supported for compatibility, and `evidence` will call them out when present.

## Installation

```bash
# From the repo root
dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts

# Install globally
dotnet tool install --global --add-source ./artifacts IoCTools.Tools.Cli

# Or install locally
dotnet new tool-manifest
dotnet tool install --add-source ./artifacts IoCTools.Tools.Cli
```

## Usage

```bash
ioc-tools <command> --project <csproj> [options]
```

### Common Options

| Option | Description |
|--------|-------------|
| `--project <csproj>` | Path to project file (.csproj) |
| `--configuration <config>` | Build configuration (default: Debug) |
| `--framework <framework>` | Target framework for multi-targeting projects |
| `--output <dir>` | Output directory for generated artifacts |
| `--json` | Emit machine-readable JSON to stdout |
| `--verbose` | Show MSBuild diagnostics and generator timing |

## Commands

### `fields`

Lists IoCTools-aware services in a file, showing generated dependency fields.

```bash
ioc-tools fields --project MyProject.csproj --file Services/UserService.cs
```

**Options:**
- `--file <path>` — Service file to analyze
- `--type <typename>` — Filter to specific type
- `--source` — Output generated constructor source code

**Output:** Shows `[DependsOn]` and `[DependsOnConfiguration]` fields, inferred names, and external flags.

---

### `fields-path`

Prints the absolute path to the generated constructor `.g.cs` file.

```bash
ioc-tools fields-path --project MyProject.csproj --file Services/UserService.cs --output ./generated
```

**Options:** Same as `fields` plus `--output <dir>` for artifact directory.

---

### `services`

Summarizes generated registration extensions.

```bash
ioc-tools services --project MyProject.csproj
```

**Options:**
- `--type <typename>` — Filter to specific service registrations
- `--source` — Output raw generated source code
- `--output <dir>` — Artifact directory

**Output:** Lifetimes, interface/implementation pairings, factories, conditionals, and configuration bindings.

---

### `services-path`

Prints the path to the generated registration extension.

```bash
ioc-tools services-path --project MyProject.csproj --output ./generated
```

---

### `explain`

Explains a single service in detail.

```bash
ioc-tools explain --project MyProject.csproj --type MyNamespace.UserService
```

**Output:** Generated dependency fields, config bindings (keys/required/reload), and external flags.

---

### `graph`

Emits a lightweight dependency graph.

```bash
ioc-tools graph --project MyProject.csproj --format json --output ./graphs
```

**Options:**
- `--format <json|puml|mermaid>` — Output format (default: json)
- `--type <typename>` — Filter graph to specific service
- `--output <dir>` — Output directory
- `--hide-auto-deps` / `--only-auto-deps` — Auto-dep visibility filter (mutually exclusive; see below)

**Output:** Service → implementation edges with per-dependency
[source attribution](auto-deps.md#cli-integration) in 1.6.0+:

- `ℹ` marker — universal auto-dep (`auto-universal` or `auto-builtin:ILogger`)
- `▣ <ProfileName>` marker — profile-sourced auto-dep
- unmarked — explicit `[DependsOn<T>]`
- JSON output adds a `source` field per node: `"explicit"`, `"auto-universal"`,
  `"auto-profile:<ProfileName>"`, `"auto-transitive:<AssemblyName>"`, or
  `"auto-builtin:ILogger"`.

A legend is emitted at the bottom of non-JSON outputs.

---

### `why`

Shows which generated field/config binding matches a requested dependency.

```bash
ioc-tools why --project MyProject.csproj --type MyNamespace.UserService --dependency MyNamespace.IUserRepository
```

**Options:**
- `--hide-auto-deps` / `--only-auto-deps` — Auto-dep visibility filter for
  downstream context; the direct target is always fully attributed.

**Output (1.6.0+):** Structured source-attribution block per dep, e.g.:

```
ILogger<OrderController> on OrderController
  source: auto-builtin:ILogger (Microsoft.Extensions.Logging.ILogger<T> detected in references)
  closed to: ILogger<OrderController> (service's concrete type)
  disable detection: IoCToolsAutoDetectLogger=false
  suppress here: [NoAutoDepOpen(typeof(ILogger<>))]

IMediator on OrderController
  source: auto-profile:ControllerDefaults
  attached by: [assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
  contributes: [assembly: AutoDepIn<ControllerDefaults, IMediator>] (Program.cs:19)
  suppress here: [NoAutoDep<IMediator>] or remove from profile

IPaymentService on OrderController
  source: explicit
  declared at: OrderController.cs:14 via [DependsOn<IPaymentService>]
```

When a dep has multiple sources, all are listed with precedence order
(`explicit → auto-profile → auto-universal → auto-transitive → auto-builtin`).

---

### `explain`

Prose narrative for a service. In 1.6.0+, auto-deps appear in the narrative
with their source attribution.

**Options:**
- `--hide-auto-deps` / `--only-auto-deps` — Auto-dep visibility filter.

---

### `doctor`

Runs the generator and prints diagnostics, plus three auto-dep preflight
checks (1.6.0+):

1. Every universal auto-dep type has at least one discoverable DI
   registration — catches broken declarations before they spam IOC001
   across the assembly.
2. No `AutoDepsApply` / `AutoDepsApplyGlob` rule matches zero services
   (aggregate of IOC099 per stale rule).
3. No `IAutoDepsProfile` type is declared but unreferenced by any
   `AutoDepIn` / `AutoDepsApply` / `AutoDeps` usage (dead profile).

```bash
ioc-tools doctor --project MyProject.csproj --fixable-only
```

**Options:**
- `--fixable-only` — Filter to warnings/infos only (no errors)

**Output:** Diagnostics with file locations and severity. Exit code 1 if any Error-severity diagnostics present.

---

### `compare`

Captures generated artifacts and compares against a baseline.

```bash
ioc-tools compare --project MyProject.csproj --output ./snapshots --baseline ./baseline
```

**Options:**
- `--output <dir>` — Current snapshot directory
- `--baseline <dir>` — Previous baseline for comparison

**Output:** Lists changed `.g.cs` files relative to baseline.

---

### `profile` vs `profiles` — two different subcommands

The singular `profile` is the existing project-load-benchmarking subcommand
(documented below). The plural `profiles` is the new auto-deps profile
introspection subcommand (1.6.0+, documented in its own section further
down). The distinction is intentional; one cannot be repurposed into the
other without a breaking rename.

### `profile`

Prints generator warm/analysis timing.

```bash
ioc-tools profile --project MyProject.csproj
```

**Options:**
- `--type <typename>` — Filter to specific type (informational only)

**Output:** Timing breakdown for generator pipeline stages.

---

### `config-audit`

Lists required config bindings and reports missing keys.

```bash
ioc-tools config-audit --project MyProject.csproj --settings appsettings.json
```

**Options:**
- `--settings <path>` — Optional settings file to validate against

**Output:** Required configuration bindings and which keys are missing from settings.

---

### `evidence`

Builds one correlated review packet across registrations, diagnostics, configuration, validators, profile data, and migration hints.

```bash
ioc-tools evidence --project MyProject.csproj --type MyNamespace.UserService --settings appsettings.json --json
```

**Options:**
- `--type <typename>` — Narrow type-specific evidence sections
- `--settings <path>` — Validate configuration evidence against a settings file
- `--baseline <dir>` — Include compare output when a baseline exists
- `--output <dir>` — Use deterministic artifact directory for generated snapshots
- `--hide-auto-deps` / `--only-auto-deps` — Auto-dep visibility filter for
  per-service rows (1.6.0+).

In 1.6.0+, each service's evidence block lists its resolved auto-dep set
alongside explicit declarations, tagged by source
(`auto-builtin:ILogger`, `auto-universal`, `auto-transitive:<Assembly>`,
`auto-profile:<Name>`).

**Output:** Compact text review packet by default, or a stable JSON bundle with `project`, `services`, `typeEvidence`, `diagnostics`, `configuration`, `validators`, `artifacts`, and `migrationHints`.

**Artifact evidence:** `artifacts.generatedArtifacts[*]` includes stable `artifactId`, on-disk `path`, `fingerprint`, and `sizeBytes`. When `--baseline` is supplied, `artifacts.compare.deltas[*]` includes `status` (`added`, `removed`, `changed`, `unchanged`) plus baseline/current paths and fingerprints.

---

### `suppress`

Generates `.editorconfig` recipes for suppressing diagnostics.

```bash
ioc-tools suppress --project MyProject.csproj --codes IOC035,IOC092 --json
```

**Options:**
- `--severity <warning,info,error>` — Include rules by default severity
- `--codes <IOC035,IOC092>` — Include explicit diagnostic ids
- `--live` — Restrict output to currently firing diagnostics
- `--output <path>` — Append generated rules to an existing `.editorconfig`

**Output:** `.editorconfig` entries plus structured rule metadata in JSON mode (`selectionReason`, `isErrorByDefault`, `riskNote`, `suppressedSeverity`).

In 1.6.0+, `suppress` is aware of the new IOC095-IOC105 auto-deps diagnostics
so generated suppressions cover the expanded diagnostic surface.

### `validators`

Lists FluentValidation validator classes discovered by the IoCTools.FluentValidation generator.

```bash
ioc-tools validators --project MyProject.csproj
```

**Options:**
- `--filter <pattern>` — Filter validators by type or model name

**Output:** Lists all validators with lifetime, model type, and composition edge count.

**Example output:**
```
Validators: 3

  [Scoped] MyApp.OrderValidator -> Order (2 composition edges)
  [Scoped] MyApp.AddressValidator -> Address
  [Transient] MyApp.CustomerValidator -> Customer
```

**JSON mode:**
```bash
ioc-tools validators --project MyProject.csproj --json
```
Returns array of `{ validator, modelType, lifetime, hasComposition, compositionEdges[] }`.

---

### `validator-graph`

Emits a tree visualization of validator composition hierarchy (SetValidator/Include/SetInheritanceValidator chains).

```bash
ioc-tools validator-graph --project MyProject.csproj
```

**Options:**
- `--why <validator>` — Trace why a validator has its lifetime through composition chains

**Output:** Tree showing validator composition relationships.

**Example output:**
```
OrderValidator [Scoped] -> Order
+-- AddressValidator [Scoped] -> Address (via SetValidator (injected))
+-- CustomerValidator [Transient] -> Customer (via Include (injected))
```

**`--why` mode:**
```bash
ioc-tools validator-graph --project MyProject.csproj --why OrderValidator
```
```
MyApp.OrderValidator is Scoped because:
  - composes MyApp.AddressValidator [Scoped] via SetValidator (matching lifetime)
```

**JSON mode:** `validator-graph --json` emits structured composition trees, and `validator-graph --why --json` emits a structured explanation object with `reason` and `steps`.

---

### `profiles` (1.6.0+)

Lists auto-deps profiles (`IAutoDepsProfile`-marked classes), their
contributed deps, and — optionally — the services each profile attaches to.

```bash
ioc-tools profiles --project MyProject.csproj
ioc-tools profiles --project MyProject.csproj --matches
ioc-tools profiles --project MyProject.csproj ControllerDefaults
```

**Positional:**
- `<ProfileName>` (optional) — Drill into one profile. Accepts a simple
  name (`ControllerDefaults`) or fully-qualified
  (`MyApp.DiProfiles.ControllerDefaults`). Ambiguous simple names exit
  non-zero with a candidate list.

**Options:**
- `--matches` — Also list the services each profile attaches to.

**Output:** For each profile: deps (from `AutoDepIn` declarations),
attachment rules (`AutoDepsApply`, `AutoDepsApplyGlob`, `[AutoDeps<T>]`
usages), and — with `--matches` — the set of services the profile resolves
to.

> **Naming distinction.** Singular `profile` = project-load benchmarking
> (existing). Plural `profiles` = auto-deps profile introspection (new).

---

### `migrate-inject` (1.6.0+)

Headless bulk `[Inject]` → `[DependsOn<T>]` migration. Uses the exact same
transform (`InjectMigrationRewriter`) as the IDE code fix, so output is
identical whether run from the CLI or invoked per-field in the IDE.

```bash
ioc-tools migrate-inject --project MyProject.csproj --dry-run
ioc-tools migrate-inject --project MyProject.csproj
ioc-tools migrate-inject --solution MyApp.sln
```

**Options:**
- `--project <path>` — Single project target
- `--solution <path>` — Walk every project in a solution
- `--path <dir>` — Alternative scoping by directory
- `--dry-run` — Print would-be diffs without writing to disk

**Output:** Per-file summary — fields deleted because an auto-dep covers
them, fields converted to `[DependsOn<T>]`, fields converted with
`memberName1..N` preservation, fields split into separate `DependsOn`
attributes because `[ExternalService]` flags diverged.

**Cross-version tolerance.** When the target project still references
`IoCTools.Abstractions < 1.6.0`, the tool prints a one-line notice and
disables the "delete entirely" branch (no auto-deps available to cover the
field); conversions to `[DependsOn<T>]` still run.

**Kill-switch interaction.** When `<IoCToolsAutoDepsDisable>true</IoCToolsAutoDepsDisable>`
or the project matches `<IoCToolsAutoDepsExcludeGlob>`, the resolver
returns an empty auto-dep set, so every `[Inject]` converts to an explicit
`[DependsOn<T>]` — consistent with the kill-switch's meaning.

**Concurrency.** `migrate-inject` processes documents sequentially in 1.6
for deterministic CI diffs. Parallelization is deferred.

---

### Cross-command auto-dep flags

Available on `graph`, `why`, `explain`, and `evidence`:

- `--hide-auto-deps` — Collapse implicit auto-dep entries from the output.
  Default is show-everything; hiding is an explicit opt-in.
- `--only-auto-deps` — Inverse view, for auditing.

The two flags are semantically contradictory and are mutually exclusive —
passing both exits non-zero with a clear message.

Note that `--hide-auto-deps` is an output filter; `why <type> <dep>` always
produces a full attribution block for the explicitly-asked dep regardless
of the flag (the flag only affects downstream context in the extended
view).

---

## JSON Output Mode

All commands support `--json` for machine-readable output:

```bash
ioc-tools services --project MyProject.csproj --json | jq '.services | length'
```

JSON is emitted to stdout; verbose output goes to stderr, enabling:
```bash
ioc-tools doctor --project MyProject.csproj --json --verbose 2>build.log | jq '.diagnostics[] | select(.severity == "Error")'
```

---

## Verbose Mode

`--verbose` adds MSBuild diagnostics, generator timing, and resolved file paths:

```bash
ioc-tools explain --project MyProject.csproj --type UserService --verbose
```

Output format: `[verbose] <elapsed>ms: <message>`

---

## Color Output

Diagnostics are color-coded by severity in terminal output:

| Severity | Color |
|----------|-------|
| Error | Red |
| Warning | Yellow |
| Info | Cyan |

Disabled when output is piped or `NO_COLOR` environment variable is set.

---

## Artifact Locations

By default, the CLI copies generator artifacts to:

```
%TEMP%/IoCTools.Tools.Cli/<project>/<timestamp>/
```

Use `--output` for deterministic locations:
```bash
ioc-tools services --project MyProject.csproj --output ./generated
```

---

## Related

- [Getting Started](getting-started.md) — IoCTools introduction
- [Configuration](configuration.md) — MSBuild properties and diagnostic severity
- [Diagnostics Reference](diagnostics.md) — All diagnostic codes
- [FluentValidation Diagnostics](diagnostics.md#fluentvalidation-diagnostics) — IOC100-IOC102

---

**Back to [main README](../README.md)**
