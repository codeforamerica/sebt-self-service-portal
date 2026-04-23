using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertIntPksToGuids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DESTRUCTIVE: drops all rows in Users, UserOptIns, and DocVerificationChallenges.

            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_DocVerificationChallenges_OneActivePerUser] ON [DocVerificationChallenges];");

            migrationBuilder.DropTable(name: "DocVerificationChallenges");
            migrationBuilder.DropTable(name: "UserOptIns");
            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExternalProviderId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdProofingStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IalLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IdProofingSessionId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdProofingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdProofingExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCoLoaded = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CoLoadedLastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SnapId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TanfId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Ssn = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IdProofingAttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", col => col.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalProviderId",
                table: "Users",
                column: "ExternalProviderId",
                unique: true,
                filter: "[ExternalProviderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdProofingSessionId",
                table: "Users",
                column: "IdProofingSessionId");

            migrationBuilder.CreateTable(
                name: "UserOptIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EmailOptIn = table.Column<bool>(type: "bit", nullable: false),
                    DobOptIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOptIns", col => col.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserOptIns_Email",
                table: "UserOptIns",
                column: "Email",
                unique: true);

            migrationBuilder.CreateTable(
                name: "DocVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProofingDateOfBirth = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProofingIdType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProofingIdValue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DocvTokenIssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocVerificationChallenges", col => col.Id);
                    table.ForeignKey(
                        name: "FK_DocVerificationChallenges_Users_UserId",
                        column: col => col.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_PublicId",
                table: "DocVerificationChallenges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_UserId",
                table: "DocVerificationChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_SocureReferenceId",
                table: "DocVerificationChallenges",
                column: "SocureReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_DocVerificationChallenges_EvalId",
                table: "DocVerificationChallenges",
                column: "EvalId");

            // Filtered unique index: one active (Created or Pending) challenge per user.
            // Matches the existing pattern from 20260315214754_AddOneActiveChallengePerUserIndex.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX [IX_DocVerificationChallenges_OneActivePerUser]
                ON [DocVerificationChallenges] ([UserId])
                WHERE [Status] IN (0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback is not supported. Up() drops and recreates Users, UserOptIns, and
            // DocVerificationChallenges to change their int IDENTITY primary keys to
            // uniqueidentifier — there is no mechanical path back to the pre-migration
            // schema that also restores data.
            //
            // If you need to revert, restore from a database backup taken before this
            // migration was applied.
            throw new NotSupportedException(
                "Rolling back migration 20260422175858_ConvertIntPksToGuids is not supported. " +
                "Up() is destructive — restore from a database backup taken before the " +
                "migration was applied if you need to revert.");
        }
    }
}
