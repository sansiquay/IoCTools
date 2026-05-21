# IoCTools.Testing.Abstractions

Attribute abstractions for IoCTools.Testing test fixture generation.

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Testing.Abstractions?label=IoCTools.Testing.Abstractions)](https://www.nuget.org/packages/IoCTools.Testing.Abstractions)

## Installation

```bash
dotnet add package IoCTools.Testing
```

## Quick Start

```csharp
using IoCTools.Testing.Annotations;

[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void Test()
    {
        var sut = CreateSut(); // Auto-generated
    }
}
```

## Migrating existing hand-wired fixtures

If you already have test classes that hand-wire `Mock<T>` fields and a
`CreateSut() => new Service(...)` helper, point the IoCTools CLI at the test
project to surface them:

```bash
ioc-tools evidence \
  --project tests/MyApp.Tests/MyApp.Tests.csproj \
  --production-project src/MyApp/MyApp.csproj \
  --test-fixtures \
  --json
```

Each candidate is classified as `safe migration`, `partial migration`,
`already covered`, `not a target`, or `unknown/manual review`. The evidence
pass does not modify code — it lists the manual setup that can move to
`[Cover<T>]`. Full before/after example in
[`docs/testing.md` → CLI Fixture Evidence](../docs/testing.md#cli-fixture-evidence).

> **Do not** replace `[Cover<T>]` compile-time fixture generation with runtime
> scanning/reflection. That pathway is rejected by IoCTools doctrine — file an
> issue against IoCTools if you hit a gap in the generator instead of working
> around it in consumer test code.

[Full testing guide](../../docs/testing.md)
