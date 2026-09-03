using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class RateLimitingSetup
{
    public static IServiceCollection AddPortalRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.OnRejected = OnRejected;

            AddOtpPolicy(options);
            AddEnrollmentCheckPolicy(options);
            AddCheckerFeaturesPolicy(options);
            AddWebhookPolicy(options);
        });

        return services;
    }

    private static async ValueTask OnRejected(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Determine which rate-limit policy rejected the request to show an appropriate message
        var endpoint = context.HttpContext.GetEndpoint();
        var rateLimitAttribute = endpoint?.Metadata
            .OfType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .FirstOrDefault();

        if (rateLimitAttribute?.PolicyName == RateLimitPolicies.EnrollmentCheck)
        {
            var enrollmentSettings = context.HttpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<EnrollmentCheckRateLimitSettings>>()
                .CurrentValue;
            var windowDescription = enrollmentSettings.WindowMinutes == 1.0
                ? "minute"
                : $"{enrollmentSettings.WindowMinutes} minutes";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = $"Rate limit exceeded. Maximum {enrollmentSettings.PermitLimit} enrollment checks per {windowDescription} allowed." },
                cancellationToken);
        }
        else if (rateLimitAttribute?.PolicyName == RateLimitPolicies.CheckerFeatures)
        {
            // The checker's features poll just retries on its next cycle; no
            // user-facing message is needed.
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = "Rate limit exceeded." },
                cancellationToken);
        }
        else if (rateLimitAttribute?.PolicyName == RateLimitPolicies.Webhook)
        {
            // Webhook callers (Socure) don't need a friendly message, but log for observability
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = "Rate limit exceeded." },
                cancellationToken);
        }
        else
        {
            var otpSettings = context.HttpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<OtpRateLimitSettings>>()
                .CurrentValue;
            var windowDescription = otpSettings.WindowMinutes == 1.0
                ? "minute"
                : $"{otpSettings.WindowMinutes} minutes";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = $"Rate limit exceeded. Maximum {otpSettings.PermitLimit} OTP requests per {windowDescription} allowed." },
                cancellationToken);
        }
    }

    // Add fixed window limiter policy for OTP requests with email-based partitioning
    private static void AddOtpPolicy(RateLimiterOptions options)
    {
        options.AddPolicy(RateLimitPolicies.Otp, httpContext =>
        {
            var rateLimitOptions = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<OtpRateLimitSettings>>()
                .CurrentValue;

            // Try to get email from HttpContext.Items (set by OtpRateLimitMiddleware)
            if (httpContext.Items.TryGetValue("RateLimitEmail", out var emailObj) &&
                emailObj is string email && !string.IsNullOrEmpty(email))
            {
                // Partition by email address
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: email,
                    factory: _ => CreateOtpRateLimitOptions(rateLimitOptions));
            }

            // If email not found, use IP address as fallback
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: _ => CreateOtpRateLimitOptions(rateLimitOptions));
        });
    }

    // Add fixed window limiter policy for enrollment check requests with IP-based partitioning
    private static void AddEnrollmentCheckPolicy(RateLimiterOptions options)
    {
        options.AddPolicy(RateLimitPolicies.EnrollmentCheck, httpContext =>
        {
            var rateLimitOptions = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<EnrollmentCheckRateLimitSettings>>()
                .CurrentValue;

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"enrollment-check:{ipAddress}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.PermitLimit,
                    Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    }

    // Add fixed window limiter policy for the checker features poll with IP-based
    // partitioning. Deliberately separate from the enrollment-check policy: every open
    // checker tab polls features once a minute, so a shared partition would let a few
    // tabs behind one NAT (school computer lab, library) drain the per-IP budget that
    // real enrollment checks need.
    private static void AddCheckerFeaturesPolicy(RateLimiterOptions options)
    {
        options.AddPolicy(RateLimitPolicies.CheckerFeatures, httpContext =>
        {
            var rateLimitOptions = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<CheckerFeaturesRateLimitSettings>>()
                .CurrentValue;

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"checker-features:{ipAddress}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.PermitLimit,
                    Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    }

    // Add fixed window limiter policy for Socure webhook endpoint with IP-based partitioning
    // TODO: Confirm appropriate thresholds with Socure and the team
    private static void AddWebhookPolicy(RateLimiterOptions options)
    {
        options.AddPolicy(RateLimitPolicies.Webhook, httpContext =>
        {
            var webhookRateLimitOptions = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<WebhookRateLimitSettings>>()
                .CurrentValue;

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"webhook:{ipAddress}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = webhookRateLimitOptions.PermitLimit,
                    Window = TimeSpan.FromMinutes(webhookRateLimitOptions.WindowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    }

    private static FixedWindowRateLimiterOptions CreateOtpRateLimitOptions(OtpRateLimitSettings settings) => new()
    {
        PermitLimit = settings.PermitLimit,
        Window = TimeSpan.FromMinutes(settings.WindowMinutes),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    };
}
