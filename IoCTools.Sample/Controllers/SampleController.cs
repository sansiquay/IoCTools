namespace IoCTools.Sample.Controllers;

using Abstractions.Annotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// Demonstrates that ASP.NET Core controllers are skipped by default for registration
[Scoped]
[DependsOn<ILogger<SampleController>>]public partial class SampleController : ControllerBase
{
}
