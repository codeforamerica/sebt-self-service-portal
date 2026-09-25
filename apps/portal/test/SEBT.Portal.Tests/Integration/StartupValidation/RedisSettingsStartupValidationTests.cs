using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves a production host refuses to start when Redis would skip TLS certificate validation.
/// AcceptSelfSignedCertificates exists for a local container; deployed Elasticache presents an
/// AWS-signed certificate, so the flag there only removes protection against an impostor endpoint.
/// <para>
/// The remaining Redis rules are covered by RedisSettingsValidatorTests. That valid and absent Redis
/// sections still boot is covered by every other integration test in this project.
/// </para>
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class RedisSettingsStartupValidationTests() : StartupValidationTestBase(Environments.Production)
{
    [Fact]
    public void Startup_WithSelfSignedCertificatesInProduction_ThrowsOptionsValidationException()
    {
        SetEnv("Redis__Host", "cluster.cache.amazonaws.com");
        SetEnv("Redis__Ssl", "true");
        SetEnv("Redis__AcceptSelfSignedCertificates", "true");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Redis:AcceptSelfSignedCertificates", ex.Message);
    }
}
