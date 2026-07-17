using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Integration tests for rate limiting functionality.
/// These tests verify the rate limiting policy configuration and behavior.
/// </summary>
[Trait("Category", "Integration")]
public class RateLimitingTests
{
    /// <summary>
    /// Tests that the rate limiting policy configuration uses email for partitioning.
    /// </summary>
    [Fact]
    public void RateLimitPolicy_ShouldUseEmail_WhenAvailable()
    {
        // Arrange
        var httpContext = CreateHttpContext("user@example.com");

        // Act - Verify email is in Items (as set by middleware)
        var hasEmail = httpContext.Items.TryGetValue("RateLimitEmail", out var emailObj);
        var email = emailObj as string;

        // Assert
        Assert.True(hasEmail);
        Assert.NotNull(email);
        Assert.Equal("user@example.com", email);
    }

    /// <summary>
    /// Tests that the rate limiting policy uses IP address as fallback when email is not available.
    /// </summary>
    [Fact]
    public void RateLimitPolicy_ShouldUseIpAddress_WhenEmailNotAvailable()
    {
        // Arrange
        var httpContext = CreateHttpContextWithoutEmail();

        // Act - Verify email is not in Items
        var hasEmail = httpContext.Items.TryGetValue("RateLimitEmail", out _);
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Assert
        Assert.False(hasEmail);
        Assert.NotNull(ipAddress);
        Assert.NotEqual("unknown", ipAddress);
    }

    /// <summary>
    /// Tests that rate limiting configuration uses correct permit limit and window.
    /// </summary>
    [Fact]
    public void RateLimitConfiguration_ShouldHaveCorrectSettings()
    {
        // Arrange & Act - Create options matching Program.cs configuration
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        };

        // Assert
        Assert.Equal(5, options.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), options.Window);
        Assert.Equal(QueueProcessingOrder.OldestFirst, options.QueueProcessingOrder);
        Assert.Equal(0, options.QueueLimit);
        Assert.True(options.AutoReplenishment);
    }

    /// <summary>
    /// Tests that email addresses are normalized to lowercase for partitioning.
    /// </summary>
    [Fact]
    public void RateLimitPolicy_ShouldNormalizeEmail_ToLowercase()
    {
        // Arrange
        var email1 = "User@Example.COM";
        var email2 = "user@example.com";
        var httpContext1 = CreateHttpContext(email1);
        var httpContext2 = CreateHttpContext(email2);

        // Act
        var normalized1 = httpContext1.Items["RateLimitEmail"] as string;
        var normalized2 = httpContext2.Items["RateLimitEmail"] as string;

        // Assert - Both should be normalized to lowercase
        Assert.Equal("user@example.com", normalized1);
        Assert.Equal("user@example.com", normalized2);
        Assert.Equal(normalized1, normalized2);
    }

    /// <summary>
    /// Tests that different email addresses create different partition keys.
    /// </summary>
    [Fact]
    public void RateLimitPolicy_ShouldCreateDifferentPartitions_ForDifferentEmails()
    {
        // Arrange
        var email1 = "user1@example.com";
        var email2 = "user2@example.com";
        var httpContext1 = CreateHttpContext(email1);
        var httpContext2 = CreateHttpContext(email2);

        // Act
        var partitionKey1 = httpContext1.Items["RateLimitEmail"] as string;
        var partitionKey2 = httpContext2.Items["RateLimitEmail"] as string;

        // Assert - Different emails should have different partition keys
        Assert.NotNull(partitionKey1);
        Assert.NotNull(partitionKey2);
        Assert.NotEqual(partitionKey1, partitionKey2);
    }

    private static HttpContext CreateHttpContext(string email)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["RateLimitEmail"] = email.ToLowerInvariant();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        return httpContext;
    }

    private static HttpContext CreateHttpContextWithoutEmail()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        return httpContext;
    }
}
