# IoCTools Evidence Command And Inject Guidance Design

Date: 2026-04-11
Status: Approved design checkpoint
Scope: Ship a coherent `1.5.0` release pass that adds a correlated CLI evidence bundle, strengthens machine-readable validator and suppression output, and makes `[DependsOn]` / `[DependsOnConfiguration]` the only recommended authoring path without breaking existing `[Inject]`-based code.

## Purpose

IoCTools already has the underlying facts needed to explain most DI and configuration behavior:

- generated registrations
- generated constructors and field names
- dependency graphs
- live diagnostics
- configuration binding discovery
- FluentValidation composition
- test-fixture generation

Those facts are currently spread across multiple commands and docs, and the authoring guidance still tolerates `[Inject]` and `InjectConfiguration` too casually for the product posture we actually want.

This design makes three things explicit for `1.5.0`:

1. IoCTools should provide one correlated evidence bundle for a project or type.
2. Machine-readable CLI surfaces should be strong enough for higher-level tools to consume directly.
3. `[Inject]` and `InjectConfiguration` remain supported for compatibility in `1.5.0`, but new code guidance becomes unambiguous: never use them; use `[DependsOn]` and `[DependsOnConfiguration]` instead.

## Design Goals

- ship on the existing `1.5.0` release line rather than inventing a new release shape
- add one top-level `ioc-tools evidence` command that composes existing truths instead of re-implementing analysis
- make `validator-graph` and `suppress` fully usable from machine consumers
- make docs and diagnostics consistently steer users away from `[Inject]` and `InjectConfiguration`
- preserve backwards compatibility for existing users during `1.5.0`
- test the CLI contracts, migration posture, and release documentation aggressively

## Non-Goals

- remove `[Inject]` or `InjectConfiguration` in `1.5.0`
- build a new general CLI framework or plugin system
- replace existing commands such as `explain`, `doctor`, `config-audit`, or `validators`
- add new runtime DI behavior unrelated to evidence, guidance, or diagnostics
- force migration before `2.0`

## Release Posture

This work ships as part of the existing `1.5.0` release line already present in the repo.

The package/version rule is:

- `IoCTools.Abstractions`, `IoCTools.Generator`, and `IoCTools.Tools.Cli` move onto the same `1.5.0` line already used by the testing packages
- changelog, README, and release notes describe the new CLI and guidance surfaces as `1.5.0` additions
- nothing in this scope requires a `2.0` breaking-change posture

`2.0` remains the earliest release where true removal of `[Inject]` and `InjectConfiguration` may happen.

## Core Decisions

### 1. Add A New Top-Level `evidence` Command

IoCTools should add a dedicated `ioc-tools evidence` command rather than overloading `explain`.

Reason:

- `explain` is type-focused and narrow
- the new feature is explicitly a correlated bundle over several existing inspection surfaces
- a new command gives the JSON contract and text rendering a clear identity

The evidence command is compositional, not a new analyzer pipeline.
It should reuse the same underlying runners and project context used by:

- `services`
- `explain`
- `why`
- `graph`
- `doctor`
- `config-audit`
- `validators`
- `validator-graph`
- `compare`
- `profile`

### 2. `[Inject]` And `InjectConfiguration` Become Compatibility-Only

For `1.5.0`:

- existing code using `[Inject]` or `InjectConfiguration` must keep working
- docs must stop recommending either pattern
- new examples should always show `[DependsOn]`, `IDependencySet`, `[DependsOnConfiguration]`, and `[DependsOnOptions]`
- diagnostics and CLI output should make the preferred replacement obvious whenever a compatible migration exists

The product message should be explicit:

- never use `[Inject]` in new code
- never use `InjectConfiguration` in new code
- use `[DependsOn]` instead of `[Inject]`
- use `[DependsOnConfiguration]` or `[DependsOnOptions]` instead of `InjectConfiguration`

### 3. Machine Output Is A First-Class Product Surface

The new and upgraded CLI surfaces must be designed for both humans and tooling.

That means:

- `evidence --json` is a stable structured contract
- `validator-graph --json` emits a structured graph and structured `--why` explanation, not only text
- `suppress --json` emits structured suppression entries with metadata, not only `.editorconfig` text
- text mode remains compact and readable

## Evidence Command Design

### Command Shape

The new command should be:

```bash
ioc-tools evidence --project <csproj> [--type Namespace.Service] [--settings appsettings.json] [--baseline <dir>] [--output <dir>] [--json] [--verbose]
```

