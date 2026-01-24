namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class CollectionDependencyTests
{
    [Theory]
    [InlineData("System.Collections.Generic.IReadOnlyCollection<Test.IFoo>")]
    public void AllowedCollections_DoNotWarn(string typeName)
    {
        var source = $@"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IFoo {{ }}

[DependsOn<{typeName}>]
public partial class Consumer {{ }}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC045").Should().BeEmpty();
    }

    [Fact]
    public void HashSetCollection_WarnsIOC045()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IFoo { }

[DependsOn<HashSet<IFoo>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diags = result.GetDiagnosticsByCode("IOC045");
        diags.Should().ContainSingle();
        diags[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DictionaryCollection_WarnsIOC045()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IFoo { }

[DependsOn<Dictionary<string, IFoo>>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC045").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ArrayCollection_WarnsIOC045()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFoo { }

[DependsOn<IFoo[]>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC045").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void CustomCollectionImplementingIReadOnlyCollection_WarnsIOC045()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections;
using System.Collections.Generic;

namespace Test;

public interface IFoo { }

public class FooBag : IReadOnlyCollection<IFoo>
{
    public int Count => 0;
    public IEnumerator<IFoo> GetEnumerator() => System.Linq.Enumerable.Empty<IFoo>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[DependsOn<FooBag>]
public partial class Consumer { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC045").Should().ContainSingle()
            .Which.Severity.Should().Be(DiagnosticSeverity.Warning);
    }
}
