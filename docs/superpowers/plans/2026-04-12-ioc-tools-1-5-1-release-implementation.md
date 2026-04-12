# IoCTools 1.5.1 Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the first real public `1.5.x` release as `1.5.1` by repairing release automation, aligning package metadata/docs, hardening generator failure visibility, and officially supporting the common open-generic registration path.

**Architecture:** Keep `1.5.1` narrow and coherent. Separate CI from publishing, align every packable project onto the same `1.5.1` line, remove the remaining silent generator degradation paths, and upgrade the existing partial open-generic story into a supported end-to-end path across diagnostics, sample usage, CLI evidence, and docs. Avoid broad refactors or new product areas that are not required to ship a stable public `1.5.1`.

**Tech Stack:** GitHub Actions, .NET 9 SDK, Roslyn source generator internals, xUnit, FluentAssertions, SDK-style NuGet packaging, markdown docs, Git tags and GitHub CLI.

---

### Task 1: Establish the real `1.5.1` release surface and split CI from publish

**Files:**
- Create: `scripts/verify-release-surface.sh`
- Modify: `.github/workflows/ci-main.yml`
- Create: `.github/workflows/release.yml`
- Modify: `IoCTools.Abstractions/IoCTools.Abstractions.csproj`
- Modify: `IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj`
- Modify: `IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj`
- Modify: `IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj`
- Modify: `IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj`

- [ ] **Step 1: Write a failing release-surface verifier**

```bash
#!/usr/bin/env bash
set -euo pipefail

expected_version="${1:-1.5.1}"
expected_repo="https://github.com/nathan-p-lane/IoCTools"

projects=(
  "IoCTools.Abstractions/IoCTools.Abstractions.csproj"
  "IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj"
  "IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj"
  "IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj"
  "IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj"
)

for project in "${projects[@]}"; do
  rg -q "<Version>${expected_version}</Version>" "$project"
done

for project in "${projects[@]}"; do
  if rg -q "<PackageProjectUrl>" "$project"; then
    rg -q "<PackageProjectUrl>${expected_repo}/?</PackageProjectUrl>" "$project"
    rg -q "<RepositoryUrl>${expected_repo}/?</RepositoryUrl>" "$project"
  fi
done

rg -q "IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj" .github/workflows/release.yml
rg -q "IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj" .github/workflows/release.yml
rg -q "IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj" .github/workflows/release.yml
```

- [ ] **Step 2: Run the verifier and confirm current state fails**

Run:

```bash
chmod +x scripts/verify-release-surface.sh
./scripts/verify-release-surface.sh 1.5.1
```

Expected: FAIL because projects are still on `1.5.0` / `1.0.0`, old `nate123456` URLs remain, and `.github/workflows/release.yml` does not exist.

- [ ] **Step 3: Bump every packable project to `1.5.1` and fix package metadata**

```xml
<Version>1.5.1</Version>
<PackageVersion>1.5.1</PackageVersion>
<AssemblyVersion>1.5.1.0</AssemblyVersion>
<FileVersion>1.5.1.0</FileVersion>
<PackageProjectUrl>https://github.com/nathan-p-lane/IoCTools</PackageProjectUrl>
<RepositoryUrl>https://github.com/nathan-p-lane/IoCTools</RepositoryUrl>
```

Apply this across the packable projects, including moving `IoCTools.FluentValidation` from `1.0.0` to `1.5.1` so the public release line is coherent.

- [ ] **Step 4: Split CI from release publishing**

`ci-main.yml` should become build/test only:

```yaml
name: CI Main Branch

on:
  push:
    branches:
      - main

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore IoCTools.sln
      - run: |
          for proj in IoCTools.Tools.Cli.Tests/TestProjects/*/; do
            dotnet restore "$proj"
          done
      - run: dotnet build IoCTools.sln --configuration Release --no-restore
      - run: |
          for proj in IoCTools.Tools.Cli.Tests/TestProjects/*/; do
            dotnet build "$proj" --configuration Release --no-restore
          done
      - run: dotnet test IoCTools.sln --configuration Release --no-build --no-restore --verbosity normal
```

Create `release.yml` for tag-driven publication:

