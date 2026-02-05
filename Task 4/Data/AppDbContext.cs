using Microsoft.EntityFrameworkCore;
using Task_4.Models;

namespace Task_4.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // DbSet properties - each represents a table in your database
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== User Configuration =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            // Create unique index on Email
            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()"); // For SQL Server

            entity.Property(u => u.IsBlocked)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(u => u.Status)
                .IsRequired()
                .HasDefaultValue(Status.Unverified);

            entity.Property(u => u.LastLoggedInAt)
                .IsRequired()
                .HasDefaultValue(null);

            entity.Property(u => u.LastActivityAt)
                .IsRequired()
                .HasDefaultValue(null);
        });
    }
}