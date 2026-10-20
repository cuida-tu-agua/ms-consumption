using SyWater.Consumption.Application.Goals;

namespace SyWater.Consumption.Application.Ports.In;

/// <summary>HU-024: the goal of the current month and how much of it is used.</summary>
public interface IGetGoalProgressUseCase
{
    Task<GoalProgressView> ExecuteAsync(Guid placeId, string? timeZone, CancellationToken ct);
}

/// <summary>HU-023: sets (or edits) the goal of the current month.</summary>
public interface ISetMonthlyGoalUseCase
{
    Task<GoalProgressView> ExecuteAsync(Guid placeId, decimal? targetLiters, string? timeZone, CancellationToken ct);
}
