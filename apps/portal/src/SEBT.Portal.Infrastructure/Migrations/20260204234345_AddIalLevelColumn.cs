using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIalLevelColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IalLevel",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate existing data: IdProofingStatus=2 (Completed) had IAL1plus; others had None
            migrationBuilder.Sql(@"
                UPDATE Users SET IalLevel = CASE WHEN IdProofingStatus = 2 THEN 2 ELSE 0 END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IalLevel",
                table: "Users");
        }
    }
}
