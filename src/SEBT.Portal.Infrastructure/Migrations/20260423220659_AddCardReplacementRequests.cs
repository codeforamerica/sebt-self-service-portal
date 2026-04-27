using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCardReplacementRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardReplacementRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdIdentifierHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CaseIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardReplacementRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardReplacementRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardReplacementRequests_Household_Case_RequestedAt",
                table: "CardReplacementRequests",
                columns: new[] { "HouseholdIdentifierHash", "CaseIdHash", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CardReplacementRequests_RequestedByUserId",
                table: "CardReplacementRequests",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardReplacementRequests");
        }
    }
}
