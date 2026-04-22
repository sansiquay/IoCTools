namespace IoCTools.Tools.Cli;

internal sealed record EvidenceBundle(
    EvidenceProject project,
    EvidenceServices services,
    EvidenceTypeEvidence? typeEvidence,
    EvidenceDiagnostics diagnostics,
    EvidenceConfiguration configuration,
    EvidenceValidators? validators,
    EvidenceArtifacts artifacts,
    IReadOnlyList<EvidenceMigrationHint> migrationHints);

internal sealed record EvidenceProject(
    string path,
    string name,
    string configuration,
    string? framework);

internal sealed record EvidenceServices(
    int serviceCount,
    int configurationCount,
    IReadOnlyList<EvidenceRegistration> registrations);

internal sealed record EvidenceRegistration(
    string kind,
    string? serviceType,
    string? implementationType,
    string? lifetime,
    bool isConditional,
    bool usesFactory);

internal sealed record EvidenceTypeEvidence(
    string typeName,
    string filePath,
    IReadOnlyList<EvidenceDependency> dependencies,
    IReadOnlyList<EvidenceConfigurationBinding> configuration,
    IReadOnlyList<EvidenceRegistration> registrations,
    IReadOnlyList<EvidenceAutoDep> autoDeps);

internal sealed record EvidenceAutoDep(
    string typeName,
    string source,
    string suppress);

internal sealed record EvidenceDependency(
    string fieldName,
    string typeName,
    string source,
    bool isExternal);

internal sealed record EvidenceConfigurationBinding(
    string fieldName,
    string typeName,
    string configurationKey,
    bool required,
    bool supportsReloading);

internal sealed record EvidenceDiagnostics(
    int total,
    int errorCount,
    int warningCount,
    int infoCount,
    bool hasErrors,
    IReadOnlyList<EvidenceDiagnostic> items);

internal sealed record EvidenceDiagnostic(
    string id,
    string severity,
    string message,
    string location);

internal sealed record EvidenceConfiguration(
    int requiredBindings,
    int settingsKeysDiscovered,
    IReadOnlyList<string> missingKeys,
    IReadOnlyList<string> allKeys);

internal sealed record EvidenceValidators(
    int count,
    IReadOnlyList<string> validatorNames);

internal sealed record EvidenceArtifacts(
    string outputDirectory,
    EvidenceProfile profile,
    IReadOnlyList<EvidenceGeneratedArtifact> generatedArtifacts,
    EvidenceCompare? compare);

internal sealed record EvidenceGeneratedArtifact(
    string artifactId,
    string fileName,
    string path,
    string fingerprint,
    long sizeBytes);

internal sealed record EvidenceProfile(
    double elapsedMilliseconds,
    int serviceCount,
    int configurationCount);

internal sealed record EvidenceCompare(
    string outputDirectory,
    string baselineDirectory,
    IReadOnlyList<string> changedArtifacts,
    IReadOnlyList<EvidenceArtifactDelta> deltas);

internal sealed record EvidenceArtifactDelta(
    string artifactId,
    string fileName,
    string status,
    string? baselinePath,
    string? currentPath,
    string? baselineFingerprint,
    string? currentFingerprint);

internal sealed record EvidenceMigrationHint(
    string service,
    string source,
    string member,
    string message);
