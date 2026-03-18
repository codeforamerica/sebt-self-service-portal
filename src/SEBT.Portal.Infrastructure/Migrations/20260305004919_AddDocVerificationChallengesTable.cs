using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocVerificationChallengesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SocureReferenceId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EvalId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SocureEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DocvTransactionToken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DocvUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OffboardingReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AllowIdRetry = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocVerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocVerificationChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_EvalId",
                table: "DocVerificationChallenges",
                column: "EvalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_PublicId",
                table: "DocVerificationChallenges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_SocureReferenceId",
                table: "DocVerificationChallenges",
                column: "SocureReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_UserId",
                table: "DocVerificationChallenges",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocVerificationChallenges");
        }
    }
}
