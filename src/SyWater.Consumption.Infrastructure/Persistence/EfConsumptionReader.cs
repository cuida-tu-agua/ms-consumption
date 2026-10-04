using Microsoft.EntityFrameworkCore;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Infrastructure.Persistence;

public sealed class EfConsumptionReader(ConsumptionDbContext db) : IConsumptionReader
{
    public async Task<IReadOnlyList<HourlyTotal>> GetHourlyAsync(Guid placeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await db.Hourly.AsNoTracking()
            .Where(h => h.PlaceId == placeId && h.HourStart >= fromUtc && h.HourStart < toUtc)
            .Select(h => new { h.HourStart, h.TotalLiters, h.ReadingCount })
            .ToListAsync(ct);

        return rows.Select(r => new HourlyTotal(AsUtc(r.HourStart), r.TotalLiters, r.ReadingCount)).ToList();
    }

    public async Task<LatestReading?> GetLatestAsync(Guid placeId, CancellationToken ct)
    {
        var last = await db.Readings.AsNoTracking()
            .Where(r => r.PlaceId == placeId)
            .OrderByDescending(r => r.RecordedAt)
            .Select(r => new { r.RecordedAt, r.FlowRateLph })
            .FirstOrDefaultAsync(ct);

        return last is null ? null : new LatestReading(AsUtc(last.RecordedAt), Math.Round(last.FlowRateLph / 60m, 2));
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
