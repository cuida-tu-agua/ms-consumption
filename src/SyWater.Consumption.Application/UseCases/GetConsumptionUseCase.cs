using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.UseCases;

/// <summary>
/// HU-015/016/017/018. Order: validate the query (400) → the place is mine (404, asks ms-places)
/// → read. Someone else's place gets the same 404 as a place that does not exist.
/// </summary>
public sealed class GetConsumptionUseCase(IPlaceAccessChecker places, IConsumptionReader reader, TimeProvider clock)
    : IGetConsumptionUseCase
{
    public async Task<ConsumptionView> ExecuteAsync(Guid placeId, string? period, string? timeZone, CancellationToken ct)
    {
        var parsedPeriod = ConsumptionPeriods.Parse(period);
        var zone = ConsumptionPeriods.ParseTimeZone(timeZone);

        if (!await places.CanReadAsync(placeId, ct)) throw new PlaceNotFoundException(placeId);

        var now = clock.GetUtcNow().UtcDateTime;
        var window = PeriodWindow.For(parsedPeriod, now, zone);

        var hours = await reader.GetHourlyAsync(placeId, window.FromUtc, window.ToUtc, ct);
        var latest = await reader.GetLatestAsync(placeId, ct);
        var summary = ConsumptionSummary.From(window, hours);

        return new ConsumptionView(
            placeId,
            parsedPeriod.ToString().ToUpperInvariant(),
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
