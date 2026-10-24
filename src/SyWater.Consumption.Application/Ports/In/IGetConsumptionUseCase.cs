using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Application.Ports.In;

public interface IGetConsumptionUseCase
{
    Task<ConsumptionView> ExecuteAsync(Guid placeId, string? period, string? timeZone, CancellationToken ct);
}

/// <summary>HU-040: the water of a place for someone with its guest link (no account).</summary>
public interface IGetGuestConsumptionUseCase
{
    /// <exception cref="SyWater.Consumption.Domain.Consumption.GuestLinkNotFoundException">The link does not open anything.</exception>
    Task<ConsumptionView> ExecuteAsync(string token, string? period, string? timeZone, CancellationToken ct);
}
