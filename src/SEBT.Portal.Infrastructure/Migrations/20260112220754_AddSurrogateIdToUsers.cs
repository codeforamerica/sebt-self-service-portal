using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurrogateIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create a new table with the Id column and IDENTITY
            migrationBuilder.CreateTable(
                name: "Users_temp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IdProofingStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IdProofingSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdProofingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdProofingExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCoLoaded = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CoLoadedLastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users_temp", x => x.Id);
                });

            // Step 2: Copy existing data to the new table
            migrationBuilder.Sql(@"
                INSERT INTO Users_temp (Email, IdProofingStatus, IdProofingSessionId, IdProofingCompletedAt, IdProofingExpiresAt, IsCoLoaded, CoLoadedLastUpdated, CreatedAt, UpdatedAt)
                SELECT 
                    Email,
                    IdProofingStatus,
                    IdProofingSessionId,
                    IdProofingCompletedAt,
                    IdProofingExpiresAt,
                    IsCoLoaded,
                    CoLoadedLastUpdated,
                    CreatedAt,
                    UpdatedAt
                FROM Users;
            ");

            // Step 3: Drop the old table
            migrationBuilder.DropTable(name: "Users");

            // Step 4: Rename the new table to Users
            migrationBuilder.RenameTable(
                name: "Users_temp",
                newName: "Users");

            // Step 4a: Rename the primary key constraint to the expected name
            migrationBuilder.Sql(@"
                EXEC sp_rename 'PK_Users_temp', 'PK_Users', 'OBJECT';
            ");

            // Step 5: Recreate the index on session ID
            migrationBuilder.CreateIndex(
                name: "IX_Users_IdProofingSessionId",
                table: "Users",
                column: "IdProofingSessionId");

            // Step 6: Create unique index on Email
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdProofingSessionId",
                table: "Users");

            // Step 2: Create temporary table with Email as primary key
            migrationBuilder.CreateTable(
                name: "Users_temp",
                columns: table => new
                {
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IdProofingStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IdProofingSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdProofingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdProofingExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCoLoaded = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CoLoadedLastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users_temp", x => x.Email);
                });

            // Step 3: Copy data back (excluding Id)
            migrationBuilder.Sql(@"
                INSERT INTO Users_temp (Email, IdProofingStatus, IdProofingSessionId, IdProofingCompletedAt, IdProofingExpiresAt, IsCoLoaded, CoLoadedLastUpdated, CreatedAt, UpdatedAt)
                SELECT Email, IdProofingStatus, IdProofingSessionId, IdProofingCompletedAt, IdProofingExpiresAt, IsCoLoaded, CoLoadedLastUpdated, CreatedAt, UpdatedAt
                FROM Users;
            ");

            // Step 4: Drop old table
            migrationBuilder.DropTable(name: "Users");

            // Step 5: Rename temp table
            migrationBuilder.RenameTable(
                name: "Users_temp",
                newName: "Users");

            // Step 5a: Rename the primary key constraint to the expected name
            migrationBuilder.Sql(@"
                EXEC sp_rename 'PK_Users_temp', 'PK_Users', 'OBJECT';
            ");

            // Step 6: Recreate session ID index
            migrationBuilder.CreateIndex(
                name: "IX_Users_IdProofingSessionId",
                table: "Users",
                column: "IdProofingSessionId");
        }
    }
}
