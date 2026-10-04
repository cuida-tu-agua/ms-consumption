using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.Ports.Out;

public interface IReadingStore
{
    Task<bool> AddAsync(Reading reading, CancellationToken ct);
}

public interface IConsumptionReader
{
    Task<IReadOnlyList<HourlyTotal>> GetHourlyAsync(Guid placeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    Task<LatestReading?> GetLatestAsync(Guid placeId, CancellationToken ct);
}

public sealed record LatestReading(DateTime RecordedAtUtc, decimal FlowLpm);

public interface IPlaceAccessChecker
{
    /// <exception cref="ExternalServiceUnavailableException">place-service did not answer.</exception>
    Task<bool> CanReadAsync(Guid placeId, CancellationToken ct);
}

public sealed class ExternalServiceUnavailableException(string service, Exception? inner = null)
    : Exception($"The service '{service}' is not available right now.", inner)
{
    public string Service { get; } = service;
}
