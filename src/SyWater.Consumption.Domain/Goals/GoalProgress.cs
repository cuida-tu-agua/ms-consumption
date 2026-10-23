namespace SyWater.Consumption.Domain.Goals;

/// <summary>HU-024: the color of the goal indicator. It changes at 80 % and at 100 %.</summary>
public enum GoalStatus { OnTrack, Warning, Exceeded }

/// <summary>
/// HU-024: how much of the monthly goal is used and how many days are left (today included).
/// Consumption can pass the goal: the percentage then goes above 100 and nothing is left.
/// </summary>
public sealed record GoalProgress(decimal Percentage, decimal RemainingLiters, GoalStatus Status)
{
    public const decimal WarningAt = 80m;
    public const decimal ExceededAt = 100m;

    public static GoalProgress For(decimal targetLiters, decimal consumedLiters)
    {
        if (targetLiters <= 0) throw new ArgumentOutOfRangeException(nameof(targetLiters), "A goal is always greater than 0.");
        var consumed = Math.Max(consumedLiters, 0m);
        var exact = consumed / targetLiters * 100m;

        var status = exact >= ExceededAt ? GoalStatus.Exceeded
            : exact >= WarningAt ? GoalStatus.Warning
            : GoalStatus.OnTrack;

        // Truncated, not rounded: 79.96 % shows 79.9, so the number never says 80 while the color is still "on track"
        return new GoalProgress(Math.Round(exact, 1, MidpointRounding.ToZero), Math.Max(targetLiters - consumed, 0m), status);
    }
}

/// <summary>The local month of "today": its first day, how many days it has and how many are left (today included).</summary>
public sealed record GoalMonth(DateOnly MonthStart, int DaysInMonth, int DaysLeft)
{
    public static GoalMonth Of(DateOnly localToday)
    {
        var days = DateTime.DaysInMonth(localToday.Year, localToday.Month);
        return new GoalMonth(new DateOnly(localToday.Year, localToday.Month, 1), days, days - localToday.Day + 1);
    }
}
