using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific IStateHealthCheckService plugin is loaded.
/// Registers no health checks since there is no backend to verify.
/// </summary>
internal class DefaultStateHealthCheckService : IStateHealthCheckService
{
    public void ConfigureHealthChecks(IHealthChecksBuilder builder)
    {
        // No state plugin loaded — no state-specific health checks to register.
    }
}
