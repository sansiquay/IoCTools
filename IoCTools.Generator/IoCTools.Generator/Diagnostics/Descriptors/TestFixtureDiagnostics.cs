namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    /// <summary>
    /// TDIAG-01: Manual Mock field detected where auto-generated fixture exists
    /// </summary>
    public static readonly DiagnosticDescriptor ManualMockField = new(
        "TDIAG-01",
        "Manual Mock field detected - consider using auto-generated fixture",
        "Test class '{0}' has manual Mock<{1}> field. The service '{2}' has an auto-generated fixture via [Cover<{2}>]. Consider using the generated fixture members instead.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Test classes with [Cover<TService>] attribute receive auto-generated Mock<T> fields. Remove manual mock declarations and use the generated _mockName fields.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag-01");

    /// <summary>
    /// TDIAG-02: Manual SUT construction detected where CreateSut() exists
    /// </summary>
    public static readonly DiagnosticDescriptor ManualSutConstruction = new(
        "TDIAG-02",
        "Manual SUT construction detected - consider using CreateSut()",
        "Test class '{0}' manually constructs service '{1}'. The auto-generated CreateSut() method can wire all mocks automatically.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Test classes with [Cover<TService>] attribute receive an auto-generated CreateSut() factory method. Replace manual 'new Service(...)' with 'var sut = CreateSut();'.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag-02");

    /// <summary>
    /// TDIAG-03: Test class with manual mocks could use Cover<T> attribute
    /// </summary>
    public static readonly DiagnosticDescriptor CouldUseFixture = new(
        "TDIAG-03",
        "Test class could use auto-generated fixture",
        "Test class '{0}' has Mock<{1}> fields matching service '{2}' dependencies. Consider adding [Cover<{2}>] to generate the fixture automatically.",
        "IoCTools.Testing",
        DiagnosticSeverity.Info,
        true,
        "Add [Cover<TService>] attribute to the test class to auto-generate Mock<T> fields, CreateSut() factory, and typed setup helpers. The test class must be marked partial.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag-03");

    /// <summary>
    /// TDIAG-04: Cover<T> references service without generated constructor
    /// </summary>
    public static readonly DiagnosticDescriptor ServiceMissingConstructor = new(
        "TDIAG-04",
        "Cover<T> service has no generated constructor",
        "Test class '{0}' references service '{1}' which has no generated constructor. Ensure the service class is marked 'partial' and has a lifetime attribute or [Inject]/[DependsOn] fields.",
        "IoCTools.Testing",
        DiagnosticSeverity.Error,
        true,
        "IoCTools generates constructors for partial classes with service intent (lifetime attributes, [Inject], [DependsOn], or implementing interfaces). Add 'partial' to the service class or appropriate service attributes.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag-04");

    /// <summary>
    /// TDIAG-05: Cover<T> used on non-partial test class
    /// </summary>
    public static readonly DiagnosticDescriptor TestClassNotPartial = new(
        "TDIAG-05",
        "Cover<T> requires partial test class",
        "Test class '{0}' uses [Cover<{1}>] but is not marked partial. Add the 'partial' modifier to enable fixture generation.",
        "IoCTools.Testing",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the test class declaration: 'public partial class {0}' to enable auto-generated fixture members.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#tdiag-05");
}
