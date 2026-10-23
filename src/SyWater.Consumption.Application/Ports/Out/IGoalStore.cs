using SyWater.Consumption.Domain.Goals;

namespace SyWater.Consumption.Application.Ports.Out;

/// <summary>goals.consumption_goals (HU-023/024).</summary>
public interface IGoalStore
{
    Task<Goal?> FindAsync(Guid placeId, DateOnly monthStart, CancellationToken ct);

    /// <summary>Inserts a new goal. False when that place and month already has one (another request won the race).</summary>
    Task<bool> AddAsync(Goal goal, CancellationToken ct);

    Task UpdateAsync(Goal goal, CancellationToken ct);
}
