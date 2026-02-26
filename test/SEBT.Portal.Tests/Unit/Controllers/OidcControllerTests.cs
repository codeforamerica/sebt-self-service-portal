extern alias statePlugin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Auth;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using IStateAuthStore = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.IStateAuthStore;

namespace SEBT.Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for <see cref="OidcController"/> (OIDC endpoints; config under Oidc:{stateCode}).
/// </summary>
public class OidcControllerTests
{
    private const string CoStateKey = "co";
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IStateAuthStore _store;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtService;
    private readonly OidcController _controller;

    public OidcControllerTests()
    {
        _config = Substitute.For<IConfiguration>();
        _httpFactory = Substitute.For<IHttpClientFactory>();
        _store = Substitute.For<IStateAuthStore>();
        _userRepository = Substitute.For<IUserRepository>();
        _jwtService = Substitute.For<IJwtTokenService>();

        _controller = new OidcController(
            _config,
            _httpFactory,
            NullLogger<OidcController>.Instance,
            _store,
            _userRepository,
            _jwtService);
    }

    [Fact]
    public async Task GetConfig_WhenDiscoveryEndpointMissing_Returns503()
    {
        _config[$"Oidc:{CoStateKey}:DiscoveryEndpoint"].Returns((string?)null);
        _config[$"Oidc:{CoStateKey}:ClientId"].Returns("client-id");
        _config[$"Oidc:{CoStateKey}:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetConfig_WhenClientIdMissing_Returns503()
    {
        _config[$"Oidc:{CoStateKey}:DiscoveryEndpoint"].Returns("https://auth.example.com/.well-known/openid-configuration");
        _config[$"Oidc:{CoStateKey}:ClientId"].Returns((string?)null);
        _config[$"Oidc:{CoStateKey}:CallbackRedirectUri"].Returns("http://localhost:3000/callback");

        var result = await _controller.GetConfig(CoStateKey, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task ExchangeCode_WhenBodyNull_Returns400()
    {
        var result = await _controller.ExchangeCode(CoStateKey, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ExchangeCode_WhenCodeMissing_Returns400()
    {
        var body = new ExchangeCodeRequest(Code: null!, "code_verifier_value");

        var result = await _controller.ExchangeCode(CoStateKey, body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ExchangeCode_WhenCodeVerifierMissing_Returns400()
    {
        var body = new ExchangeCodeRequest("code_value", null!);

        var result = await _controller.ExchangeCode(CoStateKey, body, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }
}