```yaml
name: Release Packages

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:

jobs:
  build-test-pack:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - run: dotnet restore IoCTools.sln
      - run: |
          for proj in IoCTools.Tools.Cli.Tests/TestProjects/*/; do
            dotnet restore "$proj"
          done
      - run: dotnet build IoCTools.sln --configuration Release --no-restore
      - run: |
          for proj in \
            IoCTools.Abstractions/IoCTools.Abstractions.csproj \
            IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj \
            IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj \
            IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj \
            IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj; do
            dotnet pack "$proj" --configuration Release --no-build --output artifacts
          done
      - uses: actions/upload-artifact@v4
        with:
          name: release-packages
          path: artifacts/*.nupkg

  publish:
    runs-on: ubuntu-latest
    needs: build-test-pack
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: release-packages
          path: artifacts
      - run: |
          find artifacts -name '*.nupkg' ! -name '*.symbols.nupkg' -print0 | \
          xargs -0 -n1 dotnet nuget push \
            --api-key "${{ secrets.NUGET_API_KEY }}" \
            --source https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

- [ ] **Step 5: Re-run the release-surface verifier and dry-run local packs**

Run:

```bash
./scripts/verify-release-surface.sh 1.5.1
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Abstractions/IoCTools.Abstractions.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj -c Release -o artifacts
```

Expected: PASS, with all `1.5.1` artifacts produced locally and no missing package entries in `release.yml`.

- [ ] **Step 6: Commit**

```bash
git add scripts/verify-release-surface.sh .github/workflows/ci-main.yml .github/workflows/release.yml \
  IoCTools.Abstractions/IoCTools.Abstractions.csproj \
  IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj \
  IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj \
  IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj \
  IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj
git commit -m "build: prepare coherent 1.5.1 release surface"
```

### Task 2: Make generator analysis failures visible instead of silently degrading output

**Files:**
- Create: `IoCTools.Generator.Tests/GeneratorResilienceTests.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs`

- [ ] **Step 1: Write failing resilience tests around observable failure behavior**

```csharp
[Fact]
public void GetInterfacesForRegistration_WhenInterfaceTraversalThrows_ReportsIOC093AndReturnsEmpty()
{
    var source = """
                 using IoCTools.Abstractions.Annotations;
                 namespace Test;
                 public interface IService {}
                 [Scoped]
                 public partial class MyService : IService {}
                 """;

    var compilation = SourceGeneratorTestHelper.CreateCompilation(source);
    var classSymbol = compilation.GetTypeByMetadataName("Test.MyService")!;
    var diagnostics = new List<Diagnostic>();

    var interfaces = RegistrationSelector.GetInterfacesForRegistration(
        classSymbol,
        diagnostics.Add,
        _ => throw new InvalidOperationException("boom"));

    interfaces.Should().BeEmpty();
    diagnostics.Should().ContainSingle(d => d.Id == "IOC093");
}

[Fact]
public void ResolveDeclaredClassSymbol_WithMismatchedSemanticModel_ReportsIOC093()
{
    var firstTree = CSharpSyntaxTree.ParseText("namespace Test; public partial class MyService {}");
    var secondTree = CSharpSyntaxTree.ParseText("namespace Other; public class Placeholder {}");
    var compilation = SourceGeneratorTestHelper.CreateCompilation(firstTree, secondTree);
    var mismatchedModel = compilation.GetSemanticModel(secondTree);
    var classDeclaration = firstTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Single();
    var diagnostics = new List<Diagnostic>();

    var symbol = ConstructorGenerator.ResolveDeclaredClassSymbol(classDeclaration, mismatchedModel, diagnostics.Add);

    symbol.Should().BeNull();
    diagnostics.Should().ContainSingle(d => d.Id == "IOC093");
}
```

- [ ] **Step 2: Run the targeted resilience tests and confirm they fail**

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~GeneratorResilienceTests --logger "console;verbosity=minimal"
```

Expected: FAIL because `RegistrationSelector.GetInterfacesForRegistration` and `ConstructorGenerator.ResolveDeclaredClassSymbol` do not exist yet, and the current implementation still swallows failures silently.

- [ ] **Step 3: Add a first-class observable analysis-failure diagnostic**

