namespace IoCTools.Generator.Diagnostics;

/// <summary>
///     Declares which kind of project a diagnostic should fire in.
///     The framework consults this scope (via <see cref="DiagnosticGate" />) before
///     emitting any scope-aware diagnostic, so individual analyzers do not have to
///     re-implement test/production carve-outs.
/// </summary>
internal enum AnalysisScope
{
    /// <summary>
    ///     Default. Fires in both production and test projects. Use for correctness
    ///     defects (broken codegen, lifetime mismatches, contract violations) where the
    ///     diagnostic is equally valuable everywhere.
    /// </summary>
    Both = 0,

    /// <summary>
    ///     Fires only in non-test projects. Use for style/convention diagnostics where
    ///     test projects intentionally diverge — for example, manual registrations that
    ///     re-bind IoCTools-managed services for fakes/stubs/spies.
    /// </summary>
    Production = 1,

    /// <summary>
    ///     Fires only in test projects. Reserved for diagnostics that police
    ///     test-specific patterns (currently unused; defined for completeness).
    /// </summary>
    Test = 2
}
