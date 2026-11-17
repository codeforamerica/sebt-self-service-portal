using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CaseIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApplicationIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicationStudentId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApplicationStudentIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCases_User_CaseIdentity",
                table: "UserCases",
                columns: new[] { "UserId", "CaseIdHash", "ApplicationIdHash", "ApplicationStudentIdHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCases");
        }
    }
}
