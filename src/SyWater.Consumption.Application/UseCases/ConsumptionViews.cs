using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.UseCases;

/// <summary>The consumption of a place, once the caller has been allowed to see it (shared by the user and the guest).</summary>
internal static class ConsumptionViews
{
    public static async Task<ConsumptionView> BuildAsync(
        Guid placeId, ConsumptionPeriod period, TimeZoneInfo zone, IConsumptionReader reader, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var window = PeriodWindow.For(period, now, zone);

        var hours = await reader.GetHourlyAsync(placeId, window.FromUtc, window.ToUtc, ct);
        var latest = await reader.GetLatestAsync(placeId, ct);
        var summary = ConsumptionSummary.From(window, hours);

        return new ConsumptionView(
            placeId,
            period.ToString().ToUpperInvariant(),
            zone.Id,
            window.FromUtc,
            window.ToUtc,
            Math.Round(summary.TotalLiters, 3),
            summary.HasData,
            summary.Buckets.Select(b => new BucketView(b.StartUtc, Math.Round(b.Liters, 3))).ToList(),
            latest?.RecordedAtUtc,
            CurrentFlow.From(latest?.RecordedAtUtc, latest?.FlowLpm, now),
            now);
    }
}
