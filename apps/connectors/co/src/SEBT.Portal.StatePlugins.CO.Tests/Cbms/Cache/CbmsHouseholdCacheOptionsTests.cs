using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheOptionsTests
{
    [Fact]
    public void Defaults_match_design_spec()
    {
        var sut = new CbmsHouseholdCacheOptions();

        Assert.Equal(15, sut.SoftExpirationMinutes);
        Assert.Equal(240, sut.HardExpirationMinutes);
        Assert.Equal(60, sut.NegativeCacheSeconds);
        Assert.Equal(60, sut.BackgroundRefreshTimeoutSeconds);
        Assert.Equal(1, sut.LocalCacheExpirationSeconds);
    }

    [Fact]
    public void TimeSpan_helpers_compute_correctly()
    {
        var sut = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = 20,
            HardExpirationMinutes = 300,
            NegativeCacheSeconds = 30,
            BackgroundRefreshTimeoutSeconds = 45,
            LocalCacheExpirationSeconds = 90
        };

        Assert.Equal(TimeSpan.FromMinutes(20), sut.SoftExpiration);
        Assert.Equal(TimeSpan.FromMinutes(300), sut.HardExpiration);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.NegativeCacheExpiration);
        Assert.Equal(TimeSpan.FromSeconds(45), sut.BackgroundRefreshTimeout);
        Assert.Equal(TimeSpan.FromSeconds(90), sut.LocalCacheExpiration);
    }

    [Fact]
    public void Validate_returns_no_errors_for_defaults()
    {
        var sut = new CbmsHouseholdCacheOptions();
        Assert.Empty(sut.Validate());
    }

    [Theory]
    [InlineData(0, 240, 60, 60, 60, "SoftExpirationMinutes must be > 0")]
    [InlineData(15, 0, 60, 60, 60, "HardExpirationMinutes must be > 0")]
    [InlineData(240, 240, 60, 60, 60, "SoftExpirationMinutes must be < HardExpirationMinutes")]
    [InlineData(15, 240, -1, 60, 60, "NegativeCacheSeconds must be >= 0")]
    [InlineData(15, 240, 60, 0, 60, "BackgroundRefreshTimeoutSeconds must be > 0")]
    [InlineData(15, 240, 60, 60, 0, "LocalCacheExpirationSeconds must be > 0")]
    public void Validate_returns_error_when_invalid(int soft, int hard, int neg, int bg, int local, string expected)
    {
        var sut = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = soft,
            HardExpirationMinutes = hard,
            NegativeCacheSeconds = neg,
            BackgroundRefreshTimeoutSeconds = bg,
            LocalCacheExpirationSeconds = local
        };

        Assert.Contains(expected, sut.Validate());
    }

    [Fact]
    public void Bind_from_configuration_section()
    {
        var configBuilder = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cbms:Cache:SoftExpirationMinutes"] = "5",
            ["Cbms:Cache:HardExpirationMinutes"] = "120",
            ["Cbms:Cache:NegativeCacheSeconds"] = "30",
            ["Cbms:Cache:BackgroundRefreshTimeoutSeconds"] = "45",
            ["Cbms:Cache:LocalCacheExpirationSeconds"] = "90",
        });
        var config = configBuilder.Build();

        var sut = new CbmsHouseholdCacheOptions();
        config.GetSection("Cbms:Cache").Bind(sut);

        Assert.Equal(5, sut.SoftExpirationMinutes);
        Assert.Equal(120, sut.HardExpirationMinutes);
        Assert.Equal(30, sut.NegativeCacheSeconds);
        Assert.Equal(45, sut.BackgroundRefreshTimeoutSeconds);
        Assert.Equal(90, sut.LocalCacheExpirationSeconds);
    }
}
