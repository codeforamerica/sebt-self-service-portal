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
    /// De-identified enrollment check submission records for analytics.
    /// </summary>
    public DbSet<EnrollmentCheckSubmissionEntity> EnrollmentCheckSubmissions { get; set; }

    /// <summary>
    /// De-identified child result records associated with enrollment check submissions.
    /// </summary>
    public DbSet<DeidentifiedChildResultEntity> DeidentifiedChildResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserOptInEntity>(entity =>
        {
            entity.ToTable("UserOptIns");
            entity.HasKey(e => e.Id);
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
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .UseIdentityColumn();
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");
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
    }
}
