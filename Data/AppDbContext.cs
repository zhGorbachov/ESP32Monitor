using ESP32Monitor.Models;
using Microsoft.EntityFrameworkCore;

namespace ESP32Monitor.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ParameterLog> ParameterLogs => Set<ParameterLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ParameterLog>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.ParameterName).HasMaxLength(64).IsRequired();
            e.Property(p => p.Source).HasMaxLength(16).IsRequired();
            e.HasIndex(p => p.Timestamp);
        });
    }
}
