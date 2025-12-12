using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserOptInEntity>(entity =>
        {
            entity.ToTable("UserOptIns");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EmailOptIn).IsRequired();
            entity.Property(e => e.DobOptIn).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }
}
