using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using SEBT.Portal.Api.Middleware;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class RequestPipelineSetup
{
    public static WebApplication UsePortalRequestPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHttpsRedirection();
        }

        // Map X-Forwarded-For to HttpContext.Connection.RemoteIpAddress so that
        // IP-based rate limiting identifies distinct clients correctly.
        //
        // In production the .NET API runs on a private network behind the Next.js
        // server, which proxies all requests and forwards the real client IP via
        // X-Forwarded-For. Without this middleware every request appears to come
        // from the Next.js server's single private IP, collapsing all clients into
        // one rate-limit bucket.
        //
        // Current configuration uses open trust (cleared KnownProxies/KnownIPNetworks)
        // which is acceptable because the API is not directly reachable from the
        // public internet. ForwardLimit = 1 ensures only the last proxy hop is read,
        // preventing clients from prepending fake entries.
        //
        // TODO: For defense-in-depth, consider restricting trust to the VPC CIDR:
        //   options.KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/8"));
        // This would reject forwarded headers from any source outside the private
        // network, guarding against future topology changes that might expose the API.
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1,
        };

        // Open trust: accept X-Forwarded-For from any source. Safe here because
        // the API is on a private network with no public ingress. Clear the defaults
        // (loopback) so the middleware processes headers from all sources.
        forwardedHeadersOptions.KnownProxies.Clear();
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);

        app.UseRouting();

        // reject OIDC POST requests with missing or disallowed Origin header.
        // Runs before authentication so replay attempts from rogue origins fail early.
        app.UseMiddleware<OidcOriginValidationMiddleware>();

        app.UseMiddleware<OtpRateLimitMiddleware>();

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health",
            new HealthCheckOptions { ResponseWriter = HealthCheckResponseWriter.WriteAsync });

        return app;
    }
}
