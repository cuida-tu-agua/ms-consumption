using Microsoft.EntityFrameworkCore;
using SyWater.Consumption.Infrastructure.Persistence.Entities;

namespace SyWater.Consumption.Infrastructure.Persistence;

public class ConsumptionDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ReadingEntity> Readings => Set<ReadingEntity>();
    public DbSet<HourlyEntity> Hourly => Set<HourlyEntity>();
    public DbSet<GoalEntity> Goals => Set<GoalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReadingEntity>().Property(r => r.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        modelBuilder.Entity<ReadingEntity>().HasIndex(r => new { r.DeviceId, r.RecordedAt }).IsUnique();   // UQ_reading_dev_time

        modelBuilder.Entity<HourlyEntity>().Property(h => h.Id).HasDefaultValueSql("NEWID()");
        modelBuilder.Entity<HourlyEntity>().HasIndex(h => new { h.PlaceId, h.HourStart }).IsUnique();       // UQ_hourly_place_hour

        modelBuilder.Entity<GoalEntity>().Property(g => g.Id).HasDefaultValueSql("NEWID()");
        modelBuilder.Entity<GoalEntity>().HasIndex(g => new { g.PlaceId, g.MonthStart }).IsUnique();         // UQ_goal_place_month
    }
}
