using Microsoft.EntityFrameworkCore;
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
    public static void ConfigureDevelopmentSeeding(this DbContextOptionsBuilder optionsBuilder)
    {
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

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder);
            seeder.SeedTestUsers();
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

            var dataSeeder = new DataSeeder(portalContext);
            var seeder = new DatabaseSeeder(dataSeeder);
            await seeder.SeedTestUsersAsync(cancellationToken);
        });
    }
}
