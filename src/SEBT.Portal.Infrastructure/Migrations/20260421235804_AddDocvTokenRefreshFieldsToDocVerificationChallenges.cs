using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocvTokenRefreshFieldsToDocVerificationChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DocvTokenIssuedAt",
                table: "DocVerificationChallenges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofingDateOfBirth",
                table: "DocVerificationChallenges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofingIdType",
                table: "DocVerificationChallenges",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofingIdValue",
                table: "DocVerificationChallenges",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocvTokenIssuedAt",
                table: "DocVerificationChallenges");

            migrationBuilder.DropColumn(
                name: "ProofingDateOfBirth",
                table: "DocVerificationChallenges");

            migrationBuilder.DropColumn(
                name: "ProofingIdType",
                table: "DocVerificationChallenges");

            migrationBuilder.DropColumn(
                name: "ProofingIdValue",
                table: "DocVerificationChallenges");
        }
    }
}
