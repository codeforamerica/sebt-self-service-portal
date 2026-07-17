using SEBT.Portal.StatePlugins.CO.Cbms;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class EligibilitySourceClassifierTests
{
    [Theory]
    [InlineData("CBMS", true)]
    [InlineData("PK", true)]
    [InlineData("cbms", true)]
    [InlineData("pk", true)]
    public void IsApplicationBased_returns_true_for_application_sources(string eligSrc, bool expected)
    {
        Assert.Equal(expected, EligibilitySourceClassifier.IsApplicationBased(eligSrc));
    }

    [Theory]
    [InlineData("DIRC", true)]
    [InlineData("CDE", true)]
    [InlineData("dirc", true)]
    [InlineData("cde", true)]
    public void IsStreamlinedCertification_returns_true_for_auto_eligible_sources(string eligSrc, bool expected)
    {
        Assert.Equal(expected, EligibilitySourceClassifier.IsStreamlinedCertification(eligSrc));
    }

    [Theory]
    [InlineData("DIRC")]
    [InlineData("CDE")]
    public void IsApplicationBased_returns_false_for_auto_eligible_sources(string eligSrc)
    {
        Assert.False(EligibilitySourceClassifier.IsApplicationBased(eligSrc));
    }

    [Theory]
    [InlineData("CBMS")]
    [InlineData("PK")]
    public void IsStreamlinedCertification_returns_false_for_application_sources(string eligSrc)
    {
        Assert.False(EligibilitySourceClassifier.IsStreamlinedCertification(eligSrc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    public void Both_methods_return_false_for_unknown_or_empty_values(string? eligSrc)
    {
        Assert.False(EligibilitySourceClassifier.IsApplicationBased(eligSrc));
        Assert.False(EligibilitySourceClassifier.IsStreamlinedCertification(eligSrc));
    }
}
