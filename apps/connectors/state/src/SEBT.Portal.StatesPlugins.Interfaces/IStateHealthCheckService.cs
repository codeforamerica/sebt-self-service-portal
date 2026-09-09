using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.StatesPlugins.Interfaces;

/// <summary>
/// Plugin interface for state-specific health checks.
/// Implementations call <see cref="IHealthChecksBuilder.AddCheck"/> to register
/// health checks with ASP.NET Core's health check middleware.
/// </summary>
public interface IStateHealthCheckService : IStatePlugin
{
    /// <summary>
    /// Configures health checks for the state's external dependencies.
    /// </summary>
    void ConfigureHealthChecks(IHealthChecksBuilder builder);
}
