using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.UseCases.Auth.SessionLifetime;

namespace SEBT.Portal.Api.Startup.Setup.Options;

internal class PostConfigureJwtSettings(IOptions<JwtSettings> jwtSettingsOptions)
    : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var jwtSettings = jwtSettingsOptions.Value;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TokenDenylist.ClockSkewPadding,
            NameClaimType = "sub"
        };
        // Preserve JWT claim names (sub, email) so we can read them regardless of handler mapping.
        options.MapInboundClaims = false;

        // DC-242: portal session JWT lives in an HttpOnly cookie. Fall back to the cookie
        // when no Authorization header is present so the SPA never handles the raw token.
        // The Authorization header path is preserved for service-to-service callers.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    var cookieToken = context.Request.Cookies[AuthCookies.AuthCookieName];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                // Enforce the absolute session lifetime cap on every authenticated request.
                // Tokens missing auth_time (e.g., minted before the cap was introduced) or
                // older than the cap are rejected here so the SPA's 401 handler kicks in.
                if (context.Principal is null)
                {
                    return;
                }

                var policy = context.HttpContext.RequestServices
                    .GetRequiredService<SessionLifetimePolicy>();
                var outcome = policy.Evaluate(context.Principal);

                if (outcome != SessionLifetimePolicy.Outcome.Valid)
                {
                    AuthCookies.ClearAuthCookie(context.Response);
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogInformation(
                        "JWT rejected by absolute session lifetime policy: {Outcome}", outcome);
                    context.Fail($"Absolute session lifetime: {outcome}");
                    return;
                }

                // Reject tokens revoked by logout. Tokens without a jti cannot have been
                // denylisted; the lookup fails open so a cache outage never locks users out.
                var jti = context.Principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrEmpty(jti))
                {
                    return;
                }

                var denylist = context.HttpContext.RequestServices
                    .GetRequiredService<ITokenDenylist>();
                if (await denylist.IsDeniedAsync(jti, context.HttpContext.RequestAborted))
                {
                    AuthCookies.ClearAuthCookie(context.Response);
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogInformation("JWT rejected: token revoked at logout");
                    context.Fail("Token has been revoked");
                }
            }
        };
    }
}
