using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentCheckSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentCheckSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChildrenChecked = table.Column<int>(type: "int", nullable: false),
                    IpAddressHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentCheckSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeidentifiedChildResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BirthYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EligibilityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SchoolName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeidentifiedChildResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeidentifiedChildResults_EnrollmentCheckSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "EnrollmentCheckSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeidentifiedChildResults_SubmissionId",
                table: "DeidentifiedChildResults",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeidentifiedChildResults");

            migrationBuilder.DropTable(
                name: "EnrollmentCheckSubmissions");
        }
    }
}
