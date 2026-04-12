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
ioc-tools suppress --project MyProject.csproj --codes IOC035,IOC092 --json
ioc-tools validator-graph --project MyProject.csproj --why MyValidator --json
```

- `evidence` emits one correlated bundle across registrations, diagnostics, configuration, validators, migration hints, and generated artifacts.
- `evidence --baseline <dir> --output <dir> --json` adds stable artifact fingerprints and structured compare deltas with `added`, `removed`, `changed`, and `unchanged` status.
- `suppress --json` emits structured suppression metadata alongside the `.editorconfig` recipe.
- `validator-graph --json` and `validator-graph --why --json` emit structured contracts for validator topology and lifetime reasoning.

## Authoring Guidance

- Never use `[Inject]` in new code.
- Never use `InjectConfiguration` in new code.
- Prefer `[DependsOn<T>]` for service dependencies.
- Prefer `[DependsOnConfiguration<T>]` or `[DependsOnOptions<T>]` for configuration dependencies.

See the repo docs for the full reference:

- `docs/cli-reference.md`
- `docs/diagnostics.md`
- `docs/attributes.md`
