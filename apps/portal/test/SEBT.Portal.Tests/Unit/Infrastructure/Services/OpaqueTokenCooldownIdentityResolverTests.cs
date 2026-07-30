using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class OpaqueTokenCooldownIdentityResolverTests
{
    private readonly OpaqueTokenCooldownIdentityResolver _resolver = new();

    [Fact]
    public void ResolveCanonicalCaseIdentity_OpaqueToken_ReturnsRawCaseIdRoutingField()
    {
        var token = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "STATE-CASE-123",
            ["applicationId"] = "APP-9",
            ["householdIdentifier"] = "user@example.com",
        });

        Assert.Equal("STATE-CASE-123", _resolver.ResolveCanonicalCaseIdentity(token));
    }

    [Theory]
    [InlineData("STATE-CASE-123")]
    [InlineData("123456")]
    [InlineData("SEBT-001")]
    public void ResolveCanonicalCaseIdentity_RawCaseId_PassesThroughUnchanged(string rawCaseId)
    {
        Assert.Equal(rawCaseId, _resolver.ResolveCanonicalCaseIdentity(rawCaseId));
    }

    [Fact]
    public void ResolveCanonicalCaseIdentity_TokenWithoutCaseIdField_FailsLoud()
    {
        var token = OpaqueCaseId.Compose(new Dictionary<string, string> { ["writeId"] = "CWIN-1" });

        Assert.Throws<InvalidOperationException>(() => _resolver.ResolveCanonicalCaseIdentity(token));
    }

    [Fact]
    public void ResolveCanonicalCaseIdentity_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => _resolver.ResolveCanonicalCaseIdentity(" "));
    }
}
