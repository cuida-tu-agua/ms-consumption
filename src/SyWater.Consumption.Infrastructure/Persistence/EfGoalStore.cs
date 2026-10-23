using Microsoft.EntityFrameworkCore;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Domain.Goals;
using SyWater.Consumption.Infrastructure.Persistence.Entities;

namespace SyWater.Consumption.Infrastructure.Persistence;

public sealed class EfGoalStore(ConsumptionDbContext db) : IGoalStore
{
    public async Task<Goal?> FindAsync(Guid placeId, DateOnly monthStart, CancellationToken ct)
    {
        var row = await db.Goals.AsNoTracking()
            .FirstOrDefaultAsync(g => g.PlaceId == placeId && g.MonthStart == monthStart, ct);
        return row is null
            ? null
            : Goal.Restore(row.PlaceId, row.MonthStart, row.TargetLiters, ToSource(row.Source), row.CreatedAt, row.UpdatedAt);
    }

    public async Task<bool> AddAsync(Goal goal, CancellationToken ct)
    {
        var row = new GoalEntity
        {
            PlaceId = goal.PlaceId,
            MonthStart = goal.MonthStart,
            TargetLiters = goal.TargetLiters,
            Source = ToColumn(goal.Source),
            CreatedAt = goal.CreatedAtUtc,
            UpdatedAt = goal.UpdatedAtUtc,
        };
        db.Goals.Add(row);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.Entry(row).State = EntityState.Detached;
            // UQ_goal_place_month: someone saved the same place and month first
            if (await db.Goals.AnyAsync(g => g.PlaceId == goal.PlaceId && g.MonthStart == goal.MonthStart, ct)) return false;
            throw;
        }
    }

    public async Task UpdateAsync(Goal goal, CancellationToken ct)
    {
        var updated = await db.Goals
            .Where(g => g.PlaceId == goal.PlaceId && g.MonthStart == goal.MonthStart)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.TargetLiters, goal.TargetLiters)
                .SetProperty(g => g.UpdatedAt, goal.UpdatedAtUtc), ct);

        if (updated == 0) throw new InvalidOperationException($"The goal of {goal.PlaceId} for {goal.MonthStart} does not exist.");
    }

    private static string ToColumn(GoalSource source) => source switch
    {
        GoalSource.Suggested => "SUGGESTED",
        GoalSource.CarriedOver => "CARRIED_OVER",
        _ => "MANUAL",
    };

    private static GoalSource ToSource(string column) => column switch
    {
        "SUGGESTED" => GoalSource.Suggested,
        "CARRIED_OVER" => GoalSource.CarriedOver,
        _ => GoalSource.Manual,
    };
}
