namespace SyWater.Consumption.Domain.Goals;

/// <summary>Where a goal came from. Only MANUAL exists today (HU-023); the other two are for suggested goals.</summary>
public enum GoalSource { Manual, Suggested, CarriedOver }

/// <summary>
/// BC-05 aggregate root (HU-023): the monthly consumption target of one place. One per place and month
/// (INV-G001, unique in the database). Always in liters: the app converts to the unit of the place.
/// </summary>
public sealed class Goal
{
    /// <summary>10,000 m³ in a month: far above any home or shop, it only stops typing mistakes.</summary>
    public const decimal MaxTargetLiters = 10_000_000m;

    public Guid PlaceId { get; }
    public DateOnly MonthStart { get; }
    public decimal TargetLiters { get; private set; }
    public GoalSource Source { get; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Goal(Guid placeId, DateOnly monthStart, decimal targetLiters, GoalSource source, DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        PlaceId = placeId;
        MonthStart = monthStart;
        TargetLiters = targetLiters;
        Source = source;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>A new manual goal for the month that starts on <paramref name="monthStart"/> (day 1).</summary>
    public static Goal Create(Guid placeId, DateOnly monthStart, decimal targetLiters, DateTime nowUtc)
    {
        if (placeId == Guid.Empty) throw new ArgumentException("The place is required.", nameof(placeId));
        if (monthStart.Day != 1) throw new ArgumentException("A goal month starts on day 1.", nameof(monthStart));
        var now = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        return new Goal(placeId, monthStart, ValidTarget(targetLiters), GoalSource.Manual, now, now);
    }

    /// <summary>Rebuilds a goal read from the database (no validation: the database already checked it).</summary>
    public static Goal Restore(Guid placeId, DateOnly monthStart, decimal targetLiters, GoalSource source,
        DateTime createdAtUtc, DateTime updatedAtUtc) =>
        new(placeId, monthStart, targetLiters, source, DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc));

    /// <summary>HU-023 "edit goal". Only the current month is ever edited: the API has no way to reach a past one (INV-G004).</summary>
    public void ChangeTarget(decimal targetLiters, DateTime nowUtc)
    {
        TargetLiters = ValidTarget(targetLiters);
        UpdatedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
    }

    /// <summary>INV-G002: more than 0. Rounded to the 3 decimals of the column (milliliters).</summary>
    public static decimal ValidTarget(decimal? targetLiters)
    {
        if (targetLiters is not { } value || value <= 0 || value > MaxTargetLiters)
            throw new InvalidGoalTargetException(targetLiters);
        var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        if (rounded <= 0) throw new InvalidGoalTargetException(targetLiters);
        return rounded;
    }
}
