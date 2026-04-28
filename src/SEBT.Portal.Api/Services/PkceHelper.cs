using System.Security.Cryptography;
using System.Text;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// server-side PKCE generation. Produces a cryptographically random
/// <c>code_verifier</c> and its S256 <c>code_challenge</c>, plus a random <c>state</c>
/// parameter. These are stored in the pre-auth session; only <c>code_challenge</c>
/// and <c>state</c> are sent to the browser.
/// </summary>
public static class PkceHelper
{
    /// <summary>Generates a 32-byte random base64url code_verifier (RFC 7636).</summary>
    public static string GenerateCodeVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>Computes the S256 code_challenge for the given code_verifier.</summary>
    public static string ComputeCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    /// <summary>Generates a 24-byte random base64url state parameter.</summary>
    public static string GenerateState()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
