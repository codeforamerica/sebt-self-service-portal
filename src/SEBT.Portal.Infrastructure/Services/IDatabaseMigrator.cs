namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service for applying database migrations.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Applies any pending migrations to the database.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
