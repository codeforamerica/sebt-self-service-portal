using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class IdProofingRequirementsCoherenceValidatorTests
{
    private static IdProofingRequirementsCoherenceValidator CreateValidator(
        bool stepUpConfigured = false)
    {
        var configValues = new Dictionary<string, string?>();
        if (stepUpConfigured)
        {
            configValues["Oidc:StepUp:DiscoveryEndpoint"] = "https://auth.example.com/.well-known/openid-configuration";
            configValues["Oidc:StepUp:ClientId"] = "test-client";
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new IdProofingRequirementsCoherenceValidator(config);
    }

    private static IdProofingRequirementsSettings MakeSettings(
        Dictionary<string, IalRequirement> requirements)
    {
        var settings = new IdProofingRequirementsSettings();
        foreach (var kvp in requirements)
            settings.Requirements[kvp.Key] = kvp.Value;
        return settings;
    }

    [Fact]
    public void Validate_CoherentConfig_Succeeds()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["email+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["household+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WriteBelowView_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
        Assert.Contains("address", result.FailureMessage);
    }

    [Fact]
    public void Validate_PerCaseTypeWriteBelowView_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1,
                ["streamline"] = IalLevel.IAL1plus,
            }),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_StepUpConfigured_AllWriteIal1_Fails()
    {
        var validator = CreateValidator(stepUpConfigured: true);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
        Assert.Contains("step-up", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StepUpConfigured_OneWriteAboveIal1_Succeeds()
    {
        var validator = CreateValidator(stepUpConfigured: true);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    // --- Cross-product: per-case-type view × uniform write ---

    [Fact]
    public void Validate_PerCaseTypeViewUniformWrite_WriteBelowAnyViewLevel_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1,
                ["streamline"] = IalLevel.IAL1plus,
            }),
            // Uniform IAL1 write is below the IAL1plus view level for NonCoLoaded
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_PerCaseTypeViewUniformWrite_WriteAboveAllViewLevels_Succeeds()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1,
                ["streamline"] = IalLevel.IAL1plus,
            }),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    // --- Cross-product: per-case-type view × per-case-type write ---

    [Fact]
    public void Validate_BothPerCaseType_AnyWriteLevelBelowAnyViewLevel_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1plus,
            }),
            ["address+write"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1,
            }),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
    }

    // --- Partial resource keys (only view or only write) ---

    [Fact]
    public void Validate_WriteOnlyResource_NoViewKey_Succeeds()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            // card has +write but no +view — should not fail
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ViewOnlyResource_NoWriteKey_Succeeds()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            // email has +view but no +write — should not fail
            ["email+view"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    // --- Step-up consistency ---

    [Fact]
    public void Validate_NoStepUpConfigured_AllWriteIal1_Succeeds()
    {
        var validator = CreateValidator(stepUpConfigured: false);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }
}
