using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Api.Extensions;

/// <summary>
/// Extension methods for configuring database seeding in Development environment.
/// </summary>
public static class DatabaseSeedingExtensions
{
    /// <summary>
    /// Configures database seeding for Development environment.
    /// </summary>
    /// <param name="optionsBuilder">The DbContext options builder.</param>
    /// <param name="configuration">The configuration instance to read settings from.</param>
    public static void ConfigureDevelopmentSeeding(this DbContextOptionsBuilder optionsBuilder, IConfiguration? configuration = null)
    {
        var useMockHouseholdData = configuration?.GetValue<bool>("UseMockHouseholdData", false) ?? false;

        // These are called automatically during migrations, EnsureCreated, and `dotnet ef database update`
        // Both UseSeeding and UseAsyncSeeding must implement similar logic; EF Core tooling relies on the sync version.
        // See: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
        optionsBuilder.UseSeeding((context, _) =>
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
            {
                return;
            }

            if (context is not PortalDbContext portalContext)
            {
                return;
            }

            if (portalContext.Users.Any())
            {
                return;
            }

            var serviceProvider = portalContext.GetInfrastructure().GetService<IServiceProvider>();
            var logger = serviceProvider?.GetService<ILogger<DatabaseSeeder>>();
            var timeProvider = serviceProvider?.GetService<TimeProvider>() ?? TimeProvider.System;

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder, logger, timeProvider);
            seeder.SeedTestUsers(useMockHouseholdData);
        })
        .UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment != "Development")
            {
                return;
            }

            if (context is not PortalDbContext portalContext)
            {
                return;
            }

            if (await portalContext.Users.AnyAsync(cancellationToken))
            {
                return;
            }

            var serviceProvider = portalContext.GetInfrastructure().GetService<IServiceProvider>();
            var logger = serviceProvider?.GetService<ILogger<DatabaseSeeder>>();
            var timeProvider = serviceProvider?.GetService<TimeProvider>() ?? TimeProvider.System;

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder, logger, timeProvider);
            await seeder.SeedTestUsersAsync(useMockHouseholdData, cancellationToken);
        });
    }
}
