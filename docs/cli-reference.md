# IoCTools CLI Reference

The `ioc-tools` command-line tool interrogates your project with the real IoCTools generator, showing exactly what the build produced without spelunking through `obj/`.

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

**Output:** Service → implementation edges in the specified format.

---

### `why`

Shows which generated field/config binding matches a requested dependency.

```bash
ioc-tools why --project MyProject.csproj --type MyNamespace.UserService --dependency MyNamespace.IUserRepository
```

**Use case:** Debugging why a specific dependency is wired the way it is.

---

### `doctor`

Runs the generator and prints diagnostics.

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

### `suppress`

Generates `.editorconfig` recipes for suppressing diagnostics.

```bash
ioc-tools suppress --project MyProject.csproj --diagnostic IOC035
```

**Output:** `.editorconfig` entries for the specified diagnostics.

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
