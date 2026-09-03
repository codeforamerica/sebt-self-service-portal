using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class PiiEncryptionProductionSecretsValidatorTests
{
    private const string ForbiddenMaterial = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=";
    private const string ValidMaterial = "dmFsaWQtMzItYnl0ZS1rZXktbWF0ZXJpYWwtaGVyZS0h";

    /// <summary>
    /// Key/material pairs the production validator must reject: a forbidden ActiveKeyId (whitespace variants
    /// prove trimming) or forbidden key material. All three theories share these rows, so the skip tests also
    /// prove the environment and EncryptAtRest gates short-circuit before the validator inspects any value.
    /// </summary>
    public static TheoryData<string, string> ForbiddenCases() =>
        new()
        {
            { "local-dev-primary", ForbiddenMaterial },
            { "local-dev-primary ", ForbiddenMaterial },
            { " local-dev-primary", ForbiddenMaterial },
            { "local-dev-primary", ValidMaterial },
            { "local-dev-primary ", ValidMaterial },
            { " local-dev-primary", ValidMaterial },
            { "fake-production-key-id", ForbiddenMaterial }
        };

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public void Validate_WhenKeyIdOrKeyMaterialIsForbidden_InProduction_Fails(string keyId, string keyMaterial)
    {
        // Arrange
        var validator = CreateValidator(Environments.Production);
        var options = CreateSettings(keyId, keyMaterial);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public void Validate_WhenEncryptAtRestIsFalse_InProduction_Skips(string keyId, string keyMaterial)
    {
        // Arrange
        var validator = CreateValidator(Environments.Production);
        var options = CreateSettings(keyId, keyMaterial, encryptAtRest: false);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Skipped);
    }

    [Theory]
    [MemberData(nameof(ForbiddenCases))]
    public void Validate_WhenKeyIdOrKeyMaterialIsForbidden_InNonProduction_Skips(string keyId, string keyMaterial)
    {
        // Arrange
        var validator = CreateValidator(Environments.Development);
        var options = CreateSettings(keyId, keyMaterial);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_WhenKeyDataIsValid_InProduction_Succeeds()
    {
        // Arrange
        var validator = CreateValidator(Environments.Production);
        var options = CreateSettings("fake-production-key-id", ValidMaterial);

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    private static PiiEncryptionProductionSecretsValidator CreateValidator(string environmentName)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return new PiiEncryptionProductionSecretsValidator(env);
    }

    private static PiiEncryptionSettings CreateSettings(
        string activeKeyId, string keyMaterialBase64, bool encryptAtRest = true) =>
        new()
        {
            EncryptAtRest = encryptAtRest,
            ActiveKeyId = activeKeyId,
            Keys = [new PiiEncryptionKeySetting { KeyId = activeKeyId, KeyMaterialBase64 = keyMaterialBase64 }]
        };
}
