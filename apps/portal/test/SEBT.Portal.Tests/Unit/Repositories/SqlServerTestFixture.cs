using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers;
using Testcontainers.MsSql;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Tests.Helpers;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Test fixture that provides a SQL Server container for integration tests.
/// </summary>
public class SqlServerTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _isolatedDatabases = new();

    public SqlServerTestFixture()
    {
        // Password is hardcoded for test isolation
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartWithRetryAsync();

            // Apply migrations to create the schema. Using MigrateAsync instead of
            // EnsureCreatedAsync so that raw SQL migrations (e.g., filtered indexes) are applied.
            var options = new DbContextOptionsBuilder<PortalDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            using var context = new PortalDbContext(options);
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to initialize SQL Server test container: {ex.Message}", ex);
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new DbContext instance connected to the shared database in the test container.
    /// </summary>
    public PortalDbContext CreateContext()
    {
        return CreateContext(ConnectionString);
    }

    /// <summary>
    /// Creates a new DbContext instance connected to the given database.
    /// </summary>
    public static PortalDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new PortalDbContext(options);
    }

    /// <summary>
    /// Returns the connection string for a migrated database, separate from the shared one,
    /// for tests whose subject reads or deletes every row in a table. The database is created
    /// on the first call for a given name and reused by later calls.
    /// </summary>
    public Task<string> GetIsolatedDatabaseAsync(string databaseName)
    {
        return _isolatedDatabases
            .GetOrAdd(databaseName, name => new Lazy<Task<string>>(() => CreateMigratedDatabaseAsync(name)))
            .Value;
    }

    private async Task<string> CreateMigratedDatabaseAsync(string databaseName)
    {
        var connectionString = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return connectionString;
    }
}

/// <summary>
/// Collection fixture to share the SQL Server container across all tests in the collection.
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

