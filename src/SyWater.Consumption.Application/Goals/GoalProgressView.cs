namespace SyWater.Consumption.Application.Goals;

/// <summary>
/// HU-023/024: what the goal widget shows for one place in the current local month.
/// Without a goal (HasGoal = false) the goal fields are null and the app invites the user to create one;
/// the liters consumed this month are always there.
/// </summary>
public sealed record GoalProgressView(
    Guid PlaceId,
    string TimeZone,
    DateOnly MonthStart,
    int DaysInMonth,
    int DaysLeft,
    decimal ConsumedLiters,
    bool HasGoal,
    decimal? TargetLiters,
    decimal? Percentage,
    decimal? RemainingLiters,
    string? Status,
    DateTime? UpdatedAt);
