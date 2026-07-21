using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Tests.Helpers;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Guards the Program.cs Serilog path: LOG_FORMAT=json must reach the host Console
/// sink, and authenticated requests must emit portal_user_id on that JSON line.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class SerilogJsonHostIntegrationTests : IDisposable
{
    private const string JwtIssuer = "SEBT.Portal.Api";
    private const string JwtAudience = "SEBT.Portal.Web";

    private readonly string? _previousLogFormat;

    public SerilogJsonHostIntegrationTests()
    {
        // Must be set before WebApplicationFactory builds the host — Program.cs reads
        // LOG_FORMAT once at startup when wiring SerilogSetup.Configure.
        _previousLogFormat = Environment.GetEnvironmentVariable("LOG_FORMAT");
        Environment.SetEnvironmentVariable("LOG_FORMAT", "json");
    }

    [Fact]
    public async Task AuthenticatedStatus_WithLogFormatJson_WritesPortalUserIdToConsole()
    {
        var userId = Guid.NewGuid();

        // Dedicated factory so LOG_FORMAT is applied at Program.cs startup (the shared
        // IClassFixture host may already have been built without it).
        var output = await ConsoleOutputCapture.CaptureAsync(async () =>
        {
            using var factory = new PortalWebApplicationFactory();
            var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
            request.Headers.Add(
                "Cookie",
                $"{AuthCookies.AuthCookieName}={CreateSessionJwt(userId)}");

            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        var statusLine = output.Split('\n')
            .FirstOrDefault(line => line.Contains("Authorization status check successful", StringComparison.Ordinal));
        Assert.NotNull(statusLine);
        Assert.Contains("\"status\"", statusLine, StringComparison.Ordinal);
        Assert.Contains($"\"portal_user_id\":\"{userId}\"", statusLine, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOG_FORMAT", _previousLogFormat);
    }

    private static string CreateSessionJwt(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PortalWebApplicationFactory.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var claims = new[]
        {
            new Claim("email", "serilog-json-host@example.com"),
            new Claim("sub", userId.ToString()),
            new Claim("auth_time", nowUnixSeconds),
        };
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: now.AddMinutes(-1),
            expires: now.AddMinutes(60),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
