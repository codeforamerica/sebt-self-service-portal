using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.Api.Startup;

namespace SEBT.Portal.Tests.Unit.Api.Startup;

public class PortalDbHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsDegraded_WhenConnectionStringIsUnreachable()
    {
        var check = new PortalDbHealthCheck(
            "Server=localhost,19999;Database=NonExistent;TrustServerCertificate=true;Connect Timeout=1");

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }
}
