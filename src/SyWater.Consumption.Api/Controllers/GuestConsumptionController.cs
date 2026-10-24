using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Api.Controllers;

/// <summary>
/// HU-040 / HU-041 · The water of a place for a guest. No login: the token of the guest link is the credential.
/// There is only a GET here, so a guest cannot change anything.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/guest/{token}/consumption")]
public sealed class GuestConsumptionController : ControllerBase
{
    [HttpGet]
    public Task<ConsumptionView> Get(
        string token,
        [FromQuery] string? period,
        [FromQuery] string? tz,
        [FromServices] IGetGuestConsumptionUseCase useCase,
        CancellationToken ct) =>
        useCase.ExecuteAsync(token, period, tz, ct);
}