Required input:

- `--project`

Optional narrowing and correlation inputs:

- `--type`
  - narrows type-specific sections such as explain/why/graph
- `--settings`
  - passes through to config audit
- `--baseline`
  - enables compare output when present
- `--output`
  - deterministic artifact/output directory when compare or graph snapshots are materialized

### Evidence Sections

The evidence bundle should include these sections:

- `project`
  - project path
  - configuration
  - target framework when resolved
- `services`
  - registration summary
- `typeEvidence`
  - only when `--type` is supplied
  - explanation payload
  - dependency reasoning payload
  - narrowed graph payload
- `diagnostics`
  - doctor results
- `configuration`
  - config-audit results
- `validators`
  - discovered validators
  - validator composition graph
  - structured lifetime explanation when `--type` names a validator
- `artifacts`
  - compare output when `--baseline` is supplied
  - profile output from the same project run
- `migrationHints`
  - guidance items such as “this service uses `[Inject]`; prefer `[DependsOn]`”

Not every section is mandatory in every run.
The rule is omission, not fake empty truth:

- omit compare data when no baseline is provided
- omit type-focused sections when `--type` is absent
- omit validator-specific sections when FluentValidation is not referenced or no validators exist

### JSON Contract

The JSON contract should be a single object with stable top-level property names:

```json
{
  "project": {},
  "services": {},
  "typeEvidence": null,
  "diagnostics": {},
  "configuration": {},
  "validators": {},
  "artifacts": {},
  "migrationHints": []
}
```

The exact nested shapes should be derived from existing command payloads where possible so the evidence command stays compositional.

### Text Rendering

Text mode should render a compact review packet:

- project summary first
- errors and warnings next when present
- services and type evidence next
- config findings next
- validator findings next
- migration hints last unless they are the main risk

The text surface is a synthesis layer, not a replacement for the existing focused commands.
It should always be possible to say which sub-command a section came from.

## Validator Graph JSON Design

`validator-graph --json` already has partial JSON behavior.
`1.5.0` should tighten it into a deliberate contract.

Required outcomes:

- graph mode returns a stable tree structure with:
  - validator
  - model type
  - lifetime
  - child edges
  - composition method
  - direct-instantiation flag
- `--why` mode returns a structured explanation object, not only a text blob

Conceptually:

```json
{
  "validator": "MyApp.OrderValidator",
  "lifetime": "Scoped",
  "reason": "composition",
  "steps": [
    {
      "kind": "composes",
      "target": "MyApp.AddressValidator",
      "method": "SetValidator",
      "isDirect": false,
      "lifetime": "Scoped"
    }
  ]
}
```

The plain-text rendering may stay human-readable and tree-shaped.

## Suppress JSON Design

`suppress --json` should stop at a structured suppression contract plus the generated `.editorconfig` content.

Each suppression entry should include:

- diagnostic id
- title
- category
- default severity
- suppressed severity
- whether the rule was included because of explicit code selection, severity selection, or live-diagnostic detection
- whether the rule is error-severity by default
- a note when suppressing an error-level rule should be treated as extra risky

Conceptually:

```json
{
  "rules": [
    {
      "id": "IOC035",
      "title": "Inject field could use DependsOn",
      "category": "IoCTools.Dependency",
      "defaultSeverity": "Warning",
      "suppressedSeverity": "none",
      "selectionReason": "severity",
      "isErrorByDefault": false,
      "riskNote": null
    }
  ],
  "editorconfig": "..."
}
```

This is the machine-readable contract higher-level tooling should consume.

## Inject Guidance And Diagnostics

### Documentation Rule

The following docs should be updated so the recommendation is explicit and consistent:

- `README.md`
- `docs/getting-started.md`
- `docs/attributes.md`
- `docs/testing.md`
- `docs/diagnostics.md`
- `docs/migration.md`
- `docs/cli-reference.md`

Required message:

- `[DependsOn]` is the normal path for service dependencies
- `[DependsOnConfiguration]` is the normal path for configuration dependencies
- `[Inject]` and `InjectConfiguration` are compatibility-only escape hatches
- never use `[Inject]` or `InjectConfiguration` in new code

### Diagnostic Rule

`1.5.0` should strengthen, not break.

That means:

- keep existing `[Inject]` support
- reuse or sharpen existing diagnostics where possible
- add new non-breaking warning/info diagnostics only when the current diagnostic set cannot express the preferred migration clearly

