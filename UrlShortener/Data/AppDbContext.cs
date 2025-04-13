// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using UrlShortener.Models;

namespace UrlShortener.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<UrlInfo> InfURL { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UrlInfo>()
                .ToTable("InfURL");

            modelBuilder.Entity<UrlInfo>()
                .HasIndex(u => u.NewUrl)
                .IsUnique();

            modelBuilder.Entity<UrlInfo>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<UrlInfo>()
                .Property(u => u.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<UrlInfo>()
                .Property(u => u.ClickCount)
                .HasDefaultValue(0);
        }
    }
}