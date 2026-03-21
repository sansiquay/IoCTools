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

[Full testing guide](../../docs/testing.md)
