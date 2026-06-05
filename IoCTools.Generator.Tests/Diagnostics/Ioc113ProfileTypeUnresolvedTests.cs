namespace IoCTools.Generator.Tests;

using Xunit;

/// <summary>
/// Tests for IOC113 — unresolved profile type emits an explicit diagnostic instead of silently passing.
///
/// Revert-RED contract:
///   • Revert <c>ImplementsAutoDepsProfile</c> to return <c>true</c> for IErrorTypeSymbol →
///     the IOC113 path is never reached → no IOC113 emitted → <see cref="IOC113_fires_when_profile_type_is_error_symbol"/> goes RED.
///   • Revert <c>IsUnresolvedSymbol</c> check in Validate → unresolved type falls through to IOC097 path (or silent skip) → test RED.
///
/// Note: Roslyn enforces <c>where TProfile : IAutoDepsProfile</c> at compile time, so an
/// IErrorTypeSymbol for the profile argument arises only under broken/partial compilations
/// (missing references, incremental-build error recovery). The test simulates this by
/// deliberately omitting the IAutoDepsProfile reference from the compilation.
/// </summary>
public sealed class Ioc113ProfileTypeUnresolvedTests
{
    /// <summary>
    /// Drives the REAL generator with a source that references an unknown profile type
    /// (missing assembly reference). Roslyn resolves the type argument to IErrorTypeSymbol.
    /// Reverting the fix (return true for IErrorTypeSymbol) causes the validator to skip
    /// the profile silently — no IOC113 emitted — and this assertion goes RED.
    /// </summary>
    [Fact]
    public void IOC113_fires_when_profile_type_is_error_symbol()
    {
        // Arrange: AutoDepIn references a profile type that does not exist in this compilation
        // (UnknownProfile is not defined anywhere). Roslyn binds the type argument as IErrorTypeSymbol.
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<UnknownProfile, System.IDisposable>]
";
        // Act: compile WITHOUT defining UnknownProfile — Roslyn produces IErrorTypeSymbol for it.
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert: IOC113 must be emitted; silent-skip (fail-open) is the bug being fixed.
        // If the fix is reverted, no IOC113 appears and this assertion fails (test RED).
        result.Diagnostics.Should().Contain(d => d.Id == "IOC113",
            "an unresolved profile type (IErrorTypeSymbol) must emit IOC113 — not silently pass validation");
    }

    /// <summary>
    /// A valid, resolved profile type must NOT emit IOC113.
    /// </summary>
    [Fact]
    public void IOC113_does_not_fire_for_resolved_valid_profile()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
[assembly: AutoDepIn<TestNs.GoodProfile, TestNs.IFoo>]
namespace TestNs
{
    public class GoodProfile : IAutoDepsProfile { }
    public interface IFoo { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Where(d => d.Id == "IOC113").Should().BeEmpty(
            "a correctly resolved profile that implements IAutoDepsProfile must not emit IOC113");
    }

    /// <summary>
    /// An unresolved profile type on a class-level AutoDeps attribute must also emit IOC113.
    /// </summary>
    [Fact]
    public void IOC113_fires_for_unresolved_profile_on_class_level_AutoDeps()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
namespace TestNs
{
    [Scoped]
    [AutoDeps<UnknownProfile>]
    public partial class Svc { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.Diagnostics.Should().Contain(d => d.Id == "IOC113",
            "an unresolved profile type on a class-level AutoDeps attribute must emit IOC113");
    }
}
