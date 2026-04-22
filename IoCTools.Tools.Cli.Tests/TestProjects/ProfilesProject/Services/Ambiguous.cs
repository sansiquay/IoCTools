using IoCTools.Abstractions.Annotations;

namespace ProfilesProject.Services.Alt;

// A second profile whose simple name collides with a profile in the primary namespace (Defaults).
// Used by the CLI profiles command to verify ambiguous-name handling.
public sealed class Defaults : IAutoDepsProfile
{
}
