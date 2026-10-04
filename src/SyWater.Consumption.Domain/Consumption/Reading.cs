namespace SyWater.Consumption.Domain.Consumption;

/// <summary>
/// One reading of the flow sensor, already authenticated by device-service.
/// VolumeLiters is the water of its interval: adding them up gives the consumption.
/// </summary>
public sealed record Reading(
    Guid DeviceId, Guid PlaceId, DateTime RecordedAtUtc, decimal FlowLpm, decimal VolumeLiters, decimal TotalLiters,
    DateTime ReceivedAtUtc)
{
    public static Reading Create(Guid deviceId, Guid placeId, DateTime recordedAtUtc, decimal flowLpm, decimal volumeLiters,
        decimal totalLiters, DateTime receivedAtUtc)
    {
        if (deviceId == Guid.Empty || placeId == Guid.Empty) throw new InvalidReadingException("device and place are required");
        if (flowLpm < 0 || volumeLiters < 0 || totalLiters < 0) throw new InvalidReadingException("negative value");
        return new Reading(deviceId, placeId, DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc), flowLpm, volumeLiters,
            totalLiters, DateTime.SpecifyKind(receivedAtUtc, DateTimeKind.Utc));
    }

    /// <summary>The UTC hour this reading adds to (13:47:10 → 13:00:00).</summary>
    public DateTime HourStartUtc => new(RecordedAtUtc.Year, RecordedAtUtc.Month, RecordedAtUtc.Day, RecordedAtUtc.Hour, 0, 0,
        DateTimeKind.Utc);
}

public static class CurrentFlow
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(2);

    public static decimal? From(DateTime? lastReadingAtUtc, decimal? lastFlowLpm, DateTime nowUtc) =>
        lastReadingAtUtc is { } at && lastFlowLpm is { } flow && nowUtc - at <= FreshFor ? flow : null;
}
