using SEBT.Portal.Api.Startup.Guards;
using SEBT.Portal.Core.AppSettings;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Startup.Guards;

public class PiiEncryptionGuardTests
{
    [Fact]
    public void ValidateForProduction_WhenNull_DoesNotThrow()
    {
        PiiEncryptionGuard.ValidateForProduction(null);
    }

    [Fact]
    public void ValidateForProduction_WhenEncryptAtRestDisabled_DoesNotThrow()
    {
        PiiEncryptionGuard.ValidateForProduction(
            new PiiEncryptionSettings
            {
                EncryptAtRest = false,
                ActiveKeyId = PiiEncryptionGuard.ForbiddenDevelopmentActiveKeyId,
                Keys =
                [
                    new PiiEncryptionKeySetting
                    {
                        KeyId = "k1",
                        KeyMaterialBase64 = PiiEncryptionGuard.ForbiddenPlaceholderKeyMaterialBase64
                    }
                ]
            });
    }

    [Fact]
    public void ValidateForProduction_WhenEmptyActiveKeyId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
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
                new PiiEncryptionSettings { EncryptAtRest = true, ActiveKeyId = "prod-key", Keys = [] }));
        Assert.Contains("Keys", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenDevelopmentActiveKeyId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
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
    public void ValidateForProduction_WhenNullKeyEntry_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
                    ActiveKeyId = "prod-key",
                    Keys = [null!, new PiiEncryptionKeySetting { KeyId = "prod-key", KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI=" }]
                }));
        Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForProduction_WhenEmptyKeyId_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
                    ActiveKeyId = "prod-key",
                    Keys =
                    [
                        new PiiEncryptionKeySetting { KeyId = "   ", KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI=" }
                    ]
                }));
        Assert.Contains("KeyId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenEmptyKeyMaterial_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
                    ActiveKeyId = "prod-key",
                    Keys =
                    [
                        new PiiEncryptionKeySetting { KeyId = "prod-key", KeyMaterialBase64 = "   " }
                    ]
                }));
        Assert.Contains("KeyMaterialBase64", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForProduction_WhenPlaceholderKeyMaterial_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            PiiEncryptionGuard.ValidateForProduction(
                new PiiEncryptionSettings
                {
                    EncryptAtRest = true,
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
                EncryptAtRest = true,
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
