using IoCTools.Abstractions.Annotations;

namespace DoctorAutoDepsProject.Services;

// A profile declared but never attached to any service via AutoDepsApply/Glob.
// The CLI doctor preflight should flag this as dead.
public sealed class OrphanProfile : IAutoDepsProfile
{
}

[Scoped]
public partial class SimpleService
{
    public void Run()
    {
    }
}
