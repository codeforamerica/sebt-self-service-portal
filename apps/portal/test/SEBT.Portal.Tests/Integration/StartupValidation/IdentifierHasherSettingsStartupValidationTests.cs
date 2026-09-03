using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app fails to start in Production when IdentifierHasher:SecretKey is a forbidden
/// placeholder. The prod-only IdentifierHasherSettingsValidator (which replaced IdentifierHasherGuard)
/// runs via ValidateOnStart and rejects the repo's dev placeholders. Outside Production the validator
/// skips, so this is Production-only behavior — the skip path is covered by the unit tests.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class IdentifierHasherSettingsStartupValidationTests() : StartupValidationTestBase(Environments.Production)
{
    [Fact]
    public void Startup_InProduction_WithForbiddenPlaceholderKey_ThrowsOptionsValidationException()
    {
        // A >=32-char placeholder: passes the [MinLength(32)] DataAnnotation, so ONLY the prod-only
        // IdentifierHasher validator can reject it — this proves the validator is actually wired.
        SetEnv("IdentifierHasher__SecretKey", "OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY");
        using var factory = CreateFactory();

        // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure.
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("IdentifierHasher", ex.Message);
    }
}
