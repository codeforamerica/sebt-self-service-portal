using SEBT.Portal.Api.Startup;
using SEBT.Portal.Core.AppSettings;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Startup;

public class PiiEncryptionGuardTests
{
    [Fact]
    public void ValidateForProduction_WhenNull_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(null));
        Assert.Contains("PiiEncryption", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenEmptyActiveKeyId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    ActiveKeyId = "   ",
                    Keys = [new PiiEncryptionKeySetting { KeyId = "k1", KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI=" }]
                }));
        Assert.Contains("ActiveKeyId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenNoKeys_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings { ActiveKeyId = "prod-key", Keys = [] }));
        Assert.Contains("Keys", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenDevelopmentActiveKeyId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    ActiveKeyId = PiiEncryptionGuard.ForbiddenDevelopmentActiveKeyId,
                    Keys =
                    [
                        new PiiEncryptionKeySetting
                        {
                            KeyId = "k1",
                            KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="
                        }
                    ]
                }));
        Assert.Contains("local-dev", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForProduction_WhenPlaceholderKeyMaterial_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    ActiveKeyId = "prod-key",
                    Keys =
                    [
                        new PiiEncryptionKeySetting
                        {
                            KeyId = "k1",
                            KeyMaterialBase64 = PiiEncryptionGuard.ForbiddenPlaceholderKeyMaterialBase64
                        }
                    ]
                }));
        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForProduction_WhenValid_DoesNotThrow()
    {
        PiiEncryptionGuard.ValidateForProduction(
            new PiiEncryptionSettings
            {
                ActiveKeyId = "prod-key",
                Keys =
                [
                    new PiiEncryptionKeySetting
                    {
                        KeyId = "prod-key",
                        KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="
                    }
                ]
            });
    }
}
