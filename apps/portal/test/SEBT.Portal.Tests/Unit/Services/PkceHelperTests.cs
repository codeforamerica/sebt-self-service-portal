using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// verifies server-side PKCE generation produces correctly formatted,
/// cryptographically unique values that satisfy the RFC 7636 spec.
/// </summary>
public class PkceHelperTests
{
    [Fact]
    public void GenerateCodeVerifier_IsBase64UrlAndCorrectLength()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();

        Assert.Matches(@"^[A-Za-z0-9_-]+$", verifier);
        Assert.DoesNotContain("+", verifier);
        Assert.DoesNotContain("/", verifier);
        Assert.DoesNotContain("=", verifier);
        Assert.InRange(verifier.Length, 40, 44);
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesUniqueValues()
    {
        var a = PkceHelper.GenerateCodeVerifier();
        var b = PkceHelper.GenerateCodeVerifier();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeCodeChallenge_IsBase64UrlS256()
    {
        var verifier = PkceHelper.GenerateCodeVerifier();
        var challenge = PkceHelper.ComputeCodeChallenge(verifier);

        Assert.Matches(@"^[A-Za-z0-9_-]+$", challenge);
        Assert.DoesNotContain("+", challenge);
        Assert.DoesNotContain("/", challenge);
        Assert.DoesNotContain("=", challenge);
    }

    [Fact]
    public void ComputeCodeChallenge_MatchesRfc7636TestVector()
    {
        // RFC 7636 Appendix B test vector
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expected = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        var challenge = PkceHelper.ComputeCodeChallenge(verifier);

        Assert.Equal(expected, challenge);
    }

    [Fact]
    public void GenerateState_IsBase64Url()
    {
        var state = PkceHelper.GenerateState();

        Assert.Matches(@"^[A-Za-z0-9_-]+$", state);
        Assert.True(state.Length >= 30, "State should be at least 30 chars (24 bytes base64url)");
    }

    [Fact]
    public void GenerateState_ProducesUniqueValues()
    {
        var a = PkceHelper.GenerateState();
        var b = PkceHelper.GenerateState();

        Assert.NotEqual(a, b);
    }
}
