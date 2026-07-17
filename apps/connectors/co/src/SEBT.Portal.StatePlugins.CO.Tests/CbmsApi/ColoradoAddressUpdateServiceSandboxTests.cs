using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Opt-in live verification against CBMS UAT: OAuth, get-account-details (via household cache), then PATCH update-std-dtls.
/// Run locally with user secrets (same as <see cref="CbmsSandboxFixture"/>), for example:
/// <c>dotnet test --filter "FullyQualifiedName~ColoradoAddressUpdateServiceSandboxTests"</c>.
/// </summary>
[Collection("CbmsSandbox")]
public class ColoradoAddressUpdateServiceSandboxTests(CbmsSandboxFixture fixture) : IDisposable
{
    public void Dispose() => PluginCache.ResetForTesting();

    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    /// <summary>
    /// Uses the same sample phone as <see cref="CbmsSandboxTests.GetAccountDetails_ReturnsResponse"/>.
    /// Skips when that phone returns no enrollments; fails on auth/config; asserts success when CBMS accepts the update.
    /// </summary>
    [SkippableFact]
    public async Task UpdateAddressAsync_hits_live_UAT_pipeline()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.ColoradoCbmsConfiguration is null, SkipReason);

        // Reset the cache so PluginCache.GetOrBuild builds from the sandbox configuration.
        PluginCache.ResetForTesting();

        var hostProvider = BuildHostProvider();

        var service = new ColoradoAddressUpdateService(hostProvider, fixture.ColoradoCbmsConfiguration);
        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "8185558437",
            Address = new Address
            {
                StreetAddress1 = "200 E Colfax Ave",
                StreetAddress2 = "Floor 1",
                City = "Denver",
                State = "CO",
                PostalCode = "80203",
            }
        });

        if (!result.IsSuccess && result.IsPolicyRejection && result.ErrorCode == "HOUSEHOLD_NOT_FOUND")
        {
            Skip.If(
                true,
                "No sandbox enrollment for phone 8185558437 — use a HouseholdIdentifierValue that get-account-details resolves.");
        }

        Assert.True(result.IsSuccess, $"{result.ErrorCode}");
    }

    /// <summary>
    /// Builds a minimal host provider with the services required by <see cref="PluginCache.GetOrBuild"/>.
    /// </summary>
    private static IServiceProvider BuildHostProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<IHostApplicationLifetime>(_ => Substitute.For<IHostApplicationLifetime>());
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "h:" + c.Arg<string>());
        services.AddSingleton(hasher);
        return services.BuildServiceProvider();
    }
}
