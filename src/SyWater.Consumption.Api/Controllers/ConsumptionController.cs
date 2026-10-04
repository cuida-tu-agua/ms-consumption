using Microsoft.AspNetCore.Mvc;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Api.Controllers;

[ApiController]
[Route("api/places/{placeId:guid}/consumption")]
public sealed class ConsumptionController : ControllerBase
{
    [HttpGet]
    public Task<ConsumptionView> Get(
        Guid placeId,
        [FromQuery] string? period,
        [FromQuery] string? tz,
        [FromServices] IGetConsumptionUseCase useCase,
        CancellationToken ct) =>
        useCase.ExecuteAsync(placeId, period, tz, ct);
}
