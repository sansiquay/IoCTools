// Inline stubs so the fixture compiles in isolation, without IoCTools.Abstractions.
// This simulates a pre-1.6 consumer where IoCTools.Abstractions is absent.
// The migrate-inject command should detect this and emit the "Delete entirely disabled"
// notice while still performing the convert-only migration.

namespace IoCTools.Abstractions.Annotations
{
    public sealed class InjectAttribute : System.Attribute { }

    public sealed class ScopedAttribute : System.Attribute { }

    public sealed class DependsOnAttribute<T1> : System.Attribute
    {
        public DependsOnAttribute(string? memberName1 = null, bool external = false) { }
    }
}

namespace MigrateInjectPre16Project.Services
{
    using IoCTools.Abstractions.Annotations;

    public interface ILegacyClient
    {
        void Ping();
    }

    [Scoped]
    public partial class LegacyClient : ILegacyClient
    {
        public void Ping()
        {
        }
    }

    [Scoped]
    public partial class LegacyService
    {
        [Inject] private readonly ILegacyClient _client = null!;

        public void Run() => _client.Ping();
    }
}
