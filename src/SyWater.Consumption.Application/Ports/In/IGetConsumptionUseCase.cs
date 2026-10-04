using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Application.Ports.In;

public interface IGetConsumptionUseCase
{
    Task<ConsumptionView> ExecuteAsync(Guid placeId, string? period, string? timeZone, CancellationToken ct);
}
