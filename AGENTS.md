# Repository Guidelines

## Project Structure & Modules
- `IoCTools.Abstractions/` – public attributes and enumerations consumed by generated code.
- `IoCTools.Generator/` – Roslyn source generator and analyzers; primary logic lives under `Source/` and `Diagnostics/`.
- `IoCTools.Generator.Tests/` – xUnit coverage for generator and diagnostics (golden-file style assertions).
- `IoCTools.Tools.Cli/` & `IoCTools.Tools.Cli.Tests/` – dotnet tool that inspects generator output and its tests.
- `IoCTools.Sample/` – minimal consumer showcasing attributes and generated registrations.
- `artifacts/` – local build outputs (nuget packages, CLI tool packages); safe to clean.

## Build, Test, and Development Commands
- Restore once: `dotnet restore IoCTools.sln`.
- Fast edit loop: `dotnet build IoCTools.sln -c Debug` (runs generators/analyzers).
- Full verification: `dotnet test IoCTools.sln -c Debug` (xUnit across generator + CLI).
- Release bits: `dotnet build IoCTools.sln -c Release` followed by `dotnet test ... -c Release`.
- Pack the CLI: `dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o ./artifacts`.
- Try the tool against the repo: `dotnet tool install --add-source ./artifacts IoCTools.Tools.Cli && dotnet ioc-tools services --project IoCTools.Sample/IoCTools.Sample.csproj`.

## Coding Style & Naming Conventions
- C# only: UTF-8, LF, 4-space indent, trim trailing whitespace (`.editorconfig`).
- Prefer `var` when type is obvious; file-scoped namespaces; organize usings inside namespace with System-first grouping.
- Expression-bodied members are fine on a single line; otherwise prefer standard blocks.
- Types/methods: PascalCase; private fields: `_camelCase`; interfaces keep the `I` prefix; tests mirror subject type names.

## Testing Guidelines
- Framework: xUnit with inline data/theory where possible. Place tests in the matching project under `*.Tests/` mirroring source folder names.
- Naming: `ClassName_Scenario_ExpectedBehavior`; keep helpers private/internal within test classes.
- Coverage focus: diagnostics, generated source snapshots, and CLI command outputs. Add regression tests whenever touching analyzer rules or generator output shape.
- Run `dotnet test IoCTools.sln` before pushing; prefer `-c Release` when validating package builds.

## Commit & Pull Request Guidelines
- Commit messages: short, imperative present tense (e.g., "Fix analyzer warning"); bundle related changes together.
- PRs should include: summary of behavior change, linked issue (if any), testing notes (`dotnet test` output is enough), and before/after snippets for generator/CLI changes.
- Keep diffs lean: remove dead code, avoid feature flags unless required, and update docs/examples when attributes or diagnostics change.

## Security & Configuration Tips
- No secrets in sources or test assets; local config lives in user secrets or environment variables.
- Generator and CLI create artifacts under `obj/` or `artifacts/`; avoid committing those directories.
- When sharing repros, prefer `dotnet ioc-tools compare --project <csproj> --output ./artifacts/snap` to capture generated files without editing tracked sources.
