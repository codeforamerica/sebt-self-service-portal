using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOptInTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserOptIns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EmailOptIn = table.Column<bool>(type: "bit", nullable: false),
                    DobOptIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOptIns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserOptIns_Email",
                table: "UserOptIns",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserOptIns");
        }
    }
}
