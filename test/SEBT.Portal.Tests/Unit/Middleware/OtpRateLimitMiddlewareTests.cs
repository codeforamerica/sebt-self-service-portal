using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.Api.Middleware;

namespace SEBT.Portal.Tests.Unit.Middleware;

public class OtpRateLimitMiddlewareTests
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OtpRateLimitMiddleware> _logger;
    private readonly OtpRateLimitMiddleware _middleware;

    public OtpRateLimitMiddlewareTests()
    {
        _next = Substitute.For<RequestDelegate>();
        _logger = Substitute.For<ILogger<OtpRateLimitMiddleware>>();
        _middleware = new OtpRateLimitMiddleware(_next, _logger);
    }

    /// <summary>
    /// Tests that the middleware extracts email from JSON body for OTP request endpoint.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldExtractEmail_WhenOtpRequestEndpoint()
    {
        // Arrange
        var email = "user@example.com";
        await using var bodyStream = CreateStream(CreateJsonBody(email));
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(email.ToLowerInvariant(), httpContext.Items["RateLimitEmail"]);
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware extracts email with case-insensitive property name.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldExtractEmail_CaseInsensitive()
    {
        // Arrange
        var email = "User@Example.COM";
        var jsonBody = $@"{{""Email"": ""{email}""}}";
        await using var bodyStream = CreateStream(jsonBody);
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.True(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(email.ToLowerInvariant(), httpContext.Items["RateLimitEmail"]);
    }

    /// <summary>
    /// Tests that the middleware does not extract email for non-OTP endpoints.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldNotExtractEmail_WhenNotOtpEndpoint()
    {
        // Arrange
        await using var bodyStream = CreateStream(CreateJsonBody("user@example.com"));
        var httpContext = CreateHttpContext("/api/other/endpoint", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with invalid JSON.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenJsonIsInvalid()
    {
        // Arrange
        await using var bodyStream = CreateStream("{ invalid json }");
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with empty body.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenBodyIsEmpty()
    {
        // Arrange
        await using var bodyStream = new MemoryStream();
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with missing email property.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenEmailPropertyIsMissing()
    {
        // Arrange
        await using var bodyStream = CreateStream(@"{""otherProperty"": ""value""}");
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware resets the body stream position after reading.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldResetBodyStreamPosition_AfterReading()
    {
        // Arrange
        var email = "user@example.com";
        await using var bodyStream = CreateStream(CreateJsonBody(email));
        var initialPosition = bodyStream.Position;
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(initialPosition, bodyStream.Position);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with oversized bodies.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenBodyExceedsMaxSize()
    {
        // Arrange - Create a body larger than MaxBodySize (1024 bytes)
        var largeBody = new string('a', 2048); // 2KB body
        var jsonBody = $@"{{""email"": ""{largeBody}@example.com""}}";
        await using var bodyStream = CreateStream(jsonBody);
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert - Should reject request due to size limit
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with whitespace-only email.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenEmailIsWhitespace()
    {
        // Arrange
        await using var bodyStream = CreateStream(@"{""email"": ""   ""}");
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with null email value.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenEmailIsNull()
    {
        // Arrange
        await using var bodyStream = CreateStream(@"{""email"": null}");
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware rejects requests with empty email string.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRejectRequest_WhenEmailIsEmpty()
    {
        // Arrange
        await using var bodyStream = CreateStream(@"{""email"": """"}");
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        await _next.DidNotReceive().Invoke(httpContext);
    }

    // Helper methods to reduce code duplication
    private HttpContext CreateHttpContext(string path, string method, Stream bodyStream)
    {
        var httpContext = new DefaultHttpContext();

        httpContext.Request.Path = new PathString(path);
        httpContext.Request.Method = method;
        httpContext.Request.Body = bodyStream;
        httpContext.Request.Headers.Clear();

        httpContext.Request.EnableBuffering();

        httpContext.Response.Body = new MemoryStream();
        httpContext.Response.StatusCode = 200;

        return httpContext;
    }

    private static Stream CreateStream(string content)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(content);
        return new MemoryStream(bodyBytes);
    }

    private static string CreateJsonBody(string email)
    {
        return $@"{{""email"": ""{email}""}}";
    }
}
