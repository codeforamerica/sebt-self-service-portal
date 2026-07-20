using SEBT.Portal.Api.Startup;

namespace SEBT.Portal.Tests.Unit.Startup;

public class IdentifierHasherGuardTests
{
    [Fact]
    public void ValidateForProduction_WhenKeyIsNull_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IdentifierHasherGuard.ValidateForProduction(null));

        Assert.Contains("IdentifierHasher:SecretKey", ex.Message);
        Assert.Contains("IDENTIFIERHASHER__SECRETKEY", ex.Message);
    }

    [Fact]
    public void ValidateForProduction_WhenKeyIsEmpty_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            IdentifierHasherGuard.ValidateForProduction(string.Empty));
    }

    [Fact]
    public void ValidateForProduction_WhenKeyIsAppsettingsPlaceholder_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            IdentifierHasherGuard.ValidateForProduction("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY"));

        Assert.Contains("IdentifierHasher:SecretKey", ex.Message);
    }

    [Fact]
    public void ValidateForProduction_WhenKeyIsDevelopmentPlaceholder_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            IdentifierHasherGuard.ValidateForProduction("DevelopmentIdentifierHasherKeyMustBeAtLeast32CharactersLong"));
    }

    [Fact]
    public void ValidateForProduction_WhenKeyContainsOverrideInProduction_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            IdentifierHasherGuard.ValidateForProduction("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY_please!"));
    }

    [Fact]
    public void ValidateForProduction_WhenKeyIsSecure_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            IdentifierHasherGuard.ValidateForProduction("SecureProductionKeyMustBeAtLeast32Characters!!"));

        Assert.Null(exception);
    }
}
