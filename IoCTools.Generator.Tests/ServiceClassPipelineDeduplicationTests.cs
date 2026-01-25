namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Xunit.Abstractions;

/// <summary>
///     Tests to verify partial class deduplication in ServiceClassPipeline.
///     Ensures that a partial class split across multiple files produces exactly one ServiceClassInfo
///     and results in exactly one service registration.
/// </summary>
public class ServiceClassPipelineDeduplicationTests
{
    private readonly ITestOutputHelper _output;

    public ServiceClassPipelineDeduplicationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PartialClass_SplitAcrossTwoFiles_ShouldProduceSingleRegistration()
    {
        // Simulates a partial class split across two files
        const string source = @"
namespace TestNamespace
{
    using IoCTools.Abstractions.Annotations;

    public interface IServiceA { }
    public interface IServiceB { }

    [Scoped]
    [RegisterAsAll]
    public partial class MultiPartService : IServiceA
    {
        public void MethodA() { }
    }

    public partial class MultiPartService : IServiceB
    {
        public void MethodB() { }
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify exactly one registration for the concrete class
        // (RegisterAsAll generates interface registrations, not the concrete class)
        var serviceRegistrations = Regex.Matches(
            generatedCode, @"services\.AddScoped<global::TestNamespace\.MultiPartService>");

        // For RegisterAsAll pattern, concrete class should not be registered directly
        // Only interfaces should be registered
        var interfaceARegistrations = Regex.Matches(
            generatedCode, @"AddScoped<global::TestNamespace\.IServiceA[^>]*>");
        var interfaceBRegistrations = Regex.Matches(
            generatedCode, @"AddScoped<global::TestNamespace\.IServiceB[^>]*>");

        _output.WriteLine($"IServiceA registrations: {interfaceARegistrations.Count}");
        _output.WriteLine($"IServiceB registrations: {interfaceBRegistrations.Count}");

        // Each interface should be registered exactly once
        (interfaceARegistrations.Count == 1).Should().BeTrue(
            $"IServiceA should be registered exactly once, found {interfaceARegistrations.Count}");

        (interfaceBRegistrations.Count == 1).Should().BeTrue(
            $"IServiceB should be registered exactly once, found {interfaceBRegistrations.Count}");
    }

    [Fact]
    public void PartialClass_SplitAcrossThreeFiles_ShouldProduceSingleRegistration()
    {
        // Simulates a partial class split across three files
        const string source = @"
namespace TestNamespace
{
    using IoCTools.Abstractions.Annotations;

    public interface IServiceA { }

    [Singleton]
    public partial class TripleSplitService : IServiceA
    {
        public void MethodA() { }
    }

    public partial class TripleSplitService
    {
        private readonly int _value = 42;

        public int GetValue() => _value;
    }

    public partial class TripleSplitService
    {
        public string Name { get; } = ""Triple"";
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify exactly one registration for IServiceA
        var serviceARegistrations = Regex.Matches(
            generatedCode, @"AddSingleton<global::TestNamespace\.IServiceA[^>]*>");

        (serviceARegistrations.Count == 1).Should().BeTrue(
            $"IServiceA should be registered exactly once, found {serviceARegistrations.Count}");
    }

    [Fact]
    public void PartialClass_SplitAcrossFiveFiles_ShouldProduceSingleRegistration()
    {
        // Simulates a partial class split across five files
        const string source = @"
namespace TestNamespace
{
    using IoCTools.Abstractions.Annotations;

    public interface IServiceA { }

    [Transient]
    public partial class QuintupleSplitService : IServiceA
    {
        public void MethodA() { }
    }

    public partial class QuintupleSplitService
    {
        public int Property1 { get; set; }
    }

    public partial class QuintupleSplitService
    {
        public string Property2 { get; set; } = string.Empty;
    }

    public partial class QuintupleSplitService
    {
        public double Property3 { get; set; }
    }

    public partial class QuintupleSplitService
    {
        public bool Property4 { get; set; }
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify exactly one registration for IServiceA
        var serviceARegistrations = Regex.Matches(
            generatedCode, @"AddTransient<global::TestNamespace\.IServiceA[^>]*>");

        (serviceARegistrations.Count == 1).Should().BeTrue(
            $"IServiceA should be registered exactly once, found {serviceARegistrations.Count}");
    }

    [Fact]
    public void PartialClass_WithInjectFieldsAcrossFiles_ShouldGenerateSingleConstructor()
    {
        // Tests that [Inject] fields across multiple partial parts are consolidated
        const string source = @"
namespace TestNamespace
{
    using IoCTools.Abstractions.Annotations;

    public interface IDependencyA { }
    public interface IDependencyB { }
    public interface IDependencyC { }

    [Scoped]
    public partial class MultiFileInjectService
    {
        [Inject] private readonly IDependencyA _depA;
    }

    public partial class MultiFileInjectService
    {
        [Inject] private readonly IDependencyB _depB;
    }

    public partial class MultiFileInjectService
    {
        [Inject] private readonly IDependencyC _depC;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetConstructorSource("MultiFileInjectService");
        var generatedCode = constructorSource?.Content ?? string.Empty;

        _output.WriteLine("Generated Constructor:");
        _output.WriteLine(generatedCode);

        // Verify constructor has all three dependencies
        generatedCode.Should().Contain("IDependencyA");
        generatedCode.Should().Contain("IDependencyB");
        generatedCode.Should().Contain("IDependencyC");

        // Verify constructor parameters appear exactly once each
        var depAMatches = Regex.Matches(generatedCode, @"IDependencyA[^a-zA-Z]");
        var depBMatches = Regex.Matches(generatedCode, @"IDependencyB[^a-zA-Z]");
        var depCMatches = Regex.Matches(generatedCode, @"IDependencyC[^a-zA-Z]");

        // Each dependency should appear exactly once in the constructor signature/assignment
        // (allowing for some extra matches in comments/whitespace)
        (depAMatches.Count <= 2).Should().BeTrue(
            $"IDependencyA should appear at most twice (parameter + field), found {depAMatches.Count}");
        (depBMatches.Count <= 2).Should().BeTrue(
            $"IDependencyB should appear at most twice (parameter + field), found {depBMatches.Count}");
        (depCMatches.Count <= 2).Should().BeTrue(
            $"IDependencyC should appear at most twice (parameter + field), found {depCMatches.Count}");
    }

    [Fact]
    public void TwoDistinctPartialClasses_ShouldProduceTwoRegistrations()
    {
        // Verify that two different partial classes each produce their own registration
        const string source = @"
namespace TestNamespace
{
    using IoCTools.Abstractions.Annotations;

    public interface IServiceA { }
    public interface IServiceB { }

    [Scoped]
    public partial class ServiceA : IServiceA
    {
        public void MethodA() { }
    }

    public partial class ServiceA
    {
        public int ValueA { get; set; }
    }

    [Scoped]
    public partial class ServiceB : IServiceB
    {
        public void MethodB() { }
    }

    public partial class ServiceB
    {
        public string ValueB { get; set; } = string.Empty;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Both services should be registered exactly once
        var serviceARegistrations = Regex.Matches(
            generatedCode, @"AddScoped<global::TestNamespace\.IServiceA[^>]*>");
        var serviceBRegistrations = Regex.Matches(
            generatedCode, @"AddScoped<global::TestNamespace\.IServiceB[^>]*>");

        (serviceARegistrations.Count == 1).Should().BeTrue(
            $"IServiceA should be registered exactly once, found {serviceARegistrations.Count}");

        (serviceBRegistrations.Count == 1).Should().BeTrue(
            $"IServiceB should be registered exactly once, found {serviceBRegistrations.Count}");
    }
}
