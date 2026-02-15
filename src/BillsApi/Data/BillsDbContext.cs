using BillsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BillsApi.Data;

/// <summary>
/// Database context for the Bills API.
/// </summary>
public class BillsDbContext : DbContext
{
    public BillsDbContext(DbContextOptions<BillsDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// The Bills table.
    /// </summary>
    public DbSet<Bill> Bills => Set<Bill>();

    /// <summary>
    /// The Users table.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// The RefreshTokens table.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    
    /// <summary>
    /// The PasswordResetTokens table.
    /// </summary>
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>
    /// The Households table.
    /// </summary>
    public DbSet<Household> Households => Set<Household>();

    /// <summary>
    /// The HouseholdInvitations table.
    /// </summary>
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.HouseholdId); // Index for household queries
            entity.Property(e => e.BillName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountOverMinimum).HasColumnType("decimal(18,2)");
            
            // Relationship with Household
            entity.HasOne(e => e.Household)
                  .WithMany(h => h.Bills)
                  .HasForeignKey(e => e.HouseholdId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasIndex(e => e.HouseholdId); // Index for household queries

            // Relationship with Household
            entity.HasOne(e => e.Household)
                  .WithMany(h => h.Members)
                  .HasForeignKey(e => e.HouseholdId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.Used).HasDefaultValue(false);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HouseholdInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.InvitedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.Accepted).HasDefaultValue(false);
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.HouseholdId);
            
            entity.HasOne(e => e.Household)
                  .WithMany()
                  .HasForeignKey(e => e.HouseholdId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.InvitedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.InvitedByUserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
