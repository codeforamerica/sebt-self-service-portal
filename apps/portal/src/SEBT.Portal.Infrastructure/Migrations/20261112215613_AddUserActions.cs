using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CaseIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApplicationIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicationStudentId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApplicationStudentIdHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SpanId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActions_User_CreatedAt",
                table: "UserActions",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActions");
        }
    }
}