```csharp
public static readonly DiagnosticDescriptor ServiceAnalysisFailure = new(
    "IOC093",
    "IoCTools could not analyze service shape",
    "IoCTools could not fully analyze '{0}' because '{1}' failed. Generation was skipped for the affected output to avoid incomplete registrations or constructors.",
    "IoCTools.Structural",
    DiagnosticSeverity.Error,
    true,
    "This indicates generator analysis failed for a specific type. Fix the underlying syntax/model issue or report a bug if the code is valid.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc093");
```

- [ ] **Step 4: Refactor interface discovery to be pure, testable, and non-silent**

```csharp
internal static List<INamedTypeSymbol> GetAllInterfacesForService(
    INamedTypeSymbol classSymbol,
    Func<INamedTypeSymbol, IEnumerable<INamedTypeSymbol>>? interfaceProvider = null)
{
    interfaceProvider ??= static symbol => symbol.AllInterfaces;

    return interfaceProvider(classSymbol)
        .Where(i => i.ContainingNamespace?.ToDisplayString()?.StartsWith("System", StringComparison.Ordinal) != true)
        .Distinct(SymbolEqualityComparer.Default)
        .ToList();
}
```

Add a wrapper in `RegistrationSelector` that owns reporting:

```csharp
internal static IReadOnlyList<INamedTypeSymbol> GetInterfacesForRegistration(
    INamedTypeSymbol classSymbol,
    ReportDiagnosticDelegate reportDiagnostic,
    Func<INamedTypeSymbol, IEnumerable<INamedTypeSymbol>>? interfaceProvider = null)
{
    try
    {
        return InterfaceDiscovery.GetAllInterfacesForService(classSymbol, interfaceProvider);
    }
    catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or ArgumentException)
    {
        reportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ServiceAnalysisFailure,
            classSymbol.Locations.FirstOrDefault(),
            classSymbol.Name,
            nameof(InterfaceDiscovery)));
        return Array.Empty<INamedTypeSymbol>();
    }
}
```

- [ ] **Step 5: Stop constructor generation from continuing with a mismatched semantic model**

```csharp
internal static INamedTypeSymbol? ResolveDeclaredClassSymbol(
    TypeDeclarationSyntax classDeclaration,
    SemanticModel semanticModel,
    ReportDiagnosticDelegate reportDiagnostic)
{
    try
    {
        return semanticModel.GetDeclaredSymbol(classDeclaration);
    }
    catch (ArgumentException)
    {
        reportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ServiceAnalysisFailure,
            classDeclaration.Identifier.GetLocation(),
            classDeclaration.Identifier.Text,
            nameof(ConstructorGenerator)));
        return null;
    }
}
```

Update the main constructor generation path to call `ResolveDeclaredClassSymbol(...)` and return early when it fails instead of generating degraded configuration state.

- [ ] **Step 6: Re-run targeted resilience tests, then the full generator suite**

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~GeneratorResilienceTests --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --logger "console;verbosity=minimal"
```

Expected: PASS, with `IOC093` surfacing the previously silent failure paths and no regressions across the rest of the generator suite.

- [ ] **Step 7: Commit**

```bash
git add IoCTools.Generator.Tests/GeneratorResilienceTests.cs \
  IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/StructuralDiagnostics.cs \
  IoCTools.Generator/IoCTools.Generator/Utilities/InterfaceDiscovery.cs \
  IoCTools.Generator/IoCTools.Generator/Generator/RegistrationSelector.cs \
  IoCTools.Generator/IoCTools.Generator/CodeGeneration/ConstructorGenerator.cs
git commit -m "fix: surface generator analysis failures in 1.5.1"
```

### Task 3: Turn the common open-generic path into a supported `1.5.1` story

**Files:**
- Modify: `IoCTools.Generator.Tests/TypeOfRegistrationTests.cs`
- Modify: `IoCTools.Generator.Tests/AdvancedGenericEdgeCaseTests.cs`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/OpenGenericProject/OpenGenericProject.csproj`
- Create: `IoCTools.Tools.Cli.Tests/TestProjects/OpenGenericProject/OpenGenericServices.cs`
- Modify: `IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs`
- Modify: `IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs`
- Modify: `IoCTools.Sample/Program.cs`
- Modify: `IoCTools.Sample/Services/DiagnosticExamples.cs`
- Modify: `IoCTools.Sample/Services/MultiInterfaceExamples.cs`

