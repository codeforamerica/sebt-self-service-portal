using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAndHouseholdIdentifierTrackingToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalProviderId",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "ExternalProviderId",
                table: "Users",
                newName: "IdentityProviderUserId");

            migrationBuilder.AddColumn<string>(
                name: "HouseholdIdentifierHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HouseholdIdentifierType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HouseholdIdentifierUpdatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastLoginMethod",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoginCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Users rows are only created during login, so every existing row has logged in at least once.
            migrationBuilder.Sql("UPDATE [Users] SET [LoginCount] = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_HouseholdIdentifierHash",
                table: "Users",
                column: "HouseholdIdentifierHash",
                filter: "[HouseholdIdentifierHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdentityProviderUserId",
                table: "Users",
                column: "IdentityProviderUserId",
                unique: true,
                filter: "[IdentityProviderUserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_HouseholdIdentifierHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdentityProviderUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HouseholdIdentifierHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HouseholdIdentifierType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HouseholdIdentifierUpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginMethod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginCount",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "IdentityProviderUserId",
                table: "Users",
                newName: "ExternalProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalProviderId",
                table: "Users",
                column: "ExternalProviderId",
                unique: true,
                filter: "[ExternalProviderId] IS NOT NULL");
        }
    }
}
