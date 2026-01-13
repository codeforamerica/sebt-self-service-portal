using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Repositories;
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
    /// <param name="configuration">The configuration instance to read settings from (unused; reserved for future use).</param>
    public static void ConfigureDevelopmentSeeding(this DbContextOptionsBuilder optionsBuilder, IConfiguration? configuration = null)
    {
        // These are called automatically during migrations, EnsureCreated, and `dotnet ef database update`
        // Both `UseSeeding` and `UseAsyncSeeding` are recommended for compatibility.
        // See: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
        optionsBuilder.UseSeeding((context, _) =>
        {
            // Sync callback is a no-op; seeding runs in UseAsyncSeeding below.
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

            var userRepository = new DatabaseUserRepository(portalContext);
            var seeder = new DatabaseSeeder(userRepository, portalContext);
            await seeder.SeedTestUsersAsync(cancellationToken);
        });
    }
}
