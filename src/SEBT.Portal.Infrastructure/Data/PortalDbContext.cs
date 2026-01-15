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
    /// Household records with application and benefit information.
    /// </summary>
    public DbSet<HouseholdEntity> Households { get; set; }

    /// <summary>
    /// Child records associated with households.
    /// </summary>
    public DbSet<ChildEntity> Children { get; set; }

    /// <summary>
    /// Address records associated with households.
    /// </summary>
    public DbSet<AddressEntity> Addresses { get; set; }

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
        });

        modelBuilder.Entity<HouseholdEntity>(entity =>
        {
            entity.ToTable("Households");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Phone)
                .HasMaxLength(50);
            entity.Property(e => e.Last4DigitsOfCard)
                .HasMaxLength(4);
            entity.Property(e => e.ApplicationNumber)
                .HasMaxLength(100);
            entity.Property(e => e.CaseNumber)
                .HasMaxLength(100);
            entity.Property(e => e.ApplicationStatus)
                .IsRequired()
                .HasDefaultValue(0); // 0 = Unknown
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            // Create unique index on email for faster lookups
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Households_Email");

            // Configure one-to-many relationship with children
            entity.HasMany(h => h.Children)
                .WithOne(c => c.Household)
                .HasForeignKey(c => c.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure one-to-one relationship with address
            entity.HasOne(h => h.Address)
                .WithOne(a => a.Household)
                .HasForeignKey<AddressEntity>(a => a.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChildEntity>(entity =>
        {
            entity.ToTable("Children");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
            entity.Property(e => e.HouseholdId)
                .IsRequired();
            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.HouseholdId)
                .HasDatabaseName("IX_Children_HouseholdId");
        });

        modelBuilder.Entity<AddressEntity>(entity =>
        {
            entity.ToTable("Addresses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
            entity.Property(e => e.HouseholdId)
                .IsRequired();
            entity.Property(e => e.StreetAddress1)
                .HasMaxLength(255);
            entity.Property(e => e.StreetAddress2)
                .HasMaxLength(255);
            entity.Property(e => e.City)
                .HasMaxLength(100);
            entity.Property(e => e.State)
                .HasMaxLength(50);
            entity.Property(e => e.PostalCode)
                .HasMaxLength(20);

            entity.HasIndex(e => e.HouseholdId)
                .IsUnique()
                .HasDatabaseName("IX_Addresses_HouseholdId");
        });
    }
}
