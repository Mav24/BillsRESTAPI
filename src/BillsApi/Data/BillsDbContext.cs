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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(e => e.UserId); // Index for faster queries by user
            entity.Property(e => e.BillName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AmountOverMinimum).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
        });
    }
}
