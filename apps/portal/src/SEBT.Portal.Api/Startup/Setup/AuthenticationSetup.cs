using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Api.Startup.Setup.Options;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddPortalAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // State allowlist for OIDC login endpoints. An instance is considered a
        // configured OIDC tenant if its loaded config overlay has Oidc:DiscoveryEndpoint
        // set. The current STATE env var is the only allowed state when that's true;
        // everything else (no STATE, no Oidc block) produces an empty allowlist and all
        // OIDC routes reject all stateCode inputs. This prevents the route parameter
        // from being used as a tenant escape.
        var allowedOidcStates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuration["Oidc:DiscoveryEndpoint"]))
        {
            var currentState = Environment.GetEnvironmentVariable("STATE");
            if (!string.IsNullOrWhiteSpace(currentState))
                allowedOidcStates.Add(currentState);
        }

        services.AddSingleton<IStateAllowlist>(new StateAllowlist(allowedOidcStates));

        // Configure JWT Authentication
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        // Configure JWT Bearer options using IOptions<JwtSettings> pattern
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme);
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, PostConfigureJwtSettings>();

        services.AddAuthorization();

        return services;
    }
}