- [ ] **Step 1: Write failing tests for the supported open-generic behavior**

```csharp
[Fact]
public void AddScoped_TypeOf_OpenGenericAttributedService_EmitsIOC091_NotIOC094()
{
    var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class {}

[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    }
}";

    var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

    result.GetDiagnosticsByCode("IOC091").Should().ContainSingle();
    result.GetDiagnosticsByCode("IOC094").Should().BeEmpty();
}

[Fact]
public void AddScoped_TypeOf_OpenGenericWithoutIoCToolsIntent_EmitsIOC094Suggestion()
{
    var source = @"
using Microsoft.Extensions.DependencyInjection;

namespace Test;

public interface IRepository<T> where T : class {}
public class Repository<T> : IRepository<T> where T : class {}

public static class Program
{
    public static void Main()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    }
}";

    var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

    result.GetDiagnosticsByCode("IOC094").Should().ContainSingle();
}
```

Add a CLI evidence regression using a dedicated test project:

```csharp
[Fact]
public async Task Evidence_JsonMode_ForOpenGenericProject_IncludesOpenGenericRegistration()
{
    var result = await CliTestHost.RunAsync(
        "evidence",
        "--project", OpenGenericProjectPath,
        "--json");

    result.ExitCode.Should().Be(0);
    result.Stdout.Should().Contain("IRepository<>");
    result.Stdout.Should().Contain("Repository<>");
}
```

- [ ] **Step 2: Run the targeted tests and confirm current behavior fails**

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~TypeOfRegistrationTests|FullyQualifiedName~AdvancedGenericEdgeCaseTests" --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --logger "console;verbosity=minimal"
```

Expected: FAIL because the manual-registration validator still short-circuits open generics as unsupported, and the CLI test project/path does not exist yet.

- [ ] **Step 3: Remove the “unsupported open generic” short-circuit and route supported cases through shared logic**

Replace the early `continue` path in `ManualRegistrationValidator` with shared `typeof()` extraction:

```csharp
var svcType = ExtractTypeFromTypeOf(args[0], semanticModel);
var implType = args.Count >= 2
    ? ExtractTypeFromTypeOf(args[1], semanticModel)
    : svcType;

if (svcType == null || implType == null)
    continue;

svcNamed = svcType;
implNamed = implType;
isTypeOfRegistration = true;

var isOpenGenericRegistration = svcNamed.IsUnboundGenericType || implNamed.IsUnboundGenericType;
```

After that:

- use IOC091/IOC092 when IoCTools already covers the same open-generic mapping
- use IOC094 only when the manual open-generic registration is valid but not currently expressed through IoCTools intent on the implementation

- [ ] **Step 4: Revise IOC094 so it no longer says open generics are unsupported**

```csharp
public static readonly DiagnosticDescriptor OpenGenericTypeOfCouldUseAttributes = new(
    "IOC094",
    "Open generic typeof() registration could use IoCTools attributes",
    "'{0}' is registered as an open generic via typeof(). Prefer expressing the same registration through IoCTools attributes on the generic implementation when possible.",
    "IoCTools.Registration",
    DiagnosticSeverity.Info,
    true,
    "The common open-generic registration path is supported in IoCTools 1.5.1. Prefer IoCTools attributes over manual typeof() registrations so diagnostics and generated registrations stay aligned.",
    "https://github.com/nathan-p-lane/IoCTools/blob/main/docs/diagnostics.md#ioc094");
```

- [ ] **Step 5: Re-enable the sample’s multi-interface open-generic repository and add a CLI test project**

Update the sample repository shape:

```csharp
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class Repository<T> : IMultiRepository<T>, IMultiQueryable<T>, IDisposable where T : class
{
    [Inject] private readonly ILogger<Repository<T>> _logger;
}
```

Re-enable the sample usage in `Program.cs`:

```csharp
var userRepo = services.GetService<IMultiRepository<User>>();
var userQueryable = services.GetService<IMultiQueryable<User>>();

