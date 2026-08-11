using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class OidcSetup
{
    public static IServiceCollection AddOidcServices(this IServiceCollection services)
    {
        // OIDC token exchange (replaces the Next.js /api/auth/oidc/callback route)
        services.AddScoped<IOidcExchangeService, OidcExchangeService>();
        services.AddScoped<IOidcCallbackFailureLogger, OidcCallbackFailureLogger>();

        // pre-auth session store (HybridCache-backed, 15 min TTL)
        services.AddSingleton<IPreAuthSessionStore, PreAuthSessionStore>();
        services.AddSingleton<ITokenDenylist, TokenDenylist>();

        return services;
    }
}
