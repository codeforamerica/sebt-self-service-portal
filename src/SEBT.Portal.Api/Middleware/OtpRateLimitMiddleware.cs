using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Api.Middleware;

/// <summary>
/// Middleware that extracts the email address from the OTP request body
/// and stores it in HttpContext.Items for use by rate limiting partition key resolver.
/// </summary>
public class OtpRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OtpRateLimitMiddleware> _logger;
    private const string EmailKey = "RateLimitEmail";
    private const string OtpRequestPath = "/api/auth/otp/request";
    private const int MaxBodySize = 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtpRateLimitMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public OtpRateLimitMiddleware(RequestDelegate next, ILogger<OtpRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to extract email from request body for rate limiting.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Only process OTP request endpoint
        if (context.Request.Path.StartsWithSegments(OtpRequestPath) &&
            context.Request.Method == "POST")
        {
            context.Request.EnableBuffering();

            try
            {
                var body = await ReadBodyWithLimitAsync(context.Request.Body, MaxBodySize);
                context.Request.Body.Position = 0;

                // Try to extract email from JSON body
                if (!string.IsNullOrEmpty(body))
                {
                    ExtractEmailFromJson(body, context);
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to read request body for rate limiting. Request body may be too large.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error extracting email for rate limiting");
            }
        }

        await _next(context);
    }

    private static async Task<string> ReadBodyWithLimitAsync(Stream bodyStream, int maxSizeBytes)
    {
        var buffer = new byte[maxSizeBytes + 1];
        var bytesRead = await bodyStream.ReadAsync(buffer, 0, maxSizeBytes + 1);
        
        if (bytesRead > maxSizeBytes)
        {
            throw new InvalidOperationException($"Request body exceeds maximum size of {maxSizeBytes} bytes");
        }
        
        if (bytesRead == 0)
        {
            return string.Empty;
        }
        
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    private void ExtractEmailFromJson(string jsonBody, HttpContext context)
    {
        try
        {
            var options = new JsonDocumentOptions
            {
                MaxDepth = 2,
                AllowTrailingCommas = false
            };

            using var doc = JsonDocument.Parse(jsonBody, options);
            var root = doc.RootElement;
            
            JsonElement emailElement;
            if (root.TryGetProperty("email", out emailElement) ||
                root.TryGetProperty("Email", out emailElement))
            {
                var email = emailElement.GetString();
                if (!string.IsNullOrWhiteSpace(email))
                {
                    // Store email in HttpContext.Items for rate limiting partition key resolver
                    context.Items[EmailKey] = email.ToLowerInvariant();
                    _logger.LogDebug("Extracted email {Email} for rate limiting", email);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON body for email extraction. Continuing without email-based rate limiting.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error parsing JSON for email extraction");
        }
    }
}
