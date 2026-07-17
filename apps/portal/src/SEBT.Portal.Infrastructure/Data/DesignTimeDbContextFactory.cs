using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SEBT.Portal.Infrastructure.Data;

/// <summary>
/// Factory for creating PortalDbContext at design time (EF Core migrations tooling).
/// Bypasses the full DI container, which requires plugin assemblies and external services
/// that aren't available during migration generation.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PortalDbContext>();
        // Connection string is only used for migration generation — not for runtime.
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=SebtPortal;");

        return new PortalDbContext(optionsBuilder.Options);
    }
}
