using IoCTools.Abstractions.Annotations;

namespace ProfilesProject.Services;

// Second profile with the simple name `Defaults` — collides with ProfilesProject.Services.Alt.Defaults
// so `ioc-tools profiles Defaults` is ambiguous and must print both fully-qualified candidates.
public sealed class Defaults : IAutoDepsProfile
{
}
