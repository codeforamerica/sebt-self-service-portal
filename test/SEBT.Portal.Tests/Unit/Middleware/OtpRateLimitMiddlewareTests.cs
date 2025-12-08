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
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(CreateJsonBody(email)));

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
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(jsonBody));

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
        var httpContext = CreateHttpContext("/api/other/endpoint", "POST", CreateStream(CreateJsonBody("user@example.com")));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles invalid JSON gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldHandleInvalidJson_Gracefully()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream("{ invalid json }"));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles empty body gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldHandleEmptyBody_Gracefully()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", new MemoryStream());

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles missing email property gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldHandleMissingEmailProperty_Gracefully()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(@"{""otherProperty"": ""value""}"));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware resets the body stream position after reading.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldResetBodyStreamPosition_AfterReading()
    {
        // Arrange
        var email = "user@example.com";
        var bodyStream = CreateStream(CreateJsonBody(email));
        var initialPosition = bodyStream.Position;
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", bodyStream);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(initialPosition, bodyStream.Position);
    }

    /// <summary>
    /// Tests that the middleware handles oversized request bodies gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldHandleOversizedBody_Gracefully()
    {
        // Arrange - Create a body larger than MaxBodySize (1024 bytes)
        var largeBody = new string('a', 2048); // 2KB body
        var jsonBody = $@"{{""email"": ""{largeBody}@example.com""}}";
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(jsonBody));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert - Should not extract email due to size limit, but request should continue
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles whitespace-only email gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldNotExtractEmail_WhenEmailIsWhitespace()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(@"{""email"": ""   ""}"));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles null email value gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldNotExtractEmail_WhenEmailIsNull()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(@"{""email"": null}"));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    /// <summary>
    /// Tests that the middleware handles empty email string gracefully.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldNotExtractEmail_WhenEmailIsEmpty()
    {
        // Arrange
        var httpContext = CreateHttpContext("/api/auth/otp/request", "POST", CreateStream(@"{""email"": """"}"));

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.False(httpContext.Items.ContainsKey("RateLimitEmail"));
        await _next.Received(1).Invoke(httpContext);
    }

    // Helper methods to reduce code duplication
    private HttpContext CreateHttpContext(string path, string method, Stream bodyStream)
    {
        var httpContext = Substitute.For<HttpContext>();
        
        var requestFeature = new HttpRequestFeature();
        var featureCollection = new FeatureCollection();
        featureCollection.Set<IHttpRequestFeature>(requestFeature);
        httpContext.Features.Returns(featureCollection);
        
        var request = Substitute.For<HttpRequest>();
        httpContext.Request.Returns(request);
        request.Path.Returns(new PathString(path));
        request.Method.Returns(method);
        request.Body.Returns(bodyStream);
        request.Headers.Returns(new HeaderDictionary());
        
        var items = new Dictionary<object, object?>();
        httpContext.Items.Returns(items);

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
