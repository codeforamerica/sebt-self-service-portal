using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using RichardSzalay.MockHttp;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for <see cref="OidcController"/> (OIDC endpoints; Oidc config
/// </summary>
public class OidcControllerTests
{
    private const string CoStateKey = "co";
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtService;
    private readonly OidcController _controller;

    public OidcControllerTests()
    {
        _config = Substitute.For<IConfiguration>();
        _httpFactory = Substitute.For<IHttpClientFactory>();
        _userRepository = Substitute.For<IUserRepository>();
        _jwtService = Substitute.For<IJwtTokenService>();

        _controller = new OidcController(
            _config,
            _httpFactory,
            NullLogger<OidcController>.Instance,
            _userRepository,
            _jwtService);
    }

    [Fact]
    public async Task GetConfig_WhenDiscoveryEndpointMissing_Returns503()
    {
        _config["Oidc:DiscoveryEndpoint"].Returns((string?)null);
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey, cancellationToken: CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetConfig_WhenClientIdMissing_Returns503()
    {
        _config["Oidc:DiscoveryEndpoint"].Returns("https://auth.example.com/.well-known/openid-configuration");
        _config["Oidc:ClientId"].Returns((string?)null);
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey, cancellationToken: CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetConfig_WhenConfigSet_Returns200()
    {
        const string discoveryUrl = "https://auth.example.com/.well-known/openid-configuration";
        const string discoveryJson = """{"authorization_endpoint":"https://auth.example.com/authorize","token_endpoint":"https://auth.example.com/token"}""";

        _config["Oidc:DiscoveryEndpoint"].Returns(discoveryUrl);
        _config["Oidc:ClientId"].Returns("client-id");
        _config["Oidc:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, discoveryUrl).Respond("application/json", discoveryJson);
        var client = new HttpClient(mockHttp);
        _httpFactory.CreateClient().Returns(client);

        var result = await _controller.GetConfig(CoStateKey, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value.GetType();
        Assert.Equal("client-id", valueType.GetProperty("clientId")?.GetValue(okResult.Value));
    }

    [Fact]
    public async Task CompleteLogin_WhenBodyNull_Returns400()
    {
        var result = await _controller.CompleteLogin(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CompleteLogin_WhenStateCodeMissing_Returns400()
    {
        var body = new CompleteLoginRequest(StateCode: null, "callback.jwt.here");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenMissing_Returns400()
    {
        var body = new CompleteLoginRequest(CoStateKey, CallbackToken: null);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task CompleteLogin_WhenSigningKeyNotConfigured_Returns503()
    {
        _config["Oidc:CompleteLoginSigningKey"].Returns((string?)null);
        var body = new CompleteLoginRequest(CoStateKey, "some.jwt.token");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    /// <summary>
    /// Callback token must contain an email or sub claim usable as login identifier.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenCallbackTokenHasNoEmailOrSub_Returns400()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateCallbackTokenWithClaims(signingKey, new Claim("given_name", "Pat"));
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Callback token must contain an email or sub claim.", errorResponse.Error);
    }

    /// <summary>
    /// Success path: valid callback token returns 200 with a JSON body containing a "token" property (portal JWT).
    /// Ensures the route returns the response shape the frontend expects.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenValidCallbackToken_Returns200WithToken()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken);

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user, false));

        const string portalJwt = "portal-jwt-returned-by-service";
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(portalJwt);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value.GetType();
        var tokenProp = valueType.GetProperty("token");
        Assert.NotNull(tokenProp);
        var tokenValue = tokenProp.GetValue(okResult.Value) as string;
        Assert.Equal(portalJwt, tokenValue);
    }

    /// <summary>
    /// Step-up must not create a user; IdP email must already match a portal account from primary sign-in.
    /// </summary>
    [Fact]
    public async Task CompleteLogin_WhenStepUpAndNoExistingUser_Returns400()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "new-user@example.com");
        var body = new CompleteLoginRequest(CoStateKey, callbackToken, IsStepUp: true);

        _userRepository.GetUserByEmailAsync("new-user@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        var errorProp = badRequest.Value.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal(
            "Step-up requires an existing session. Please sign in again.",
            errorProp.GetValue(badRequest.Value) as string);

        await _userRepository.DidNotReceive().GetOrCreateUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteLogin_WhenStepUpAndSafeReturnUrl_Returns200WithReturnUrl()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(
            CoStateKey,
            callbackToken,
            IsStepUp: true,
            ReturnUrl: "/profile/address?q=1");

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        const string portalJwt = "portal-jwt-returned-by-service";
        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns(portalJwt);

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        Assert.Equal("/profile/address?q=1", valueType.GetProperty("returnUrl")!.GetValue(okResult.Value) as string);
        Assert.Equal(portalJwt, valueType.GetProperty("token")!.GetValue(okResult.Value) as string);
    }

    [Fact]
    public async Task CompleteLogin_WhenStepUpAndExternalReturnUrl_OmitsReturnUrlFromResponse()
    {
        const string signingKey = "complete-login-signing-key-at-least-32-characters-long";
        _config["Oidc:CompleteLoginSigningKey"].Returns(signingKey);

        var callbackToken = CreateValidCallbackToken(signingKey, email: "user@example.com");
        var body = new CompleteLoginRequest(
            CoStateKey,
            callbackToken,
            IsStepUp: true,
            ReturnUrl: "https://evil.example/phish");

        var user = new User { Id = 1, Email = "user@example.com" };
        _userRepository.GetUserByEmailAsync("user@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _userRepository.UpdateUserAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _jwtService.GenerateToken(Arg.Any<User>(), Arg.Any<IReadOnlyDictionary<string, string>?>())
            .Returns("portal-jwt");

        var result = await _controller.CompleteLogin(body, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value!.GetType().GetProperty("returnUrl"));
    }

    private static string CreateValidCallbackToken(string signingKey, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim> { new("email", email) };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateCallbackTokenWithClaims(string signingKey, params Claim[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
