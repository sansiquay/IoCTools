using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace ProfilesProject.Services;

// Two profiles in this namespace + one profile in an alternate namespace with a colliding
// simple name to exercise disambiguation in the CLI profiles subcommand.

public sealed class ControllerDefaults : IAutoDepsProfile
{
}

public sealed class WorkerDefaults : IAutoDepsProfile
{
}

// Services + interfaces so AutoDepsApply / AutoDepsApplyGlob have targets.

public interface IMediator
{
    void Send();
}

public interface IMapper
{
    void Map();
}

public interface IMetrics
{
    void Record();
}

[Scoped]
public partial class MediatorImpl : IMediator
{
    public void Send()
    {
    }
}

[Scoped]
public partial class MapperImpl : IMapper
{
    public void Map()
    {
    }
}

[Scoped]
public partial class MetricsImpl : IMetrics
{
    public void Record()
    {
    }
}

public abstract partial class ControllerBase
{
}

[Scoped]
public partial class OrderController : ControllerBase
{
}

[Scoped]
public partial class UserController : ControllerBase
{
}

