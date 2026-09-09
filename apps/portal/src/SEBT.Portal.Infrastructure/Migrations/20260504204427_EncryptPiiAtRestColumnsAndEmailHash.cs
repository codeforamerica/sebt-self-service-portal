using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEBT.Portal.Infrastructure.Migrations;

/// <inheritdoc />
public partial class EncryptPiiAtRestColumnsAndEmailHash : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Users_Email",
            table: "Users");

        migrationBuilder.AlterColumn<string>(
            name: "TanfId",
            table: "Users",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "SnapId",
            table: "Users",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Phone",
            table: "Users",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Users",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(255)",
            oldMaxLength: 255,
            oldNullable: true);

        MoveUsersDateOfBirthFromDateTypeToNvarchar(migrationBuilder);

        migrationBuilder.AddColumn<string>(
            name: "EmailHash",
            table: "Users",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ProofingIdValue",
            table: "DocVerificationChallenges",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(255)",
            oldMaxLength: 255,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ProofingIdType",
            table: "DocVerificationChallenges",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "ProofingDateOfBirth",
            table: "DocVerificationChallenges",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_EmailHash",
            table: "Users",
            column: "EmailHash",
            unique: true,
            filter: "[EmailHash] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Reverting EncryptPiiAtRestColumnsAndEmailHash drops ciphertext columns and would lose data — not supported.");

    /// <summary>
    /// SQL Server cannot always widen <c>date</c> → ciphertext <c>nvarchar</c> in one ALTER; copy via temp column (idempotent).
    /// </summary>
    private static void MoveUsersDateOfBirthFromDateTypeToNvarchar(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'dbo.Users', N'DateOfBirth') IS NOT NULL
            BEGIN
                DECLARE @dtype sysname =
                    (SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                     WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'Users' AND COLUMN_NAME = N'DateOfBirth');
                IF @dtype IN (N'date', N'datetime2', N'datetime')
                BEGIN
                    IF COL_LENGTH(N'dbo.Users', N'__pii_legacy_dob_nvarchar') IS NULL
                        ALTER TABLE [dbo].[Users] ADD [__pii_legacy_dob_nvarchar] nvarchar(512) NULL;
                    EXEC(N'
                        UPDATE [dbo].[Users]
                        SET [__pii_legacy_dob_nvarchar] = CONVERT(char(10), [DateOfBirth], 126)
                        WHERE [DateOfBirth] IS NOT NULL
                    ');
                    IF COL_LENGTH(N'dbo.Users', N'DateOfBirth') IS NOT NULL
                        ALTER TABLE [dbo].[Users] DROP COLUMN [DateOfBirth];
                    IF COL_LENGTH(N'dbo.Users', N'__pii_legacy_dob_nvarchar') IS NOT NULL
                       AND COL_LENGTH(N'dbo.Users', N'DateOfBirth') IS NULL
                        EXEC sp_rename N'dbo.Users.__pii_legacy_dob_nvarchar', N'DateOfBirth', N'COLUMN';
                END
                ELSE IF @dtype IS NOT NULL AND @dtype NOT IN (N'nvarchar', N'varchar', N'nchar', N'char')
                BEGIN
                    THROW 50001, 'Unexpected Users.DateOfBirth type during PII encryption migration.', 1;
                END
            END
            """);
    }
}
