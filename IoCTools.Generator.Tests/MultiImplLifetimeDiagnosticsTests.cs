namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for the multi-implementation lifetime diagnostics fix:
///     <list type="bullet">
///         <item>The resolver returns every impl candidate (not iteration-order-dependent first match).</item>
///         <item>IOC012/013/087 fire only when ALL impls violate, and the message enumerates each impl + lifetime.</item>
///         <item>IOC110 (new) fires when SOME impls violate (mixed) — the ambiguous case.</item>
///         <item>Implicit-vs-explicit lifetime distinction is rendered in the message.</item>
///         <item>Determinism: identical compilations produce identical messages (sorted candidate list).</item>
///     </list>
/// </summary>
public class MultiImplLifetimeDiagnosticsTests
{
    [Fact]
    public void IOC012_AllImplsViolate_FiresIOC012WithEnumeratedImpls()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

public partial class SystemClock : IClock { }   // implicit Scoped (no attribute)
public partial class WallClock : IClock { }     // implicit Scoped (no attribute)

[Singleton]
public partial class Consumer
{
    [DependsOn<IClock>] private partial class _Marker { }
}";

        // Use [DependsOn] form via Inject field for compatibility with the test harness.
        sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

public partial class SystemClock : IClock { }
public partial class WallClock : IClock { }

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc110 = result.GetDiagnosticsByCode("IOC110");

        ioc012.Should().ContainSingle("all impls of IClock are implicit Scoped, so all violate the Singleton consumer");
        ioc110.Should().BeEmpty("IOC110 only fires when SOME impls violate; here all violate");

        var msg = ioc012[0].GetMessage();
        msg.Should().Contain("Consumer");
        msg.Should().Contain("IClock");
        msg.Should().Contain("SystemClock");
        msg.Should().Contain("WallClock");
        msg.Should().Contain("Scoped");
        msg.Should().Contain("implicit", "implicit-vs-explicit qualifier should appear for unattributed impls");
    }

    [Fact]
    public void IOC110_SomeImplsViolate_FiresIOC110_NotIOC012()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

[Singleton]
public partial class SystemClock : IClock { }   // safe — Singleton

public partial class WallClock : IClock { }     // implicit Scoped — would violate

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");
        var ioc110 = result.GetDiagnosticsByCode("IOC110");

        ioc012.Should().BeEmpty("IOC012 must NOT fire — at least one impl is lifetime-compatible");
        ioc013.Should().BeEmpty();
        ioc110.Should().ContainSingle("mixed impls (Singleton + implicit Scoped) is the ambiguous case");

        var msg = ioc110[0].GetMessage();
        msg.Should().Contain("Consumer");
        msg.Should().Contain("IClock");
        msg.Should().Contain("SystemClock");
        msg.Should().Contain("WallClock");
        msg.Should().Contain("Singleton");
        msg.Should().Contain("Scoped");
        msg.Should().Contain("registration order",
            "IOC110 explains why the analyzer cannot pick — DI registration order decides at runtime");
    }

    [Fact]
    public void NoImplsViolate_NoDiagnostic()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

[Singleton]
public partial class SystemClock : IClock { }

[Singleton]
public partial class WallClock : IClock { }

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        result.GetDiagnosticsByCode("IOC012").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC013").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC087").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC110").Should().BeEmpty();
    }

    [Fact]
    public void Determinism_TwoIdenticalCompilations_ProduceIdenticalMessages()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

public partial class ZebraClock : IClock { }   // implicit Scoped — sorted last by name
public partial class AlphaClock : IClock { }   // implicit Scoped — sorted first by name
public partial class MidClock   : IClock { }   // implicit Scoped — sorted middle

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var first = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var second = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var firstMsg = first.GetDiagnosticsByCode("IOC012").Single().GetMessage();
        var secondMsg = second.GetDiagnosticsByCode("IOC012").Single().GetMessage();

        firstMsg.Should().Be(secondMsg, "candidate list must be deterministically sorted across runs");

        // Sanity: alpha appears before mid appears before zebra in the message.
        var iAlpha = firstMsg.IndexOf("AlphaClock");
        var iMid = firstMsg.IndexOf("MidClock");
        var iZebra = firstMsg.IndexOf("ZebraClock");
        iAlpha.Should().BeGreaterOrEqualTo(0);
        iMid.Should().BeGreaterThan(iAlpha);
        iZebra.Should().BeGreaterThan(iMid);
    }

    [Fact]
    public void ImplicitVsExplicit_QualifierAppearsForImplicit_AbsentForExplicit()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

[Scoped]
public partial class ExplicitScopedClock : IClock { }  // explicitly [Scoped]

public partial class ImplicitScopedClock : IClock { }  // implicit Scoped (no attr)

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        ioc012.Should().ContainSingle();

        var msg = ioc012[0].GetMessage();

        // Implicit row should carry the qualifier.
        var implicitIndex = msg.IndexOf("ImplicitScopedClock");
        implicitIndex.Should().BeGreaterOrEqualTo(0);
        var implicitLineEnd = msg.IndexOf('\n', implicitIndex);
        if (implicitLineEnd < 0) implicitLineEnd = msg.Length;
        var implicitLine = msg.Substring(implicitIndex, implicitLineEnd - implicitIndex);
        implicitLine.Should().Contain("implicit", "rows for unattributed impls must say so");

        // Explicit row should NOT carry the qualifier.
        var explicitIndex = msg.IndexOf("ExplicitScopedClock");
        explicitIndex.Should().BeGreaterOrEqualTo(0);
        var explicitLineEnd = msg.IndexOf('\n', explicitIndex);
        if (explicitLineEnd < 0) explicitLineEnd = msg.Length;
        var explicitLine = msg.Substring(explicitIndex, explicitLineEnd - explicitIndex);
        explicitLine.Should().NotContain("implicit",
            "rows for explicitly attributed impls must NOT carry the implicit qualifier");
    }

    [Fact]
    public void IOC013_AllTransientImpls_FiresWithEnumeratedImpls()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IClock { }

[Transient]
public partial class FastClock : IClock { }

[Transient]
public partial class SlowClock : IClock { }

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IClock _clock;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc013 = result.GetDiagnosticsByCode("IOC013");
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc110 = result.GetDiagnosticsByCode("IOC110");

        ioc012.Should().BeEmpty();
        ioc110.Should().BeEmpty("all impls violate uniformly — fires the canonical IOC013, not IOC110");
        ioc013.Should().ContainSingle();

        var msg = ioc013[0].GetMessage();
        msg.Should().Contain("Consumer");
        msg.Should().Contain("IClock");
        msg.Should().Contain("FastClock");
        msg.Should().Contain("SlowClock");
        msg.Should().Contain("Transient");
    }

    [Fact]
    public void IOC087_TransientConsumer_AllScopedImpls_FiresWithEnumeratedImpls()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;

public interface IDb { }

[Scoped]
public partial class PrimaryDb : IDb { }

[Scoped]
public partial class ReplicaDb : IDb { }

[Transient]
public partial class Consumer
{
    [Inject] private readonly IDb _db;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc087 = result.GetDiagnosticsByCode("IOC087");
        var ioc110 = result.GetDiagnosticsByCode("IOC110");

        ioc110.Should().BeEmpty();
        ioc087.Should().ContainSingle();

        var msg = ioc087[0].GetMessage();
        msg.Should().Contain("Consumer");
        msg.Should().Contain("IDb");
        msg.Should().Contain("PrimaryDb");
        msg.Should().Contain("ReplicaDb");
        msg.Should().Contain("Scoped");
    }
}
