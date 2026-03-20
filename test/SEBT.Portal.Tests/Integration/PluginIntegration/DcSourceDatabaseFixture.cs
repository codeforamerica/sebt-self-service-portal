using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Test fixture that provides a SQL Server container simulating the DC source database.
/// Creates the dbo.EligibleChildren lookup table, seeds one eligible child, and installs
/// a stub dbo.sp_CheckEligibility stored procedure matching the DC connector's parameter
/// signature.
/// </summary>
public class DcSourceDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public DcSourceDatabaseFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();
    }

    /// <summary>
    /// Connection string to the test DC source database.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await CreateSchemaAndSeedData();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task CreateSchemaAndSeedData()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Create the lookup table that the stub stored procedure queries
        await ExecuteSql(connection, """
            CREATE TABLE dbo.EligibleChildren (
                FirstName NVARCHAR(100) NOT NULL,
                LastName NVARCHAR(100) NOT NULL,
                DateOfBirth DATE NOT NULL
            )
            """);

        // Seed one eligible child for the "match" test case
        await ExecuteSql(connection, """
            INSERT INTO dbo.EligibleChildren (FirstName, LastName, DateOfBirth)
            VALUES ('Jane', 'Doe', '2015-03-12')
            """);

        // Create the stub stored procedure matching the DC connector's
        // DcEnrollmentCheckService parameter signature. It reads firstName,
        // lastName, and dateOfBirth from the @formData JSON parameter and
        // checks against the EligibleChildren table.
        await ExecuteSql(connection, """
            CREATE PROCEDURE dbo.sp_CheckEligibility
                @submissionId NVARCHAR(100),
                @formData NVARCHAR(MAX),
                @isEligible BIT OUTPUT,
                @addressLine1 NVARCHAR(255) OUTPUT,
                @addressLine2 NVARCHAR(255) OUTPUT,
                @city NVARCHAR(100) OUTPUT,
                @state NVARCHAR(50) OUTPUT,
                @zip NVARCHAR(20) OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;

                DECLARE @firstName NVARCHAR(100) = JSON_VALUE(@formData, '$.firstName');
                DECLARE @lastName NVARCHAR(100) = JSON_VALUE(@formData, '$.lastName');
                DECLARE @dateOfBirth DATE = TRY_CAST(JSON_VALUE(@formData, '$.dateOfBirth') AS DATE);

                IF EXISTS (
                    SELECT 1 FROM dbo.EligibleChildren
                    WHERE FirstName = @firstName
                      AND LastName = @lastName
                      AND DateOfBirth = @dateOfBirth
                )
                BEGIN
                    SET @isEligible = 1;
                END
                ELSE
                BEGIN
                    SET @isEligible = 0;
                END

                SET @addressLine1 = '';
                SET @addressLine2 = '';
                SET @city = '';
                SET @state = '';
                SET @zip = '';
            END
            """);
    }

    private static async Task ExecuteSql(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
