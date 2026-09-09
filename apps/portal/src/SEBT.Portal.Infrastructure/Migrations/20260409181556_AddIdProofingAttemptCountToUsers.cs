using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdProofingAttemptCountToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdProofingAttemptCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdProofingAttemptCount",
                table: "Users");
        }
    }
}
