using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection AddPortalInfrastructureServices(this IServiceCollection services)
    {
        // Otp Services
        services.AddTransient<IOtpSenderService, EmailOtpSenderService>();
        services.AddTransient<IOtpGeneratorService, OtpGeneratorService>();
        services.AddTransient<ISmtpClientService, MailKitClientService>();

        // JWT Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureRepositories(this IServiceCollection services)
    {
        services.AddTransient<IOtpRepository, InMemoryOtpRepository>();
        services.AddTransient<IUserRepository, DatabaseUserRepository>();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds the database context for the portal application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="configureOptions">Optional action to configure DbContext options (e.g., for seeding).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPortalDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<PortalDbContext>(options =>
        {
            options.UseSqlServer(connectionString)
                // These are called automatically during migrations, EnsureCreated, and `dotnet ef database update`
                // Both `UseSeeding` and `UseAsyncSeeding` are recommended to be called for compatibility
                // reasons (some EF Core versions may not support the async version, for example).  
                // See: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
                .UseSeeding((context, _) =>
                {
                    // Only seed in Development environment
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                    if (environment != "Development")
                    {
                        return;
                    }

                    // Cast to PortalDbContext to access models DbSet
                    if (context is not PortalDbContext portalContext)
                    {
                        return;
                    }

                    // Check if records already exist to avoid re-seeding
                    if (portalContext.Users.Any())
                    {
                        return;
                    }

                    var userRepository = new Repositories.DatabaseUserRepository(portalContext);
                    var seeder = new DatabaseSeeder(userRepository, portalContext);
                    // Call async method synchronously for UseSeeding callback
                    seeder.SeedTestUsersAsync(CancellationToken.None).GetAwaiter().GetResult();
                })
                .UseAsyncSeeding(async (context, _, cancellationToken) =>
                {
                    // Only seed in Development environment
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                    if (environment != "Development")
                    {
                        return;
                    }

                    // Cast to PortalDbContext to access models DbSet
                    if (context is not PortalDbContext portalContext)
                    {
                        return;
                    }

                    // Check if records already exist to avoid re-seeding
                    if (await portalContext.Users.AnyAsync(cancellationToken))
                    {
                        return;
                    }

                    var userRepository = new Repositories.DatabaseUserRepository(portalContext);
                    var seeder = new DatabaseSeeder(userRepository, portalContext);
                    await seeder.SeedTestUsersAsync(cancellationToken);
                });

            configureOptions?.Invoke(options);
        });

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureAppSettings(this IServiceCollection services)
    {

        services.AddOptionsWithValidateOnStart<EmailOtpSenderServiceSettings>()
            .BindConfiguration(EmailOtpSenderServiceSettings.SectionName);
        services.AddOptionsWithValidateOnStart<SmtpClientSettings>()
            .BindConfiguration(SmtpClientSettings.SectionName);
        services.AddOptionsWithValidateOnStart<OtpRateLimitSettings>()
            .BindConfiguration(OtpRateLimitSettings.SectionName);
        services.AddOptionsWithValidateOnStart<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName);

        return services;
    }
}
