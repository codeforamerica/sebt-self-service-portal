using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IdProofingStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IdProofingSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdProofingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdProofingExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Email);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdProofingSessionId",
                table: "Users",
                column: "IdProofingSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
