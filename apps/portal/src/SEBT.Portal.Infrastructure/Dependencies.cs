using Medallion.Threading;
using Medallion.Threading.Redis;
using Medallion.Threading.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Extensions;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using StackExchange.Redis;

namespace SEBT.Portal.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection AddPortalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddAppSettings(configuration);
        services.AddServices(configuration);
        services.AddRepositories();
        services.AddPortalDbContext(configuration,
            options => options.ConfigureDevelopmentSeeding());
        services.AddDistributedLocking(configuration, environment);

        return services;
    }

    /// <summary>
    /// Registers caching services. When Redis is configured (via structured settings
    /// or legacy connection string), uses Redis as the distributed cache (L2) backing
    /// HybridCache. Otherwise, falls back to in-memory caching only — except in
    /// non-Development environments with OIDC configured, where Redis is required for
    /// cross-container session lookup and startup fails fast.
    /// Call this before AddPlugins — plugins may depend on HybridCache.
    /// </summary>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration? configuration,
        IHostEnvironment environment)
    {
        var redisOptions = configuration.ResolveRedisConfigurationOptions(environment);

        if (redisOptions != null)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisOptions;
            });
        }
        else if (!environment.IsDevelopment()
            && !string.IsNullOrEmpty(configuration?["Oidc:DiscoveryEndpoint"]))
        {
            // Outside Development, OIDC + no Redis is misconfiguration: pre-auth sessions
            // live in a per-container in-memory cache, so callbacks landing on a different
            // container than the authorize-redirect see missing_session or replay errors.
            // Fail fast at startup instead of silently shipping a broken login flow.
            throw new InvalidOperationException(
                "Redis is required when OIDC is configured outside Development: " +
                "set Redis:Host (or legacy ConnectionStrings:Redis). " +
                "Cross-container session lookup depends on a shared distributed cache.");
        }
        else
        {
            // Fallback so IDistributedCache is always resolvable (PreAuthSessionStore
            // depends on it). Used for local dev without Redis and for integration tests
            // that omit Redis config.
            services.AddDistributedMemoryCache();
        }

        // HybridCache provides an L1 in-memory cache with optional L2 distributed backing.
        // When Redis is registered above, HybridCache automatically uses it as L2.
        // When Redis is not configured, HybridCache operates as in-memory only.
        services.AddHybridCache();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Registers a distributed lock provider. Uses Redis when Redis is configured
    /// (via structured settings or legacy connection string); otherwise falls back
    /// to SQL Server application locks.
    /// </summary>
    private static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var redisOptions = configuration.ResolveRedisConfigurationOptions(environment);

        if (redisOptions != null)
        {
            services.AddSingleton<IDistributedLockProvider>(_ =>
            {
                var connection = ConnectionMultiplexer.Connect(redisOptions);
                return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
            });
        }
        else
        {
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is required for distributed locking.");
            services.AddSingleton<IDistributedLockProvider>(
                new SqlDistributedSynchronizationProvider(sqlConnectionString));
        }

        return services;
    }
}
