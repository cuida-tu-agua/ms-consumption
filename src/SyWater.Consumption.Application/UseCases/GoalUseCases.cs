using SyWater.Consumption.Application.Goals;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Domain.Consumption;
using SyWater.Consumption.Domain.Goals;

namespace SyWater.Consumption.Application.UseCases;

/// <summary>
/// HU-024. Order: time zone (400) → the place is mine (404, asks ms-places) → read.
/// The month is the CURRENT month in the place's time zone, the same window as the "Month" chart (HU-016).
/// </summary>
public sealed class GetGoalProgressUseCase(IPlaceAccessChecker places, IGoalStore goals, IConsumptionReader reader,
    TimeProvider clock) : IGetGoalProgressUseCase
{
    public async Task<GoalProgressView> ExecuteAsync(Guid placeId, string? timeZone, CancellationToken ct)
    {
        var zone = ConsumptionPeriods.ParseTimeZone(timeZone);
        if (!await places.CanReadAsync(placeId, ct)) throw new PlaceNotFoundException(placeId);

        var month = CurrentMonth.Of(clock, zone);
        var goal = await goals.FindAsync(placeId, month.Goal.MonthStart, ct);
        return await GoalViews.BuildAsync(placeId, zone, month, goal, reader, ct);
    }
}

/// <summary>
/// HU-023. Order: target (400) → time zone (400) → the place is mine (404) → create or edit the goal of the current month.
/// Only the owner sets the goal (INV-G003): CanReadAsync is true for the owner alone, collaborators are not included.
/// </summary>
public sealed class SetMonthlyGoalUseCase(IPlaceAccessChecker places, IGoalStore goals, IConsumptionReader reader,
    TimeProvider clock) : ISetMonthlyGoalUseCase
{
    public async Task<GoalProgressView> ExecuteAsync(Guid placeId, decimal? targetLiters, string? timeZone, CancellationToken ct)
    {
        var target = Goal.ValidTarget(targetLiters);
        var zone = ConsumptionPeriods.ParseTimeZone(timeZone);
        if (!await places.CanReadAsync(placeId, ct)) throw new PlaceNotFoundException(placeId);

        var now = clock.GetUtcNow().UtcDateTime;
        var month = CurrentMonth.Of(clock, zone);

        var goal = await goals.FindAsync(placeId, month.Goal.MonthStart, ct);
        if (goal is null)
        {
            goal = Goal.Create(placeId, month.Goal.MonthStart, target, now);
            if (!await goals.AddAsync(goal, ct))
            {
                // Two saves at the same time: the other one created it, this one edits it
                goal = await goals.FindAsync(placeId, month.Goal.MonthStart, ct)
                       ?? throw new InvalidOperationException("The goal was neither created nor found.");
                goal.ChangeTarget(target, now);
                await goals.UpdateAsync(goal, ct);
            }
        }
        else
        {
            goal.ChangeTarget(target, now);
            await goals.UpdateAsync(goal, ct);
        }

        return await GoalViews.BuildAsync(placeId, zone, month, goal, reader, ct);
    }
}

/// <summary>The current local month: its days for the goal and its UTC window for the liters.</summary>
internal sealed record CurrentMonth(GoalMonth Goal, PeriodWindow Window)
{
    public static CurrentMonth Of(TimeProvider clock, TimeZoneInfo zone)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));
        return new CurrentMonth(GoalMonth.Of(localToday), PeriodWindow.For(ConsumptionPeriod.Month, now, zone));
    }
}

internal static class GoalViews
{
    public static async Task<GoalProgressView> BuildAsync(Guid placeId, TimeZoneInfo zone, CurrentMonth month, Goal? goal,
        IConsumptionReader reader, CancellationToken ct)
    {
        var hours = await reader.GetHourlyAsync(placeId, month.Window.FromUtc, month.Window.ToUtc, ct);
        var consumed = Math.Round(hours.Sum(h => h.Liters), 3);

        var progress = goal is null ? null : GoalProgress.For(goal.TargetLiters, consumed);

        return new GoalProgressView(
            placeId,
            zone.Id,
            month.Goal.MonthStart,
            month.Goal.DaysInMonth,
            month.Goal.DaysLeft,
            consumed,
            goal is not null,
            goal?.TargetLiters,
            progress?.Percentage,
            progress is null ? null : Math.Round(progress.RemainingLiters, 3),
            progress is null ? null : StatusCode(progress.Status),
            goal?.UpdatedAtUtc);
    }

    private static string StatusCode(GoalStatus status) => status switch
    {
        GoalStatus.OnTrack => "ON_TRACK",
        GoalStatus.Warning => "WARNING",
        _ => "EXCEEDED",
    };
}
