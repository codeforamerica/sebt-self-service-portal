using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOneActiveChallengePerUserIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One active (Created/Pending) challenge per user.
            // Filtered unique index on UserId only — must be raw SQL because
            // EF Core merges HasIndex calls on the same column as the FK index.
            migrationBuilder.Sql("""
                CREATE UNIQUE NONCLUSTERED INDEX [IX_DocVerificationChallenges_OneActivePerUser]
                ON [DocVerificationChallenges] ([UserId])
                WHERE [Status] IN (0, 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocVerificationChallenges_OneActivePerUser",
                table: "DocVerificationChallenges");
        }
    }
}
