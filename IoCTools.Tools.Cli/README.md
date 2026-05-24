# IoCTools.Tools.Cli

`IoCTools.Tools.Cli` is the developer-facing inspection tool for IoCTools. Install it as a dotnet tool and point it at a real project to inspect generated constructors, registrations, diagnostics, configuration bindings, validators, and migration posture.

## Install

```bash
dotnet tool install --global IoCTools.Tools.Cli
```

To install from a local build:

```bash
dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts
dotnet tool install --global --add-source ./artifacts IoCTools.Tools.Cli
```

## Key Commands

```bash
ioc-tools evidence --project MyProject.csproj --json
ioc-tools evidence --project tests/MyApp.Tests.csproj --test-fixtures --production-project src/MyApp.csproj --json
ioc-tools suppress --project MyProject.csproj --codes IOC035,IOC092 --json
ioc-tools validator-graph --project MyProject.csproj --why MyValidator --json
```

- `evidence` emits one correlated bundle across registrations, diagnostics, configuration, validators, migration hints, and generated artifacts.
- `evidence --test-fixtures --production-project <csproj>` scans a test project for hand-wired `Mock<T>` fields, manual `new Service(...)` helpers, and `Options.Create(...)` boilerplate that can move to `[Cover<T>]`. Each candidate is classified as `safe migration`, `partial migration`, `already covered`, `not a target`, or `unknown/manual review`. See [`docs/testing.md` → CLI Fixture Evidence](../docs/testing.md#cli-fixture-evidence) for the before/after example.
- `evidence --baseline <dir> --output <dir> --json` adds stable artifact fingerprints and structured compare deltas with `added`, `removed`, `changed`, and `unchanged` status.
- `suppress --json` emits structured suppression metadata alongside the `.editorconfig` recipe.
- `validator-graph --json` and `validator-graph --why --json` emit structured contracts for validator topology and lifetime reasoning.

### `--json` receipt envelope

Every `--json` output is wrapped in an agent-receipt envelope so downstream automations can verify the contract version and timestamp the receipt:

```json
{
  "schema_version": "1.0",
  "generated_at": "2026-05-24T12:34:56Z",
  "project": { ... },
  "services": { ... }
}
```

- `schema_version` (string) — receipt envelope contract version. Starts at `1.0` and only bumps when the envelope shape changes; payload-specific shape changes do not bump it.
- `generated_at` (string) — ISO8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`).
- Array-shaped payloads (e.g. `validator-graph --json`) are wrapped under a `data` field so the envelope remains a JSON object.

The headers are additive — existing parsers that ignore unknown top-level fields keep working without changes.

> **Do not** replace `[Cover<T>]` compile-time fixture generation with runtime
> scanning/reflection. That pathway is rejected by IoCTools doctrine — file an
> issue against IoCTools if you hit a gap in the generator instead of working
> around it.

## Authoring Guidance

- Never use `[Inject]` in new code.
- Never use `InjectConfiguration` in new code.
- Prefer `[DependsOn<T>]` for service dependencies.
- Prefer `[DependsOnConfiguration<T>]` or `[DependsOnOptions<T>]` for configuration dependencies.

See the repo docs for the full reference:

- `docs/cli-reference.md`
- `docs/diagnostics.md`
- `docs/attributes.md`
