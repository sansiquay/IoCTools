namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    /// <summary>
    /// TDIAG01: Manual Mock field detected where auto-generated fixture exists
    /// </summary>
    public static readonly DiagnosticDescriptor ManualMockField = new(
        "TDIAG01",
        "Manual Mock field detected - consider using auto-generated fixture",
        "Test class '{0}' has manual Mock<{1}> field. The service '{2}' has an auto-generated fixture via [Cover<{2}>]. Consider using the generated fixture members instead.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Test classes with [Cover<TService>] attribute receive auto-generated Mock<T> fields. Remove manual mock declarations and use the generated _mockName fields.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag01");

    /// <summary>
    /// TDIAG02: Manual SUT construction detected where CreateSut() exists
    /// </summary>
    public static readonly DiagnosticDescriptor ManualSutConstruction = new(
        "TDIAG02",
        "Manual SUT construction detected - consider using CreateSut()",
        "Test class '{0}' manually constructs service '{1}'. The auto-generated CreateSut() method can wire all mocks automatically.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Test classes with [Cover<TService>] attribute receive an auto-generated CreateSut() factory method. Replace manual 'new Service(...)' with 'var sut = CreateSut();'.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag02");

    /// <summary>
    /// TDIAG03: Test class with manual mocks could use Cover<T> attribute
    /// </summary>
    public static readonly DiagnosticDescriptor CouldUseFixture = new(
        "TDIAG03",
        "Test class could use auto-generated fixture",
        "Test class '{0}' has Mock<{1}> fields matching service '{2}' dependencies. Consider adding [Cover<{2}>] to generate the fixture automatically.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Add [Cover<TService>] attribute to the test class to auto-generate Mock<T> fields, CreateSut() factory, and typed setup helpers. The test class must be marked partial.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag03");

    /// <summary>
    /// TDIAG04: Cover<T> references service without generated constructor
    /// </summary>
    public static readonly DiagnosticDescriptor ServiceMissingConstructor = new(
        "TDIAG04",
        "Cover<T> service has no generated constructor",
        "Test class '{0}' references service '{1}' which has no generated constructor. Ensure the service class is marked 'partial' and has service intent such as a lifetime attribute and [DependsOn] dependencies.",
        "IoCTools.Testing",
        DiagnosticSeverity.Error,
        true,
        "IoCTools generates constructors for partial classes with service intent (lifetime attributes, [DependsOn], [DependsOnConfiguration], [DependsOnOptions], or implementing interfaces). Add 'partial' to the service class or appropriate service attributes.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag04");

    /// <summary>
    /// TDIAG05: Cover<T> used on non-partial test class
    /// </summary>
    public static readonly DiagnosticDescriptor TestClassNotPartial = new(
        "TDIAG05",
        "Cover<T> requires partial test class",
        "Test class '{0}' uses [Cover<{1}>] but is not marked partial. Add the 'partial' modifier to enable fixture generation.",
        "IoCTools.Testing",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the test class declaration: 'public partial class {0}' to enable auto-generated fixture members.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag05");

    /// <summary>
    /// TDIAG06: Generated fixture member name collision detected
    /// </summary>
    public static readonly DiagnosticDescriptor FixtureMemberCollision = new(
        "TDIAG06",
        "Generated fixture member name collision",
        "Fixture for '{0}' detects collisions for parameter(s) '{1}'. Generated names may be ambiguous or unreadable. Consider renaming types or dependencies.",
        "IoCTools.Testing",
        DiagnosticSeverity.Warning,
        true,
        "Disambiguated fixture member names may be hard to read when multiple constructor parameters share similar type names. Rename types or add descriptive namespaces.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag06");

    /// <summary>
    /// TDIAG07: Setup/Sut method called after Sut property was accessed
    /// </summary>
    public static readonly DiagnosticDescriptor SetupAfterSutAccess = new(
        "TDIAG07",
        "Fixture helper called after Sut property was accessed",
        "Call to '{0}' after 'Sut' access in method '{1}'. Generated fixture helpers should be called in the Arrange phase, before the first access to the Sut property.",
        "IoCTools.Testing",
        DiagnosticSeverity.Warning,
        true,
        "Generated fixture helpers (Setup*, Configure*, Use*) configure mock dependencies and should be called before accessing the Sut property. Sut triggers lazy construction via CreateSut(), so calling helpers afterward has no effect on the already-constructed service.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag07");

    /// <summary>
    /// TDIAG08: Manual construction of IoCTools-owned service detected, could use [Cover&lt;T&gt;]
    /// Default severity: Info. Legitimate manual construction exists (e.g. surface tests with bespoke stubs).
    /// Can be escalated to Warning or Error by setting
    /// &lt;IoCToolsTestingDiagnosticSeverity&gt;Warning&lt;/IoCToolsTestingDiagnosticSeverity&gt; in the consumer project.
    /// </summary>
    public static readonly DiagnosticDescriptor CouldUseCoverAttribute = new(
        "TDIAG08",
        "Manual service construction detected - consider using [Cover<T>]",
        "Test class '{0}' manually constructs service '{1}' which is IoCTools-managed. Consider adding [Cover<{1}>] to generate fixture members automatically.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Add [Cover<TService>] attribute to the test class (must be partial) to auto-generate Mock<T> fields, CreateSut() factory, typed setup helpers, configuration helpers, and options helpers. Legitimate manual construction (e.g. surface tests with custom stubs) can suppress or ignore this suggestion.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag08");
}
