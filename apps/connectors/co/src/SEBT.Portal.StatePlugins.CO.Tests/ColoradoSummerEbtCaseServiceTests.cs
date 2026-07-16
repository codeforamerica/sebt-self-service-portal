using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests;

[Collection("PluginCache")]
public class ColoradoSummerEbtCaseServiceTests : IDisposable
{
    public ColoradoSummerEbtCaseServiceTests() => PluginCache.ResetForTesting();
    public void Dispose() => PluginCache.ResetForTesting();

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Installs a no-op cache substitute so that tests which don't need a real
    /// CBMS backend can construct the service without triggering PluginCache.GetOrBuild's
    /// client-credential validation.
    /// </summary>
    private static void UseNoOpCache()
    {
        var noOp = Substitute.For<ICbmsHouseholdCache>();
        PluginCache.OverrideForTesting(noOp);
    }

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

    private static IConfiguration CreateEmptyConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cbms:UseMockResponses"] = "false" })
            .Build();
    }

    private static IConfiguration CreateCbmsConfiguration(
        string clientId = "test-id",
        string clientSecret = "test-secret",
        bool useMockResponses = false)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = clientId,
                ["Cbms:ClientSecret"] = clientSecret,
                ["Cbms:UseMockResponses"] = useMockResponses ? "true" : "false"
            })
            .Build();
    }

    private static IConfiguration CreateMockCbmsConfigurationWithCache()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "test-id",
                ["Cbms:ClientSecret"] = "test-secret",
                ["Cbms:UseMockResponses"] = "true",
                ["Cbms:Cache:SoftExpirationMinutes"] = "5",
                ["Cbms:Cache:HardExpirationMinutes"] = "60",
            })
            .Build();
    }

    private static HybridCache CreateInMemoryHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HybridCache>();
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_returns_null_CBMS_has_no_email_lookup()
    {
        UseNoOpCache();
        var service = new ColoradoSummerEbtCaseService(BuildHostProvider(), CreateEmptyConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByGuardianEmailAsync(
            "test@example.com", piiVisibility, IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_returns_null_for_unsupported_identifier_type()
    {
        UseNoOpCache();
        var service = new ColoradoSummerEbtCaseService(BuildHostProvider(), CreateEmptyConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.SnapId,
            "snap-123",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_when_Cbms_not_configured()
    {
        UseNoOpCache();
        var service = new ColoradoSummerEbtCaseService(BuildHostProvider(), CreateEmptyConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "8185551234",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_household_when_UseMockResponses_and_valid_phone()
    {
        var config = CreateMockCbmsConfigurationWithCache();
        var hostProvider = BuildHostProvider();
        var cache = CreateInMemoryHybridCache();
        var service = new ColoradoSummerEbtCaseService(hostProvider, config, NullLoggerFactory.Instance, cache);
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "7198004382",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.SummerEbtCases);
        Assert.DoesNotContain(
            result.Applications.SelectMany(a => a.Children),
            c => c.FirstName == "Yoko" && c.LastName == "Alden");

        var lucienne = Assert.Single(
            result.Applications.SelectMany(a => a.Children),
            c => c.FirstName == "Lucienne" && c.LastName == "Hayman");
        Assert.Equal(ApplicationStatus.Denied, lucienne.Status);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_for_unknown_phone_in_mock_mode()
    {
        var config = CreateMockCbmsConfigurationWithCache();
        var hostProvider = BuildHostProvider();
        var cache = CreateInMemoryHybridCache();
        var service = new ColoradoSummerEbtCaseService(hostProvider, config, NullLoggerFactory.Instance, cache);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "9999999999",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("12345")]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_when_invalid_phone(string? phone)
    {
        UseNoOpCache();
        var service = new ColoradoSummerEbtCaseService(BuildHostProvider(), CreateCbmsConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            phone ?? string.Empty,
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_routes_through_household_cache()
    {
        // Arrange: inject a fake cache via PluginCache.OverrideForTesting
        var fakeCache = Substitute.For<ICbmsHouseholdCache>();
        fakeCache.GetAsync("3035550199", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GetAccountDetailsResponse
            {
                StdntEnrollDtls = new List<GetAccountStudentDetail>
                {
                    new GetAccountStudentDetail()
                }
            });
        PluginCache.OverrideForTesting(fakeCache);

        var config = CreateCbmsConfiguration(clientId: "test-id", clientSecret: "test-secret");
        var service = new ColoradoSummerEbtCaseService(BuildHostProvider(), config, NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        // Act
        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "303-555-0199",
            piiVisibility,
            IdentityAssuranceLevel.IAL2,
            portalUserId: null,
            cancellationToken: CancellationToken.None);

        // Assert: the fake cache was called with the normalized phone
        await fakeCache.Received(1).GetAsync("3035550199", Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
