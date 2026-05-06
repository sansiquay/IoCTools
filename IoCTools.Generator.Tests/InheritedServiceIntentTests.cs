namespace IoCTools.Generator.Tests;

using FluentAssertions;
using Xunit;

/// <summary>
/// Tests for Option B fix: a bare derived partial class inheriting from an IoCTools-managed
/// base should have a forwarding constructor generated, even when the derived class carries
/// no IoCTools attributes of its own.
/// </summary>
public class InheritedServiceIntentTests
{
    // -----------------------------------------------------------------------
    // Case 1: base has [DependsOn] → derived should get a ctor
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedPartialClass_BaseHasDependsOn_CtorGenerated()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }
public interface IAdapter { }

[DependsOn<IFoo>]
public partial class BaseAdapter : IAdapter
{
}

public partial class DerivedAdapter : BaseAdapter
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("DerivedAdapter");
        ctor.Should().NotBeNull(
            "derived partial class whose base has [DependsOn] should have a forwarding constructor generated");
    }

    // -----------------------------------------------------------------------
    // Case 2: base has [Scoped] only → derived should get a ctor
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedPartialClass_BaseHasScopedLifetime_CtorGenerated()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
public partial class BaseService : IService
{
}

public partial class DerivedService : BaseService
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("DerivedService");
        ctor.Should().NotBeNull(
            "derived partial class whose base has [Scoped] should have a forwarding constructor generated");
    }

    // -----------------------------------------------------------------------
    // Case 3: base is a plain abstract class with no IoCTools attrs → ctor NOT generated
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedPartialClass_BaseHasNoIoCToolsAttrs_CtorNotGenerated()
    {
        var source = @"
namespace Test;

public abstract class PlainBase
{
    public PlainBase() { }
}

public partial class DerivedService : PlainBase
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("DerivedService");
        ctor.Should().BeNull(
            "derived partial class whose base has no IoCTools attributes should not trigger generator");
    }

    // -----------------------------------------------------------------------
    // Case 4: class directly implements IDisposable only
    // Pre-existing behavior: isPartialWithInterfaces triggers on symbol.Interfaces.Any(),
    // so a partial class with ANY direct interface (including IDisposable) gets a ctor.
    // My fix must NOT break this existing path.
    // -----------------------------------------------------------------------

    [Fact]
    public void PartialClass_ImplementsOnlyIDisposable_PreserveExistingBehavior()
    {
        var source = @"
using System;

namespace Test;

public partial class DisposableService : IDisposable
{
    public void Dispose() { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Pre-existing behavior: symbol.Interfaces.Any() → isPartialWithInterfaces = true → ctor generated.
        // The inheritsManaged path is NOT what triggers this — the original isPartialWithInterfaces check does.
        // This test documents/preserves that existing behavior is unchanged by the fix.
        var ctor = result.GetConstructorSource("DisposableService");
        ctor.Should().NotBeNull(
            "existing behavior: partial class with any direct interface generates a ctor via isPartialWithInterfaces");
    }

    // -----------------------------------------------------------------------
    // Case 4b: bare partial class with NO interfaces and NO managed base → ctor NOT generated
    // -----------------------------------------------------------------------

    [Fact]
    public void BarePartialClass_NoInterfacesNoManagedBase_CtorNotGenerated()
    {
        var source = @"
namespace Test;

public abstract class PlainAbstractBase
{
    protected PlainAbstractBase() { }
}

public partial class DerivedService : PlainAbstractBase
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("DerivedService");
        ctor.Should().BeNull(
            "partial class deriving from a plain non-IoCTools base with no interfaces should not trigger generator");
    }

    // -----------------------------------------------------------------------
    // Case 5: base has [Singleton] → derived should get a ctor
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedPartialClass_BaseHasSingletonLifetime_CtorGenerated()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IOrchestrator { }

[Singleton]
public partial class BaseOrchestrator : IOrchestrator
{
}

public partial class ConcreteOrchestrator : BaseOrchestrator
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("ConcreteOrchestrator");
        ctor.Should().NotBeNull(
            "derived partial class whose base has [Singleton] should have a forwarding constructor generated");
    }

    // -----------------------------------------------------------------------
    // Case 6: multi-level inheritance — grandparent has [DependsOn], parent plain
    // → derived should still get a ctor (chain walking)
    // -----------------------------------------------------------------------

    [Fact]
    public void DerivedPartialClass_GrandparentHasDependsOn_CtorGenerated()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDep { }
public interface IGrandparent { }

[DependsOn<IDep>]
public partial class GrandparentService : IGrandparent
{
}

public partial class ParentService : GrandparentService
{
}

public partial class ChildService : ParentService
{
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ctor = result.GetConstructorSource("ChildService");
        ctor.Should().NotBeNull(
            "derived partial class with IoCTools-managed grandparent should have a forwarding constructor generated");
    }
}
