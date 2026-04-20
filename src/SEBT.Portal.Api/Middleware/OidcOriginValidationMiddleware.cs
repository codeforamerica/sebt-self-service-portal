namespace SEBT.Portal.Api.Middleware;

/// <summary>
/// validates the <c>Origin</c> header on OIDC auth POST endpoints.
/// Browsers set <c>Origin</c> automatically on same-origin fetch POSTs; a missing
/// or mismatched header indicates cross-origin replay or a non-browser client that
/// we don't expect on these endpoints.
///
/// Policy:
/// <list type="bullet">
///   <item>POST to <c>/api/auth/oidc/*</c> → require <c>Origin</c> matches the
///         configured portal origin. Reject with 403 if mismatched.</item>
///   <item>GET (config) → skip (idempotent, returns public info).</item>
///   <item><c>Referer</c> is logged when mismatched but not enforced (privacy
///         extensions strip it).</item>
/// </list>
/// </summary>
public class OidcOriginValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OidcOriginValidationMiddleware> _logger;
    private readonly HashSet<string> _allowedOrigins;

    /// <summary>
    /// Route prefix this middleware guards (with trailing slash to avoid matching
    /// unrelated paths like <c>/api/auth/oidc-admin/</c>). Only POST requests whose
    /// path starts with this prefix are checked; all other requests pass through.
    /// </summary>
    private const string OidcPathPrefix = "/api/auth/oidc/";

    /// <summary>Builds allowed-origin set from configured <c>Oidc:CallbackRedirectUri</c>.</summary>
    public OidcOriginValidationMiddleware(
        RequestDelegate next,
        ILogger<OidcOriginValidationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        // Build the allowed-origins set from CallbackRedirectUri (which contains the portal origin).
        // In production this is e.g. "https://sunbucks.co.gov"; in dev "http://localhost:3000".
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var callbackUri = configuration["Oidc:CallbackRedirectUri"];
        if (!string.IsNullOrEmpty(callbackUri) && Uri.TryCreate(callbackUri, UriKind.Absolute, out var uri))
        {
            _allowedOrigins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        // Step-up may have a different redirect URI (though it rarely has a different origin).
        var stepUpUri = configuration["Oidc:StepUp:RedirectUri"];
        if (!string.IsNullOrEmpty(stepUpUri) && Uri.TryCreate(stepUpUri, UriKind.Absolute, out var su))
        {
            _allowedOrigins.Add(su.GetLeftPart(UriPartial.Authority));
        }
    }

    /// <summary>Validates Origin on OIDC POST requests; passes all others through.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var isOidcPost = context.Request.Method == HttpMethods.Post
            && path != null
            && path.StartsWith(OidcPathPrefix, StringComparison.OrdinalIgnoreCase);

        if (isOidcPost)
        {
            var origin = context.Request.Headers.Origin.FirstOrDefault();
            if (string.IsNullOrEmpty(origin))
            {
                _logger.LogWarning(
                    "OIDC Origin check: POST to {Path} missing Origin header (reason=missing_origin)",
                    path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Missing Origin header." });
                return;
            }

            if (!_allowedOrigins.Contains(origin))
            {
                _logger.LogWarning(
                    "OIDC Origin check: POST to {Path} has disallowed Origin {Origin} (reason=origin_mismatch, allowed={Allowed})",
                    path,
                    origin,
                    string.Join(", ", _allowedOrigins));
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Origin not allowed." });
                return;
            }

            // Referer: log-only when mismatched. Privacy tools strip it; Origin is the real defense.
            var referer = context.Request.Headers.Referer.FirstOrDefault();
            if (!string.IsNullOrEmpty(referer)
                && Uri.TryCreate(referer, UriKind.Absolute, out var refUri)
                && !_allowedOrigins.Contains(refUri.GetLeftPart(UriPartial.Authority)))
            {
                _logger.LogInformation(
                    "OIDC Referer mismatch (log-only): POST to {Path} has Referer origin {RefererOrigin} outside allowed set",
                    path,
                    refUri.GetLeftPart(UriPartial.Authority));
            }
        }

        await _next(context);
    }
}
