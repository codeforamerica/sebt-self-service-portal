using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Data;

/// <summary>
/// Database context for the SEBT Portal application.
/// </summary>
public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// User opt-in records stored in the database.
    /// </summary>
    public DbSet<UserOptInEntity> UserOptIns { get; set; }

    /// <summary>
    /// User records with ID proofing status stored in the database.
    /// </summary>
    public DbSet<UserEntity> Users { get; set; }

    /// <summary>
    /// Document verification challenge records tracking individual verification attempts.
    /// </summary>
    public DbSet<DocVerificationChallengeEntity> DocVerificationChallenges { get; set; }

    /// <summary>
    /// De-identified enrollment check submission records for analytics.
    /// </summary>
    public DbSet<EnrollmentCheckSubmissionEntity> EnrollmentCheckSubmissions { get; set; }

    /// <summary>
    /// De-identified child result records associated with enrollment check submissions.
    /// </summary>
    public DbSet<DeidentifiedChildResultEntity> DeidentifiedChildResults { get; set; }

    /// <summary>
    /// Card replacement request records for cooldown enforcement.
    /// </summary>
    public DbSet<CardReplacementRequestEntity> CardReplacementRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserOptInEntity>(entity =>
        {
            entity.ToTable("UserOptIns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EmailOptIn)
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
            entity.Property(e => e.DobOptIn)
                .IsRequired()
                .UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Email)
                .HasMaxLength(255);
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email")
                .HasFilter("[Email] IS NOT NULL");
            entity.Property(e => e.IdProofingStatus)
                .IsRequired()
                .HasDefaultValue(0); // 0 = NotStarted
            entity.Property(e => e.IalLevel)
                .IsRequired()
                .HasDefaultValue(0); // 0 = UserIalLevel.None
            entity.Property(e => e.IdProofingSessionId)
                .HasMaxLength(255);
            entity.Property(e => e.IsCoLoaded)
                .IsRequired()
                .HasDefaultValue(false);
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            // Create index on session ID for faster lookups
            entity.HasIndex(e => e.IdProofingSessionId)
                .HasDatabaseName("IX_Users_IdProofingSessionId");

            // Household identifier fields
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.Property(e => e.SnapId).HasMaxLength(64);
            entity.Property(e => e.TanfId).HasMaxLength(64);
            entity.Property(e => e.Ssn).HasMaxLength(64);

            // OIDC external provider identifier — nullable, filtered unique index
            entity.Property(e => e.ExternalProviderId)
                .HasMaxLength(255);
            entity.HasIndex(e => e.ExternalProviderId)
                .IsUnique()
                .HasDatabaseName("IX_Users_ExternalProviderId")
                .HasFilter("[ExternalProviderId] IS NOT NULL");

            // The DF_Users_IdProofingAttemptCount default constraint has existed in the DB
            // since migration 20260409181556_AddIdProofingAttemptCountToUsers created the
            // column with defaultValue: 0. That prior migration never added the matching
            // .HasDefaultValue(0) here, so EF's model snapshot has drifted from the actual
            // schema. Declaring it here re-aligns the snapshot with the DB reality and
            // ensures future migrations won't generate spurious "remove default" deltas.
            entity.Property(e => e.IdProofingAttemptCount)
                .IsRequired()
                .HasDefaultValue(0);
        });

        modelBuilder.Entity<DocVerificationChallengeEntity>(entity =>
        {
            entity.ToTable("DocVerificationChallenges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            // Opaque public ID for API consumers
            entity.Property(e => e.PublicId)
                .IsRequired();
            entity.HasIndex(e => e.PublicId)
                .IsUnique()
                .HasDatabaseName("IX_DocVerificationChallenges_PublicId");

            // User foreign key
            entity.Property(e => e.UserId)
                .IsRequired();
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_DocVerificationChallenges_UserId");
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasDefaultValue(0); // Created

            // Socure correlation fields
            entity.Property(e => e.SocureReferenceId)
                .HasMaxLength(255);
            entity.HasIndex(e => e.SocureReferenceId)
                .HasDatabaseName("IX_DocVerificationChallenges_SocureReferenceId");

            entity.Property(e => e.EvalId)
                .HasMaxLength(255);
            entity.HasIndex(e => e.EvalId)
                .HasDatabaseName("IX_DocVerificationChallenges_EvalId");

            entity.Property(e => e.SocureEventId)
                .HasMaxLength(255);

            entity.Property(e => e.DocvTransactionToken)
                .HasMaxLength(255);
            entity.Property(e => e.DocvUrl)
                .HasMaxLength(1024);
            entity.Property(e => e.OffboardingReason)
                .HasMaxLength(255);

            entity.Property(e => e.ProofingDateOfBirth)
                .HasMaxLength(32);
            entity.Property(e => e.ProofingIdType)
                .HasMaxLength(64);
            entity.Property(e => e.ProofingIdValue)
                .HasMaxLength(255);

            entity.Property(e => e.DocvTokenIssuedAt)
                .HasColumnType("datetime2");

            entity.Property(e => e.AllowIdRetry)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            // Optimistic concurrency token
            entity.Property(e => e.RowVersion)
                .IsRowVersion();

            // One active challenge per user — enforced by a filtered unique
            // index on UserId WHERE Status IN (0, 1). Managed via raw SQL migration
            // because EF merges HasIndex calls on the same column.
        });

        modelBuilder.Entity<EnrollmentCheckSubmissionEntity>(entity =>
        {
            entity.ToTable("EnrollmentCheckSubmissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CheckedAtUtc).IsRequired();
            entity.Property(e => e.IpAddressHash).HasMaxLength(128);
            entity.HasMany(e => e.ChildResults)
                .WithOne(e => e.Submission)
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeidentifiedChildResultEntity>(entity =>
        {
            entity.ToTable("DeidentifiedChildResults");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EligibilityType).HasMaxLength(50);
            entity.Property(e => e.SchoolName).HasMaxLength(255);
        });

        modelBuilder.Entity<CardReplacementRequestEntity>(entity =>
        {
            entity.ToTable("CardReplacementRequests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.Property(e => e.HouseholdIdentifierHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.CaseIdHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.RequestedAt)
                .IsRequired();

            entity.Property(e => e.RequestedByUserId)
                .IsRequired();

            entity.HasOne(e => e.RequestedByUser)
                .WithMany()
                .HasForeignKey(e => e.RequestedByUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite index covering the cooldown lookup query:
            // WHERE HouseholdIdentifierHash = @hash AND CaseIdHash = @hash AND RequestedAt > @cutoff
            entity.HasIndex(e => new { e.HouseholdIdentifierHash, e.CaseIdHash, e.RequestedAt })
                .HasDatabaseName("IX_CardReplacementRequests_Household_Case_RequestedAt");
        });
    }
}
