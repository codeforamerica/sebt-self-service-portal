using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Integration test that verifies <see cref="CbmsApiHealthCheck"/> against the CBMS sandbox.
/// Skips gracefully when no credentials are configured.
/// </summary>
[Collection("CbmsSandbox")]
public class CbmsHealthCheckSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    [SkippableFact]
    public async Task CbmsApiHealthCheck_ReturnsHealthy_WhenSandboxIsReachable()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, "Skipped when using mock responses; health check requires the real sandbox to be useful.");

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CbmsSandboxFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var check = new CbmsApiHealthCheck(
            configuration["Cbms:ClientId"]!,
            configuration["Cbms:ClientSecret"]!,
            CbmsDefaults.SandboxApiBaseUrl,
            CbmsDefaults.SandboxTokenEndpointUrl);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
