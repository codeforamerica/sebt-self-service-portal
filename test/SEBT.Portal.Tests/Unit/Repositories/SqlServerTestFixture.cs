using Microsoft.EntityFrameworkCore;
using Testcontainers;
using Testcontainers.MsSql;
using SEBT.Portal.Infrastructure.Data;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Test fixture that provides a SQL Server container for integration tests.
/// </summary>
public class SqlServerTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public SqlServerTestFixture()
    {
        // Password is hardcoded for test isolation
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();

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
    /// Creates a new DbContext instance connected to the test container.
    /// </summary>
    public PortalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new PortalDbContext(options);
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

