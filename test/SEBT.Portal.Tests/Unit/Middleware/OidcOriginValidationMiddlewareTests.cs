using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Api.Middleware;

namespace SEBT.Portal.Tests.Unit.Middleware;

/// <summary>
/// verifies that OIDC POST endpoints reject missing or mismatched
/// Origin headers, while allowing GET (config) and non-OIDC paths through.
/// </summary>
public class OidcOriginValidationMiddlewareTests
{
    private const string AllowedOrigin = "http://localhost:3000";
    private const string OidcCallbackPath = "/api/auth/oidc/callback";
    private const string OidcCompleteLoginPath = "/api/auth/oidc/complete-login";
    private const string OidcConfigPath = "/api/auth/oidc/co/config";
    private const string NonOidcPath = "/api/household/data";

    private static OidcOriginValidationMiddleware CreateMiddleware(
        RequestDelegate next,
        string? callbackRedirectUri = AllowedOrigin + "/callback")
    {
        var configData = new Dictionary<string, string?>();
        if (callbackRedirectUri != null)
            configData["Oidc:CallbackRedirectUri"] = callbackRedirectUri;
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        return new OidcOriginValidationMiddleware(
            next,
            NullLogger<OidcOriginValidationMiddleware>.Instance,
            config);
    }

    private static DefaultHttpContext CreateContext(string method, string path, string? origin = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (origin != null)
            context.Request.Headers.Origin = origin;
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task Post_OidcCallback_WithAllowedOrigin_PassesThrough()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", OidcCallbackPath, AllowedOrigin);

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_OidcCallback_WithMissingOrigin_Returns403()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", OidcCallbackPath, origin: null);

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_OidcCallback_WithWrongOrigin_Returns403()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", OidcCallbackPath, "https://evil.example.com");

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Post_CompleteLogin_WithMissingOrigin_Returns403()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", OidcCompleteLoginPath, origin: null);

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Get_OidcConfig_WithoutOrigin_PassesThrough()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("GET", OidcConfigPath, origin: null);

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task Post_NonOidcPath_WithoutOrigin_PassesThrough()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", NonOidcPath, origin: null);

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task Post_OidcCallback_OriginIsCaseInsensitive()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateContext("POST", OidcCallbackPath, "HTTP://LOCALHOST:3000");

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }
}
