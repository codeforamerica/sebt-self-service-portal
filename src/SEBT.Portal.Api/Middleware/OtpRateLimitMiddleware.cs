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

            string body;
            try
            {
                body = await ReadBodyWithLimitAsync(context.Request.Body, MaxBodySize);
            }
            catch (RequestBodyTooLargeException ex)
            {
                _logger.LogWarning(ex, "OTP request body exceeds maximum size. Rejecting request.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = $"Request body exceeds maximum size of {MaxBodySize} bytes." });
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error reading request body. Rejecting request.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = "Invalid request format." });
                return;
            }

            try
            {
                context.Request.Body.Position = 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset request body stream position. This may indicate a stream configuration issue.");
                // Continue processing - the body has already been read
            }

            // For OTP request endpoint, we require a valid email to be extracted
            // This prevents bypassing email-based rate limiting with malformed requests
            if (string.IsNullOrEmpty(body))
            {
                _logger.LogWarning("OTP request received with empty body. Rejecting request.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = "Request body is required and must contain a valid email address." });
                return;
            }

            var emailExtracted = ExtractEmailFromJson(body, context);
            if (!emailExtracted)
            {
                _logger.LogWarning("OTP request received but email could not be extracted from request body. Rejecting request.");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { Error = "Request body must be valid JSON containing an 'email' property." });
                return;
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
            throw new RequestBodyTooLargeException(maxSizeBytes);
        }

        if (bytesRead == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    /// <summary>
    /// Exception thrown when a request body exceeds the maximum allowed size.
    /// </summary>
    private sealed class RequestBodyTooLargeException : InvalidOperationException
    {
        public int MaxSizeBytes { get; }

        public RequestBodyTooLargeException(int maxSizeBytes)
            : base($"Request body exceeds maximum size of {maxSizeBytes} bytes")
        {
            MaxSizeBytes = maxSizeBytes;
        }
    }

    /// <summary>
    /// Extracts the email address from a JSON request body and stores it in HttpContext.Items.
    /// </summary>
    /// <param name="jsonBody">The JSON body string to parse.</param>
    /// <param name="context">The HTTP context to store the email in.</param>
    /// <returns>True if a valid email was extracted; otherwise, false.</returns>
    private bool ExtractEmailFromJson(string jsonBody, HttpContext context)
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
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse JSON body for email extraction.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error parsing JSON for email extraction");
            return false;
        }
    }
}
