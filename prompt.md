## Mission

Finish params-style `MemberNames` support for `[DependsOn]`, keep redundancy/overlap diagnostics intact, and ensure all tests pass.

## Current State

- Repo root: `/Users/nathan/Documents/projects/ioctools`.
- Redundancy/overlap work (IOC040/046) and collection fixes are in; tests are currently green **only because** the partially edited `DependsOn.cs` hasn’t been recompiled yet. That file is broken (duplicate `MemberNames` props, constructors not all updated) and must be fixed.
- `AttributeParser.GetDependsOnOptionsFromAttribute` now returns `(namingConvention, stripI, prefix, external, memberNames)`.
- `DependsOnFieldAnalyzer` already consumes memberNames when generating field names (explicit per index fallback to generated).
- New validators: `ConfigurationRedundancyValidator` (IOC046) for options vs field config overlap; IOC040 extended to config bindings + inheritance; unused dependency validator covers InjectConfiguration/DependsOnConfiguration.
- New tests in `IoCTools.Generator.Tests/DependencyRedundancyTests.cs`; expectations tweaked in `RedundancyDetectionTests.cs` (IOC040 ignores pure DependsOn duplication).

## Objectives

1) Implement params-style `memberNames` for **all** `DependsOnAttribute<…>` arities in `IoCTools.Abstractions/Annotations/DependsOn.cs`.
   - Keep parameterless ctor.
   - Advanced ctor signature: `(NamingConvention namingConvention = CamelCase, bool stripI = true, string prefix = "_", bool external = false, params string[] memberNames)`.
   - Set `MemberNames = memberNames ?? Array.Empty<string>();` inside.
   - Single `MemberNames` property per class (remove duplicates).
2) Maintain backward compatibility with existing uses.
3) Ensure `AttributeParser` matches ctor arg order (external before params) and memberNames parsing works.
4) Confirm `DependsOnFieldAnalyzer` logic after fixes.
5) Optional: add analyzer guidance to prefer params over manual `MemberNames = new[] { … }` arrays.
6) Update README to mention params-style MemberNames convenience.
7) Rerun tests: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj /p:UseSharedCompilation=false` then `dotnet test IoCTools.sln` (ignore NETSDK1138).

## Key Files

- `IoCTools.Abstractions/Annotations/DependsOn.cs` (many arities up to 20). Needs full ctor/property cleanup.
- `IoCTools.Generator/IoCTools.Generator/Utilities/AttributeParser.cs` — returns memberNames.
- `IoCTools.Generator/IoCTools.Generator/Analysis/DependsOnFieldAnalyzer.cs` — uses memberNames for naming.
- Validators: `ConfigurationRedundancyValidator.cs`, `DependencyUsageValidator.cs` (IOC039/040), `CollectionDependencyValidator.cs`.
- Tests: `IoCTools.Generator.Tests/DependencyRedundancyTests.cs`, `RedundancyDetectionTests.cs`, `CollectionDependencyTests.cs`.
- Docs: `README.md` (diagnostics table/highlights).

## Pitfalls / Gotchas

- `DependsOn.cs` presently has duplicated `MemberNames` from partial edits; must clean all.
- Each advanced ctor must include params at the end; preserve default args.
- AttributeParser must align with ctor positions; memberNames is the params string array.
- Pure `[DependsOn]` duplicates handled by IOC006/IOC008; IOC040 should not fire for those alone.
- Use `apply_patch` only.

## Useful Commands

- `rg -n "DependsOnAttribute<" IoCTools.Abstractions/Annotations/DependsOn.cs`
- `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj /p:UseSharedCompilation=false`
- `dotnet test IoCTools.sln`

## Key Domain Facts

- Only `IReadOnlyCollection<T>` allowed for multi-implementation deps; others warn IOC045.
- IOC046 warns when options binding overlaps per-field config in same/nested section.
- IOC039 includes InjectConfiguration/DependsOnConfiguration (generated fields too).
- NETSDK1138 expected due to sample net6.0 TFM.
