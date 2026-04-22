using IoCTools.Abstractions.Annotations;
using ProfilesProject.Services;

// Contribute deps into profiles.
[assembly: AutoDepIn<ControllerDefaults, IMediator>]
[assembly: AutoDepIn<ControllerDefaults, IMapper>]
[assembly: AutoDepIn<WorkerDefaults, IMetrics>]

// Attach profiles to services.
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>]
[assembly: AutoDepsApplyGlob<WorkerDefaults>("ProfilesProject.Services.Background*")]
