namespace IoCTools.Generator.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

public sealed class AutoDepsIntegrationTests
{
    private static Dictionary<string, string> OptIn(params (string Key, string Value)[] extra)
    {
        var d = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "false"
        };
        foreach (var (k, v) in extra) d[k] = v;
        return d;
    }

    [Fact]
    public void Universal_AutoDep_closed_type_appears_in_generated_constructor()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

[assembly: AutoDep<TestNs.IFoo>]

namespace TestNs;

[Scoped] public partial class Svc { }
public interface IFoo { }
[Scoped] public partial class FooImpl : IFoo { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var ctor = result.GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        ctor.Should().NotBeNull("the generator should emit a constructor for Svc");
        ctor!.Content.Should().Contain("IFoo");
    }

    [Fact]
    public void Builtin_ILogger_auto_detected_appears_as_closed_generic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped] public partial class Svc { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var ctor = result.GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        ctor.Should().NotBeNull("Svc should get a generated constructor now that built-in ILogger auto-detection is enabled");
        ctor!.Content.Should().Contain("ILogger<Svc>");
    }

    [Fact]
    public void Existing_DependsOn_deduplicates_auto_dep_of_same_type()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

[assembly: AutoDep<TestNs.IFoo>]

namespace TestNs;

[Scoped]
[DependsOn<IFoo>]
public partial class Svc { }
public interface IFoo { }
[Scoped] public partial class FooImpl : IFoo { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var ctor = result.GeneratedSources.First(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        // Explicit DependsOn wins over universal AutoDep; the resolver drops its entry so
        // there should be exactly one IFoo constructor parameter.
        var paramMatches = Regex.Matches(ctor.Content, @"\bIFoo\s+\w+\s*[,)]");
        paramMatches.Count.Should().Be(1);
    }

    [Fact]
    public void AutoDepsDisable_kill_switch_suppresses_all_auto_deps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

[assembly: AutoDep<TestNs.IFoo>]

namespace TestNs;

[Scoped] public partial class Svc { }
public interface IFoo { }
";
        var options = new Dictionary<string, string>
        {
            ["build_property.IoCToolsAutoDepsDisable"] = "true"
        };
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, options);
        var ctor = result.GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        // With the kill-switch on no auto-deps should flow through. Svc has no [Inject]/
        // [DependsOn], so there should either be no constructor emitted OR it should not
        // contain IFoo.
        if (ctor is not null)
            ctor.Content.Should().NotContain("IFoo");
    }

    [Fact]
    public void NoAutoDeps_on_service_suppresses_all_entries()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped]
[NoAutoDeps]
public partial class Svc { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());
        var ctor = result.GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        if (ctor is not null)
            ctor.Content.Should().NotContain("ILogger");
    }

    [Fact]
    public void AutoDetectLogger_opt_out_suppresses_ILogger()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped] public partial class Svc { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true,
            OptIn(("build_property.IoCToolsAutoDetectLogger", "false")));
        var ctor = result.GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("partial class Svc") && s.Content.Contains("Svc("));

        if (ctor is not null)
            ctor.Content.Should().NotContain("ILogger");
    }

    [Fact]
    public void Open_generic_logger_closes_per_level_for_inheritance()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped] public partial class BaseSvc { }
[Scoped] public partial class DerivedSvc : BaseSvc { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());

        var baseCtor = result.GeneratedSources.First(s =>
            s.Content.Contains("partial class BaseSvc") && s.Content.Contains("BaseSvc("));
        var derivedCtor = result.GeneratedSources.First(s =>
            s.Content.Contains("partial class DerivedSvc") && s.Content.Contains("DerivedSvc("));

        baseCtor.Content.Should().Contain("ILogger<BaseSvc>");
        derivedCtor.Content.Should().Contain("ILogger<DerivedSvc>");
        // The derived class forwards the base's closed logger via base(...).
        derivedCtor.Content.Should().Contain("ILogger<BaseSvc>");
        derivedCtor.Content.Should().Contain("base(");
    }

    [Fact]
    public void NoAutoDepOpen_on_derived_suppresses_derived_level_only()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped] public partial class BaseSvc { }
[Scoped]
[NoAutoDepOpen(typeof(ILogger<>))]
public partial class DerivedSvc : BaseSvc { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());

        var derivedCtor = result.GeneratedSources.First(s =>
            s.Content.Contains("partial class DerivedSvc") && s.Content.Contains("DerivedSvc("));

        // Derived's OWN logger is suppressed; base's logger is still forwarded via base(...).
        derivedCtor.Content.Should().NotContain("ILogger<DerivedSvc>");
        derivedCtor.Content.Should().Contain("ILogger<BaseSvc>");
    }

    [Fact]
    public void Three_level_inheritance_threads_all_loggers()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNs;

[Scoped] public partial class A { }
[Scoped] public partial class B : A { }
[Scoped] public partial class C : B { }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source, true, OptIn());

        var cCtor = result.GeneratedSources.First(s =>
            s.Content.Contains("partial class C") && s.Content.Contains("C("));

        // C's constructor has own logger, plus both ancestors' loggers for base-chaining.
        cCtor.Content.Should().Contain("ILogger<A>");
        cCtor.Content.Should().Contain("ILogger<B>");
        cCtor.Content.Should().Contain("ILogger<C>");
    }
}
