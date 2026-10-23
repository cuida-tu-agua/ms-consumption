using SyWater.Consumption.Domain.Goals;

namespace SyWater.Consumption.Domain.Tests;

public class GoalTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly October = new(2026, 10, 1);
    private readonly Guid _place = Guid.NewGuid();

    [Fact]
    public void A_new_goal_is_manual_in_liters_and_rounded_to_milliliters()
    {
        var goal = Goal.Create(_place, October, 12_000.12345m, Now);

        Assert.Equal(12_000.123m, goal.TargetLiters);
        Assert.Equal(GoalSource.Manual, goal.Source);
        Assert.Equal(Now, goal.CreatedAtUtc);
        Assert.Equal(Now, goal.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("0.0004")]          // rounds to 0
    [InlineData("10000000.001")]    // above the 10,000 m³ limit
    public void The_target_must_be_more_than_zero_and_reasonable(string? value)
    {
        decimal? target = value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var error = Assert.Throws<InvalidGoalTargetException>(() => Goal.Create(_place, October, target ?? 0, Now));
        Assert.Equal("goal.invalid_target", error.Code);
        Assert.Throws<InvalidGoalTargetException>(() => Goal.ValidTarget(target));
    }

    [Fact]
    public void The_month_starts_on_day_1()
    {
        Assert.Throws<ArgumentException>(() => Goal.Create(_place, new DateOnly(2026, 10, 2), 100m, Now));
    }

    [Fact]
    public void Editing_changes_the_target_and_the_update_time_only()
    {
        var goal = Goal.Create(_place, October, 12_000m, Now);
        goal.ChangeTarget(9_500m, Now.AddDays(2));

        Assert.Equal(9_500m, goal.TargetLiters);
        Assert.Equal(Now, goal.CreatedAtUtc);
        Assert.Equal(Now.AddDays(2), goal.UpdatedAtUtc);
        Assert.Throws<InvalidGoalTargetException>(() => goal.ChangeTarget(0m, Now));
        Assert.Equal(9_500m, goal.TargetLiters);   // a rejected edit changes nothing
    }

    [Theory]
    [InlineData(0, 0, "OnTrack")]
    [InlineData(7_996, 79.9, "OnTrack")]     // 79.96 % is truncated, never shown as 80 while on track
    [InlineData(8_000, 80, "Warning")]
    [InlineData(9_999, 99.9, "Warning")]
    [InlineData(10_000, 100, "Exceeded")]
    [InlineData(12_500, 125, "Exceeded")]
    public void Progress_changes_color_at_80_and_100_percent(decimal consumed, decimal percentage, string status)
    {
        var progress = GoalProgress.For(10_000m, consumed);

        Assert.Equal(percentage, progress.Percentage);
        Assert.Equal(Enum.Parse<GoalStatus>(status), progress.Status);
        Assert.Equal(Math.Max(10_000m - consumed, 0m), progress.RemainingLiters);   // never negative
    }

    [Theory]
    [InlineData(2026, 10, 1, 31, 31)]   // first day: the whole month is left
    [InlineData(2026, 10, 20, 31, 12)]
    [InlineData(2026, 10, 31, 31, 1)]   // last day: today is still left
    [InlineData(2028, 2, 29, 29, 1)]    // leap year
    public void The_month_counts_today_among_the_days_left(int year, int month, int day, int daysInMonth, int daysLeft)
    {
        var goalMonth = GoalMonth.Of(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(year, month, 1), goalMonth.MonthStart);
        Assert.Equal(daysInMonth, goalMonth.DaysInMonth);
        Assert.Equal(daysLeft, goalMonth.DaysLeft);
    }
}