The intended diagnostic posture is:

- `[Inject]` that matches the default generated-field pattern should produce a clear “use `[DependsOn]` instead” message
- `InjectConfiguration` should produce a clear “use `[DependsOnConfiguration]` or `[DependsOnOptions]` instead” message when a direct migration is available
- raw `IOptions<T>` and `IConfiguration` usage should continue pointing toward `[DependsOnConfiguration]`

The product wording should stop treating `[Inject]` as merely an equal “last-resort marker” and instead treat it as a compatibility-only fallback.

### CLI Migration Hints

The CLI should surface migration hints where it can do so truthfully.

At minimum:

- `evidence` may report compatibility-only declarations found in the project
- hints should name the preferred replacement
- hints should never invent an automatic rewrite the CLI cannot actually justify

Example:

- “`BillingService` uses `[Inject]` field `_logger`; prefer `[DependsOn<ILogger<BillingService>>]`.”
- “`DatabaseService` uses `InjectConfiguration`; prefer `[DependsOnConfiguration<string>(\"Database:ConnectionString\")]`.”

## File And Responsibility Map

The primary implementation areas are:

- CLI command routing and parsing:
  - `IoCTools.Tools.Cli/Program.cs`
  - `IoCTools.Tools.Cli/CommandLine/CommandLineParser.cs`
- new CLI composition helpers and printers:
  - new evidence-specific utility files in `IoCTools.Tools.Cli/Utilities/`
  - `ValidatorPrinter`
  - `SuppressPrinter`
  - `UsagePrinter`
- tests:
  - new CLI evidence tests
  - validator command tests
  - suppress/extended CLI tests
  - generator diagnostic tests for guidance posture
- docs and release metadata:
  - README, CLI docs, attribute docs, testing docs, migration docs, changelog
- package/release metadata:
  - the project files that still need to move onto `1.5.0`

## Testing Strategy

Testing should be intentionally heavy.

### CLI Tests

- parser tests for `evidence`
- text-mode tests for `evidence`
- JSON-mode tests for `evidence`
- validator graph JSON contract tests
- validator `--why` JSON contract tests
- suppress JSON contract tests
- usage/help text tests updated for the new command

### Generator / Diagnostic Tests

- tests proving `[Inject]` guidance diagnostics remain non-breaking
- tests proving `InjectConfiguration` guidance points to `[DependsOnConfiguration]`
- tests proving existing supported behavior still works

### Documentation Verification

- CLI reference reflects actual command/options
- README command table includes `evidence`
- getting-started and attribute docs no longer imply `[Inject]` is acceptable for normal authoring
- migration docs explain the `1.5.0` posture and reserve hard removal for `2.0`

### Release Verification

- build succeeds
- CLI test project passes
- generator test project passes
- testing packages still build
- package metadata and changelog read like one coherent `1.5.0` release

## Success Criteria

This design is satisfied when all of the following are true:

- `ioc-tools evidence` ships with readable text mode and stable JSON mode
- `validator-graph --json` and `validator-graph --why --json` emit structured contracts
- `suppress --json` emits structured suppression metadata plus `.editorconfig` text
- docs explicitly say never use `[Inject]` or `InjectConfiguration` in new code
- diagnostics and/or evidence output steer users toward `[DependsOn]` and `[DependsOnConfiguration]` without breaking compatibility
- `IoCTools.Abstractions`, `IoCTools.Generator`, and `IoCTools.Tools.Cli` align to the `1.5.0` release line already present in the repo
- changelog and release docs describe the new surfaces accurately
- automated tests strongly cover CLI contracts, diagnostics, and release documentation changes

## Deferred To `2.0`

These are intentionally not part of this design:

- removing `[Inject]`
- removing `InjectConfiguration`
- auto-rewriting source code during migration
- making compatibility-only declarations fail the build

## Recommendation

Implement this as one coherent `1.5.0` release pass:

- new `evidence` command
- stronger machine-readable validator and suppression output
- explicit anti-`[Inject]` / anti-`InjectConfiguration` guidance
- non-breaking diagnostic pressure toward `[DependsOn]` / `[DependsOnConfiguration]`
- release/documentation/version alignment
- heavy automated coverage

That gives IoCTools a clearer product center:

- declarative service intent on the class
- machine-readable evidence over generated DI truth
- compatibility support for legacy escape hatches
- a clean breaking-change story later in `2.0`
