using Microsoft.AspNetCore.Mvc;
using SyWater.Consumption.Application.Goals;
using SyWater.Consumption.Application.Ports.In;

namespace SyWater.Consumption.Api.Controllers;

/// <summary>HU-023/024: the goal of the CURRENT month of a place. Past months are never edited (INV-G004).</summary>
[ApiController]
[Route("api/places/{placeId:guid}/goals/current")]
public sealed class GoalsController : ControllerBase
{
    /// <summary>HU-024: progress of the current month (with or without a goal).</summary>
    [HttpGet]
    public Task<GoalProgressView> Get(
        Guid placeId,
        [FromQuery] string? tz,
        [FromServices] IGetGoalProgressUseCase useCase,
        CancellationToken ct) =>
        useCase.ExecuteAsync(placeId, tz, ct);

    /// <summary>HU-023: creates or edits the goal of the current month. Same answer as GET.</summary>
    [HttpPut]
    public Task<GoalProgressView> Put(
        Guid placeId,
        [FromBody] SetGoalRequest body,
        [FromQuery] string? tz,
        [FromServices] ISetMonthlyGoalUseCase useCase,
        CancellationToken ct) =>
        useCase.ExecuteAsync(placeId, body.TargetLiters, tz, ct);
}

/// <summary>The goal in liters (the app converts from m³ or gallons).</summary>
public sealed record SetGoalRequest(decimal? TargetLiters);
