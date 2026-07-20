using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SEBT.Portal.Infrastructure.Data;

/// <summary>
/// Design-time factory for PortalDbContext. Used by EF Core tooling (dotnet ef migrations)
/// to create the DbContext without requiring the full application startup pipeline.
/// This avoids issues with plugin loading, feature flags, and other runtime dependencies
/// that are irrelevant to schema generation.
/// </summary>
public class DesignTimePortalDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../SEBT.Portal.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=SebtPortal;User Id=sa;Password=YourStrong@Passw0rd;";

        var optionsBuilder = new DbContextOptionsBuilder<PortalDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new PortalDbContext(optionsBuilder.Options);
    }
}