Console.WriteLine("Generic Repository<User>:");
Console.WriteLine($"  IMultiRepository<User>: {userRepo != null}");
Console.WriteLine($"  IMultiQueryable<User>: {userQueryable != null}");
```

Create the CLI regression project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\IoCTools.Generator\IoCTools.Generator\IoCTools.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="..\..\..\IoCTools.Abstractions\Annotations\*.cs" Link="Annotations\%(FileName)%(Extension)" />
    <Compile Include="..\..\..\IoCTools.Abstractions\Enumerations\*.cs" Link="Enumerations\%(FileName)%(Extension)" />
  </ItemGroup>
</Project>
```

```csharp
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace OpenGenericProject.Services;

public interface IRepository<T> where T : class {}

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class Repository<T> : IRepository<T> where T : class {}
```

- [ ] **Step 6: Re-run targeted open-generic tests**

Run:

```bash
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter "FullyQualifiedName~TypeOfRegistrationTests|FullyQualifiedName~AdvancedGenericEdgeCaseTests" --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --filter FullyQualifiedName~CliEvidenceCommandTests --logger "console;verbosity=minimal"
```

Expected: PASS, with IOC094 now meaning “prefer IoCTools expression” instead of “not supported,” and the sample/CLI test project both reflecting the supported common path.

- [ ] **Step 7: Commit**

```bash
git add IoCTools.Generator.Tests/TypeOfRegistrationTests.cs \
  IoCTools.Generator.Tests/AdvancedGenericEdgeCaseTests.cs \
  IoCTools.Tools.Cli.Tests/TestProjects/OpenGenericProject/OpenGenericProject.csproj \
  IoCTools.Tools.Cli.Tests/TestProjects/OpenGenericProject/OpenGenericServices.cs \
  IoCTools.Tools.Cli.Tests/CliEvidenceCommandTests.cs \
  IoCTools.Generator/IoCTools.Generator/Generator/Diagnostics/Validators/ManualRegistrationValidator.cs \
  IoCTools.Generator/IoCTools.Generator/Diagnostics/Descriptors/RegistrationDiagnostics.cs \
  IoCTools.Sample/Program.cs \
  IoCTools.Sample/Services/DiagnosticExamples.cs \
  IoCTools.Sample/Services/MultiInterfaceExamples.cs
git commit -m "feat: support the common open-generic registration path"
```

### Task 4: Refresh user-facing docs and changelog for the real `1.5.1` release

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/diagnostics.md`
- Modify: `docs/cli-reference.md`
- Modify: `docs/attributes.md`
- Modify: `docs/migration.md`

- [ ] **Step 1: Write a failing stale-text scan against the exact `1.5.1` changes**

Run:

```bash
rg -n "nate123456|not yet supported by IoCTools|Temporarily disabled \(open generic registration issue\)" \
  README.md CHANGELOG.md docs IoCTools.Sample/Program.cs IoCTools.Sample/Services
```

Expected: FAIL with matches in package/docs/sample text that still describe old ownership or old open-generic posture.

- [ ] **Step 2: Update the changelog and README to describe the real public `1.5.1` release**

```md
## 1.5.1

- first public `1.5.x` release
- ships `ioc-tools evidence` and strengthened machine-readable CLI output
- makes `[Inject]` / `InjectConfiguration` compatibility-only guidance explicit
- hardens generator analysis failures so they do not degrade silently
- officially supports the common open-generic registration path
```

In `README.md`, keep the landing-page summary short and add one open-generic example:

```csharp
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class {}
```

- [ ] **Step 3: Update diagnostics and migration docs for IOC093 and the revised IOC094 posture**

```md
### IOC093
IoCTools could not fully analyze a type and skipped affected generation to avoid incomplete output.

### IOC094
Open-generic `typeof()` registration can usually be expressed through IoCTools attributes.
This is no longer an “unsupported” diagnostic in the common `typeof(IFoo<>), typeof(Foo<>)` path.
```

In `migration.md`, keep the explicit posture:

```md
- never introduce new `[Inject]`
- never introduce new `InjectConfiguration`
- prefer `[DependsOn]` and `[DependsOnConfiguration]`
```

- [ ] **Step 4: Re-run the stale-text scan and doc hygiene checks**

Run:

```bash
rg -n "nate123456|not yet supported by IoCTools|Temporarily disabled \(open generic registration issue\)" \
  README.md CHANGELOG.md docs IoCTools.Sample/Program.cs IoCTools.Sample/Services
