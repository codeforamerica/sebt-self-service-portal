using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUsersDateOfBirthColumnSqlType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some environments created Users.DateOfBirth as a string column (e.g. nvarchar),
            // which causes SqlDataReader to surface values as string and EF Core's DateOnly
            // materializer to throw InvalidCastException on any query touching Users — including
            // OIDC step-up complete-login (GetUserByExternalIdAsync). Canonical type is date.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM INFORMATION_SCHEMA.COLUMNS c
                    WHERE c.TABLE_SCHEMA = N'dbo'
                      AND c.TABLE_NAME = N'Users'
                      AND c.COLUMN_NAME = N'DateOfBirth'
                      AND c.DATA_TYPE IN (N'nvarchar', N'varchar', N'nchar', N'char')
                )
                BEGIN
                    ALTER TABLE [Users] ADD [DateOfBirth__fix] [date] NULL;
                    UPDATE [Users] SET [DateOfBirth__fix] = TRY_CONVERT([date], [DateOfBirth]);
                    ALTER TABLE [Users] DROP COLUMN [DateOfBirth];
                    EXEC sp_rename N'Users.DateOfBirth__fix', N'DateOfBirth', N'COLUMN';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way repair: reversing would require knowing the prior string format.
        }
    }
}
