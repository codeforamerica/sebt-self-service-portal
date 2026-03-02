using Microsoft.EntityFrameworkCore;

namespace SEBT.Portal.Infrastructure;

/// <summary>
/// Extension methods for configuring the portal DbContext (e.g. for development seeding).
/// </summary>
public static class PortalDbContextExtensions
{
    /// <summary>
    /// Configures DbContext options for development seeding. No-op by default; override or add configuration as needed.
    /// </summary>
    public static DbContextOptionsBuilder ConfigureDevelopmentSeeding(this DbContextOptionsBuilder optionsBuilder)
    {
        return optionsBuilder;
    }
}
