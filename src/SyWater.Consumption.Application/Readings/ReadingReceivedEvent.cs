namespace SyWater.Consumption.Application.Readings;

/// <summary>
/// "data" of the RabbitMQ event "device.reading.received" published by device-service.
/// Each service keeps its own copy of the contract (no shared library between services).
/// </summary>
public sealed record ReadingReceivedEvent(
    Guid DeviceId,
    Guid PlaceId,
    DateTime RecordedAt,
    decimal FlowLpm,
    decimal VolumeLiters,
    decimal TotalLiters,
    DateTime ReceivedAt)
{
    public const string EventType = "device.reading.received";
}

/// <summary>HU-015..018: what the panel shows for one place and one period.</summary>
public sealed record ConsumptionView(
    Guid PlaceId,
    string Period,
    string TimeZone,
    DateTime From,
    DateTime To,
    decimal TotalLiters,
    bool HasData,
    IReadOnlyList<BucketView> Buckets,
    DateTime? LastReadingAt,
    decimal? CurrentFlowLpm,
    DateTime GeneratedAt);

public sealed record BucketView(DateTime Start, decimal Liters);
