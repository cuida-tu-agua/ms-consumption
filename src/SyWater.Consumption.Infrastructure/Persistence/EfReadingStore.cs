using Microsoft.EntityFrameworkCore;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Domain.Consumption;
using SyWater.Consumption.Infrastructure.Persistence.Entities;

namespace SyWater.Consumption.Infrastructure.Persistence;

public sealed class EfReadingStore(ConsumptionDbContext db) : IReadingStore
{
    public async Task<bool> AddAsync(Reading reading, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (await db.Readings.AnyAsync(r => r.DeviceId == reading.DeviceId && r.RecordedAt == reading.RecordedAtUtc, ct))
            return false;

        db.Readings.Add(new ReadingEntity
        {
            DeviceId = reading.DeviceId,
            PlaceId = reading.PlaceId,
            FlowRateLph = reading.FlowLpm * 60m,   // the table stores liters per HOUR (data model v5)
            VolumeLiters = reading.VolumeLiters,
            CumulativeLiters = reading.TotalLiters,
            RecordedAt = reading.RecordedAtUtc,
            ReceivedAt = reading.ReceivedAtUtc,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (await IsDuplicateAsync(reading, ct)) return false;
            throw;
        }

        await AddToHourAsync(reading, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task AddToHourAsync(Reading reading, CancellationToken ct)
    {
        var hour = reading.HourStartUtc;
        var now = reading.ReceivedAtUtc;

        var updated = await IncrementAsync(reading, hour, now, ct);
        if (updated > 0) return;

        db.Hourly.Add(new HourlyEntity
        {
            PlaceId = reading.PlaceId,
            HourStart = hour,
            TotalLiters = reading.VolumeLiters,
            ReadingCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await IncrementAsync(reading, hour, now, ct) == 0) throw;
        }
    }

    private Task<int> IncrementAsync(Reading reading, DateTime hour, DateTime now, CancellationToken ct) =>
        db.Hourly
            .Where(h => h.PlaceId == reading.PlaceId && h.HourStart == hour)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.TotalLiters, h => h.TotalLiters + reading.VolumeLiters)
                .SetProperty(h => h.ReadingCount, h => h.ReadingCount + 1)
                .SetProperty(h => h.UpdatedAt, now), ct);

    private async Task<bool> IsDuplicateAsync(Reading reading, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return await db.Readings.AnyAsync(r => r.DeviceId == reading.DeviceId && r.RecordedAt == reading.RecordedAtUtc, ct);
    }
}
