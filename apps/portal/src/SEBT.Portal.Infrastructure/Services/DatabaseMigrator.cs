using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Infrastructure.Data;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="IDatabaseMigrator"/> that applies Entity Framework Core migrations.
/// </summary>
public class DatabaseMigrator : IDatabaseMigrator
{
    private readonly PortalDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseMigrator"/> class.
    /// </summary>
    /// <param name="dbContext">The database context to migrate.</param>
    public DatabaseMigrator(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);
    }
}
