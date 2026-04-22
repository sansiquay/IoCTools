namespace IoCTools.Generator.Tests;

public sealed class DependsOnSparseMemberNameTests
{
    [Fact]
    public void Sparse_memberName_preserves_slot_alignment()
    {
        // Regression: user writes memberName2 without memberName1. The compacted-list bug
        // in AttributeParser.GetDependsOnOptionsFromAttribute would have assigned "_customBar"
        // to slot 0 (IFoo) instead of slot 1 (IBar).
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }
public interface IBar { }

[Scoped]
[DependsOn<IFoo, IBar>(memberName2: ""_customBar"")]
public partial class Svc { }

[Scoped] public partial class FooImpl : IFoo { }
[Scoped] public partial class BarImpl : IBar { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var ctor = result.GetRequiredConstructorSource("Svc").Content;

        // Slot 0 (IFoo) -> default name (_foo), slot 1 (IBar) -> explicit name (_customBar).
        ctor.Should().Contain("private readonly IFoo _foo;");
        ctor.Should().Contain("private readonly IBar _customBar;");
        ctor.Should().NotContain("_customBar.*IFoo");  // never IFoo _customBar
        ctor.Should().NotContain("IBar _bar");          // slot 1 must not use default
    }

    [Fact]
    public void Explicit_memberName1_and_memberName2_both_honored()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }
public interface IBar { }

[Scoped]
[DependsOn<IFoo, IBar>(memberName1: ""_customFoo"", memberName2: ""_customBar"")]
public partial class Svc { }

[Scoped] public partial class FooImpl : IFoo { }
[Scoped] public partial class BarImpl : IBar { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var ctor = result.GetRequiredConstructorSource("Svc").Content;

        ctor.Should().Contain("IFoo _customFoo;");
        ctor.Should().Contain("IBar _customBar;");
    }

    [Fact]
    public void Sparse_memberName1_only_preserves_slot_alignment()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }
public interface IBar { }

[Scoped]
[DependsOn<IFoo, IBar>(memberName1: ""_customFoo"")]
public partial class Svc { }

[Scoped] public partial class FooImpl : IFoo { }
[Scoped] public partial class BarImpl : IBar { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var ctor = result.GetRequiredConstructorSource("Svc").Content;

        ctor.Should().Contain("IFoo _customFoo;");
        ctor.Should().Contain("IBar _bar;");  // default name for slot 1
    }
}
