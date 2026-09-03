using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class IdentifierHasherSettingsValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY")]
    [InlineData("DevelopmentIdentifierHasherKeyMustBeAtLeast32CharactersLong")]
    [InlineData("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY_please")]
    public void Validate_WhenKeyIsInvalid_InProduction_Fails(string? key)
    {
        // Arrange
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Production);
        var validator = new IdentifierHasherSettingsValidator(env);
        var options = new IdentifierHasherSettings
        {
            SecretKey = key!
        };

        // Act
        var result = validator.Validate(name: null, options: options);

        // Assert
        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY")]
    [InlineData("DevelopmentIdentifierHasherKeyMustBeAtLeast32CharactersLong")]
    [InlineData("OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY_please")]
    public void Validate_WhenKeyIsInvalid_InNonProduction_Skips(string? key)
    {
        // Arrange
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        var validator = new IdentifierHasherSettingsValidator(env);
        var options = new IdentifierHasherSettings
        {
            SecretKey = key!
        };

        // Act
        var result = validator.Validate(name: null, options: options);

        // Assert
        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_WhenKeyIsValid_Succeeds()
    {
        // Arrange
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Production);   // "Production"
        var validator = new IdentifierHasherSettingsValidator(env);
        var options = new IdentifierHasherSettings
        {
            SecretKey = "SecureProductionKeyMustBeAtLeast32Characters"
        };

        // Act
        var result = validator.Validate(name: null, options: options);

        // Assert
        Assert.True(result.Succeeded);
    }
}
