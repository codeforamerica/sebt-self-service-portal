using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Api.Extensions;

/// <summary>
/// Extension methods for configuring database seeding in Development environment.
/// Seeding is separated from Infrastructure to maintain Clean Architecture compliance.
/// </summary>
public static class DatabaseSeedingExtensions
{
    /// <summary>
    /// Configures database seeding for Development environment.
    /// This is called separately from Infrastructure to avoid Infrastructure depending on Seeding project.
    /// </summary>
    /// <param name="optionsBuilder">The DbContext options builder.</param>
    /// <param name="configuration">The configuration instance to read settings from.</param>
    public static void ConfigureDevelopmentSeeding(this DbContextOptionsBuilder optionsBuilder, IConfiguration? configuration = null)
    {
        var useMockHouseholdData = configuration?.GetValue<bool>("UseMockHouseholdData", false) ?? false;

        // These are called automatically during migrations, EnsureCreated, and `dotnet ef database update`
        // Both `UseSeeding` and `UseAsyncSeeding` are recommended to be called for compatibility
        // reasons (some EF Core versions may not support the async version, for example).  
        // See: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
        optionsBuilder.UseSeeding((context, _) =>
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

            var logger = portalContext.GetService<ILogger<DatabaseSeeder>>();

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder, logger, TimeProvider.System);
            seeder.SeedTestUsers(useMockHouseholdData);
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

            var logger = portalContext.GetService<ILogger<DatabaseSeeder>>();

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder, logger, TimeProvider.System);
            await seeder.SeedTestUsersAsync(useMockHouseholdData, cancellationToken);
        });
    }
}
