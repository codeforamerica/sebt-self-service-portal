using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// HTTP integration tests for GET /api/household/data. Locks the ProblemDetails
/// contract the frontend relies on for IAL step-up redirects (<c>requiredIal</c>
/// at the JSON root, <c>application/problem+json</c> content type).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class HouseholdDataEndpointIntegrationTests : IClassFixture<PortalWebApplicationFactory>
{
    private const string JwtIssuer = "SEBT.Portal.Api";
    private const string JwtAudience = "SEBT.Portal.Web";

    private readonly PortalWebApplicationFactory _factory;

    public HouseholdDataEndpointIntegrationTests(PortalWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHouseholdData_WhenUserIalBelowRequired_ReturnsProblemDetailsWithRequiredIal()
    {
        var email = "household-ial-test@example.com";
        var userId = Guid.NewGuid();
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    ChildFirstName = "Test",
                    ChildLastName = "Child",
                    SummerEBTCaseID = "CASE-1",
                    // Streamline cases require IAL1plus under the integration factory's
                    // per-case-type household+view config (application-only cases allow IAL1).
                    IsStreamlineCertified = true,
                    IsCoLoaded = false
                }
            ]
        };

        var resolver = Substitute.For<IHouseholdIdentifierResolver>();
        resolver
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);

        var repository = Substitute.For<IHouseholdRepository>();
        repository
            .GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(),
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(householdData);

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ReplaceWithMock(services, resolver);
                ReplaceWithMock(services, repository);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/household/data");
        request.Headers.Add(
            "Cookie",
            $"{AuthCookies.AuthCookieName}={CreateSessionJwt(email, userId, ial: "1")}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("problem+json", response.Content.Headers.ContentType!.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("IAL1plus", root.GetProperty("requiredIal").GetString());
        Assert.Equal(403, root.GetProperty("status").GetInt32());
        Assert.Equal(
            "Insufficient identity assurance level",
            root.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-15, false)]
    public async Task GetHouseholdData_ReportsCardReplacementCooldown_FromPortalDbTimestamp(
        int requestedDaysAgo,
        bool expectedInCooldown)
    {
        var email = "household-cooldown-test@example.com";
        var userId = Guid.NewGuid();
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    ChildFirstName = "Test",
                    ChildLastName = "Child",
                    SummerEBTCaseID = "CASE-1",
                    IsStreamlineCertified = false,
                    IsCoLoaded = false
                }
            ]
        };

        var resolver = Substitute.For<IHouseholdIdentifierResolver>();
        resolver
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);

        var repository = Substitute.For<IHouseholdRepository>();
        repository
            .GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(),
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(householdData);

        // The factory has no database. Stand in for the hasher and the portal-DB timestamp so
        // the real evaluator decides the flag from the base appsettings.json cooldown. Analytics
        // digests must look like real SHA-256 hex because the resolver slices 16-character prefixes.
        var hasher = Substitute.For<IIdentifierHasher>();
        hasher.Hash(Arg.Any<string?>()).Returns(callInfo =>
            callInfo.Arg<string?>() is { } value ? $"HASH_{value}" : null);
        hasher.HashForAnalytics(Arg.Any<string?>()).Returns(callInfo =>
            callInfo.Arg<string?>() is { } value
                ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
                : null);
        var cardReplacementRepo = Substitute.For<ICardReplacementRequestRepository>();
        cardReplacementRepo
            .GetMostRecentRequestDateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.AddDays(requestedDaysAgo));

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ReplaceWithMock(services, resolver);
                ReplaceWithMock(services, repository);
                ReplaceWithMock(services, hasher);
                ReplaceWithMock(services, cardReplacementRepo);
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/household/data");
        request.Headers.Add(
            "Cookie",
            $"{AuthCookies.AuthCookieName}={CreateSessionJwt(email, userId, ial: "1plus")}");

        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {json}");

        using var document = JsonDocument.Parse(json);
        var summerEbtCase = document.RootElement.GetProperty("summerEbtCases")[0];

        Assert.Equal(expectedInCooldown, summerEbtCase.GetProperty("inCardReplacementCooldown").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, summerEbtCase.GetProperty("cardRequestedAt").ValueKind);
    }

    private static void ReplaceWithMock<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(implementation);
    }

    private static string CreateSessionJwt(string email, Guid userId, string ial)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(PortalWebApplicationFactory.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var claims = new[]
        {
            new Claim("email", email),
            new Claim(ClaimTypes.Email, email),
            new Claim("sub", userId.ToString()),
            new Claim(JwtClaimTypes.Ial, ial),
            new Claim("auth_time", nowUnixSeconds)
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
