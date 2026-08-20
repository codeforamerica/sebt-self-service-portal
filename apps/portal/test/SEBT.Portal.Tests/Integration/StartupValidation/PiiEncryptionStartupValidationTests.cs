using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app fails to start in Production when PII encryption is on and a key uses a repo
/// placeholder. The prod-only PiiEncryptionProductionSecretsValidator (which replaced PiiEncryptionGuard)
/// runs via ValidateOnStart and rejects the sample key material. Outside Production it skips, so this is
/// Production-only behavior — the skip and forbidden-ActiveKeyId paths are covered by unit tests.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class PiiEncryptionStartupValidationTests()
    : StartupValidationTestBase(Environments.Production)
{
    [Fact]
    public void Startup_InProduction_WithPlaceholderKeyMaterial_ThrowsOptionsValidationException()
    {
        // The repo's sample key material decodes to 32 valid bytes, so it passes the structural
        // validator (base64 + length + ActiveKeyId resolves). Only the prod-only secrets validator
        // rejects it — proving that validator is actually wired into ValidateOnStart.
        SetEnv("PiiEncryption__EncryptAtRest", "true");
        SetEnv("PiiEncryption__ActiveKeyId", "prod-key-1");
        SetEnv("PiiEncryption__Keys__0__KeyId", "prod-key-1");
        SetEnv("PiiEncryption__Keys__0__KeyMaterialBase64", "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=");

        using var factory = CreateFactory();

        // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure.
        var ex = Assert.Throws<OptionsValidationException>(factory.CreateClient);
        Assert.Contains("PiiEncryption", ex.Message);
    }
}