git diff --check
```

Expected: PASS with no stale ownership/open-generic language left in the updated user-facing docs or sample text.

- [ ] **Step 5: Commit**

```bash
git add README.md CHANGELOG.md docs/diagnostics.md docs/cli-reference.md docs/attributes.md docs/migration.md
git commit -m "docs: finalize the 1.5.1 release story"
```

### Task 5: Verify, push, tag, and prove the public `1.5.1` release

**Files:**
- Verify all modified files from Tasks 1-4

- [ ] **Step 1: Run the full local verification slate**

Run:

```bash
./scripts/verify-release-surface.sh 1.5.1
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Tools.Cli.Tests/IoCTools.Tools.Cli.Tests.csproj --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --logger "console;verbosity=minimal"
env DOTNET_ROLL_FORWARD=LatestMajor dotnet test IoCTools.sln --logger "console;verbosity=minimal" -m:1
git diff --check
```

Expected: PASS with no failing test suites and no whitespace/diff hygiene issues.

- [ ] **Step 2: Pack all `1.5.1` artifacts locally one last time**

Run:

```bash
rm -rf artifacts
mkdir -p artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Abstractions/IoCTools.Abstractions.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Generator/IoCTools.Generator/IoCTools.Generator.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Tools.Cli/IoCTools.Tools.Cli.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.Testing/IoCTools.Testing/IoCTools.Testing.csproj -c Release -o artifacts
env DOTNET_ROLL_FORWARD=LatestMajor dotnet pack IoCTools.FluentValidation/IoCTools.FluentValidation/IoCTools.FluentValidation.csproj -c Release -o artifacts
ls artifacts
```

Expected: `IoCTools.Abstractions.1.5.1.nupkg`, `IoCTools.Generator.1.5.1.nupkg`, `IoCTools.Tools.Cli.1.5.1.nupkg`, `IoCTools.Testing.1.5.1.nupkg`, and `IoCTools.FluentValidation.1.5.1.nupkg`.

- [ ] **Step 3: Push the release candidate state to `origin/main`**

Run:

```bash
git status --short
git push origin main
```

Expected: clean status except intentional release artifacts or ignored files, and `origin/main` updated to the fully verified `1.5.1` state.

- [ ] **Step 4: Create and push the `v1.5.1` tag to trigger the release workflow**

Run:

```bash
git tag v1.5.1
git push origin v1.5.1
gh run watch --exit-status
```

Expected: the new `Release Packages` workflow completes successfully for the `v1.5.1` tag.

- [ ] **Step 5: Prove NuGet actually has the public `1.5.1` packages**

Run:

```bash
curl -fsSL https://api.nuget.org/v3-flatcontainer/ioctools.abstractions/index.json | rg '"1\\.5\\.1"'
curl -fsSL https://api.nuget.org/v3-flatcontainer/ioctools.generator/index.json | rg '"1\\.5\\.1"'
curl -fsSL https://api.nuget.org/v3-flatcontainer/ioctools.tools.cli/index.json | rg '"1\\.5\\.1"'
curl -fsSL https://api.nuget.org/v3-flatcontainer/ioctools.testing/index.json | rg '"1\\.5\\.1"'
curl -fsSL https://api.nuget.org/v3-flatcontainer/ioctools.fluentvalidation/index.json | rg '"1\\.5\\.1"'
```

Expected: every package endpoint contains `"1.5.1"`, confirming the first real public `1.5.x` release is live.

## Acceptance Criteria

- the repo has a clean `1.5.1` release workflow separate from normal CI
- all five packable packages share the same public `1.5.1` line and correct repository metadata
- generator analysis failures surface as visible diagnostics instead of silently degrading output
- the common open-generic registration path is supported and documented end-to-end
- `v1.5.1` is pushed and the public package feeds show `1.5.1`
