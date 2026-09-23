using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class RedisSettingsValidatorTests
{
    // The base appsettings.json ships no Redis section, and states without Redis rely on the
    // in-memory fallback. If the defaults ever failed validation, those deployments would stop booting.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Validate_DefaultSettings_Succeeds(string environmentName)
    {
        var result = Validator(environmentName).Validate(null, new RedisSettings());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NullSettings_Fails()
    {
        var result = Validator(Environments.Development).Validate(null, null!);

        Assert.True(result.Failed);
        Assert.Contains("Redis configuration section is not present", result.FailureMessage);
    }

    [Fact]
    public void Validate_ElasticacheShapedSettings_InProduction_Succeeds()
    {
        var settings = new RedisSettings
        {
            Host = "cluster.cache.amazonaws.com",
            Port = 6379,
            Password = "auth-token",
            Ssl = true,
            SslHost = "cluster.cache.amazonaws.com"
        };

        var result = Validator(Environments.Production).Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // The local shape from appsettings.co.example.json: TLS to a container presenting a self-signed cert.
    [Fact]
    public void Validate_SelfSignedCertificates_InDevelopment_Succeeds()
    {
        var settings = new RedisSettings
        {
            Host = "localhost",
            Port = 6380,
            Ssl = true,
            SslHost = "redis",
            AcceptSelfSignedCertificates = true
        };

        var result = Validator(Environments.Development).Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Validate_SelfSignedCertificates_OutsideDevelopment_Fails(string environmentName)
    {
        var settings = new RedisSettings
        {
            Host = "cluster.cache.amazonaws.com",
            Ssl = true,
            AcceptSelfSignedCertificates = true
        };

        var result = Validator(environmentName).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Redis:AcceptSelfSignedCertificates", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_PortOutOfRange_Fails(int port)
    {
        var settings = new RedisSettings { Host = "localhost", Port = port };

        var result = Validator(Environments.Development).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Redis:Port", result.FailureMessage);
    }

    // SslHost only names the certificate to expect during a TLS handshake, so without Ssl it is
    // ignored and the connection is silently unencrypted.
    [Fact]
    public void Validate_SslHostWithoutSsl_Fails()
    {
        var settings = new RedisSettings { Host = "cluster.cache.amazonaws.com", SslHost = "cluster.cache.amazonaws.com" };

        var result = Validator(Environments.Development).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Redis:SslHost", result.FailureMessage);
    }

    [Fact]
    public void Validate_SelfSignedCertificatesWithoutSsl_Fails()
    {
        var settings = new RedisSettings { Host = "localhost", AcceptSelfSignedCertificates = true };

        var result = Validator(Environments.Development).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Redis:Ssl", result.FailureMessage);
    }

    // Blanking the host over a state file that configures TLS Redis turns Redis off; the CO integration
    // e2e stack does exactly this over appsettings.co.example.json. The leftover values are ignored,
    // so they must not fail the boot, even outside Development.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankHostWithLeftoverRedisValues_Succeeds(string host)
    {
        var settings = new RedisSettings
        {
            Host = host,
            Port = 6380,
            Ssl = true,
            SslHost = "redis",
            AcceptSelfSignedCertificates = true
        };

        var result = Validator(Environments.Production).Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // One boot should tell an operator everything to fix.
    [Fact]
    public void Validate_SeveralProblems_ReportsEveryOne()
    {
        var settings = new RedisSettings
        {
            Host = "cluster.cache.amazonaws.com",
            Port = 0,
            SslHost = "cluster.cache.amazonaws.com",
            AcceptSelfSignedCertificates = true
        };

        var result = Validator(Environments.Production).Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Redis:Port", result.FailureMessage);
        Assert.Contains("Redis:SslHost", result.FailureMessage);
        Assert.Contains("Redis:AcceptSelfSignedCertificates", result.FailureMessage);
    }

    private static RedisSettingsValidator Validator(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new RedisSettingsValidator(environment);
    }
}
