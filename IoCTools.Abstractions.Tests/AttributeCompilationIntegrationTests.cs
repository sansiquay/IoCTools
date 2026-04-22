namespace IoCTools.Abstractions.Tests;

using FluentAssertions;
using IoCTools.Abstractions.Annotations;
using Xunit;

public sealed class AttributeCompilationIntegrationTests
{
    public sealed class TestProfile : IAutoDepsProfile { }
    public interface IExample { }
    public class ExampleBase { }
    public class ExampleService : ExampleBase { }

    [Fact]
    public void All_attributes_can_be_constructed_from_user_code()
    {
        var autoDep = new AutoDepAttribute<IExample>();
        var autoDepOpen = new AutoDepOpenAttribute(typeof(System.Collections.Generic.IEnumerable<>));
        var autoDepIn = new AutoDepInAttribute<TestProfile, IExample>();
        var autoDepsApply = new AutoDepsApplyAttribute<TestProfile, ExampleBase>();
        var autoDepsApplyGlob = new AutoDepsApplyGlobAttribute<TestProfile>("*.Test.*");
        var autoDeps = new AutoDepsAttribute<TestProfile>();
        var noAutoDeps = new NoAutoDepsAttribute();
        var noAutoDep = new NoAutoDepAttribute<IExample>();
        var noAutoDepOpen = new NoAutoDepOpenAttribute(typeof(System.Collections.Generic.IEnumerable<>));

        (autoDep, autoDepOpen, autoDepIn, autoDepsApply, autoDepsApplyGlob,
         autoDeps, noAutoDeps, noAutoDep, noAutoDepOpen).Should().NotBeNull();
    }
}
