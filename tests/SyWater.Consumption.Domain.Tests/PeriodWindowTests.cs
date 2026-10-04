using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Domain.Tests;

public class PeriodWindowTests
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    // 2026-10-03 20:30 in Bogotá (UTC-5) = 2026-10-04 01:30 UTC: in UTC it is already "tomorrow"
    private static readonly DateTime Now = new(2026, 10, 4, 1, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Day_is_today_in_the_users_time_zone_with_24_hourly_bars()
    {
        var w = PeriodWindow.For(ConsumptionPeriod.Day, Now, Bogota);

        Assert.Equal(new DateTime(2026, 10, 3, 5, 0, 0), w.FromUtc);   // local midnight Oct 3
        Assert.Equal(new DateTime(2026, 10, 4, 5, 0, 0), w.ToUtc);
        Assert.Equal(24, w.BucketStartsUtc.Count);
        Assert.Equal(w.FromUtc.AddHours(23), w.BucketStartsUtc[^1]);
    }

    [Fact]
    public void Week_is_the_last_7_local_days_including_today()
    {
        var w = PeriodWindow.For(ConsumptionPeriod.Week, Now, Bogota);

        Assert.Equal(7, w.BucketStartsUtc.Count);
        Assert.Equal(new DateTime(2026, 9, 27, 5, 0, 0), w.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 4, 5, 0, 0), w.ToUtc);
    }

    [Fact]
    public void Month_is_the_calendar_month_one_bar_per_day()
    {
        var w = PeriodWindow.For(ConsumptionPeriod.Month, Now, Bogota);

        Assert.Equal(31, w.BucketStartsUtc.Count);                       // October
        Assert.Equal(new DateTime(2026, 10, 1, 5, 0, 0), w.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 1, 5, 0, 0), w.ToUtc);
    }

    [Fact]
    public void A_day_with_a_daylight_saving_change_has_25_bars()
    {
        // US fall back: 2026-11-01 has 25 hours in New York
        var w = PeriodWindow.For(ConsumptionPeriod.Day, new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc), NewYork);
        Assert.Equal(25, w.BucketStartsUtc.Count);
    }

    [Fact]
    public void Each_hour_goes_to_its_local_day()
    {
        var w = PeriodWindow.For(ConsumptionPeriod.Week, Now, Bogota);

        Assert.Equal(6, w.BucketOf(new DateTime(2026, 10, 4, 1, 0, 0)));   // 20:00 local Oct 3 → today (last bar)
        Assert.Equal(5, w.BucketOf(new DateTime(2026, 10, 3, 4, 0, 0)));   // 23:00 local Oct 2 → yesterday
        Assert.Equal(-1, w.BucketOf(new DateTime(2026, 9, 27, 4, 0, 0)));  // before the window
    }

    [Theory]
    [InlineData(null, ConsumptionPeriod.Day)]
    [InlineData("", ConsumptionPeriod.Day)]
    [InlineData("WEEK", ConsumptionPeriod.Week)]
    [InlineData(" month ", ConsumptionPeriod.Month)]
    public void Periods_are_parsed(string? value, ConsumptionPeriod expected) =>
        Assert.Equal(expected, ConsumptionPeriods.Parse(value));

    [Fact]
    public void Unknown_period_and_time_zone_are_rejected()
    {
        Assert.Throws<InvalidPeriodException>(() => ConsumptionPeriods.Parse("year"));
        Assert.Throws<UnknownTimeZoneException>(() => ConsumptionPeriods.ParseTimeZone("Mars/Olympus"));
        Assert.Equal("America/Bogota", ConsumptionPeriods.ParseTimeZone(null).Id);
    }
}

public class SummaryAndFlowTests
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly DateTime Now = new(2026, 10, 4, 1, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Hours_are_added_into_their_bars_and_the_total()
    {
        var w = PeriodWindow.For(ConsumptionPeriod.Week, Now, Bogota);
        var summary = ConsumptionSummary.From(w,
        [
            new HourlyTotal(new DateTime(2026, 10, 4, 1, 0, 0), 12.5m, 300),
            new HourlyTotal(new DateTime(2026, 10, 3, 14, 0, 0), 7.5m, 200),   // same local day
            new HourlyTotal(new DateTime(2026, 10, 3, 4, 0, 0), 3m, 50),       // the day before
            new HourlyTotal(new DateTime(2026, 9, 1, 0, 0, 0), 999m, 1),       // outside: ignored
        ]);

        Assert.Equal(23m, summary.TotalLiters);
        Assert.True(summary.HasData);
        Assert.Equal(20m, summary.Buckets[6].Liters);
        Assert.Equal(3m, summary.Buckets[5].Liters);
    }

    [Fact]
    public void Without_readings_HasData_is_false_so_the_app_does_not_show_a_misleading_zero()
    {
        var summary = ConsumptionSummary.From(PeriodWindow.For(ConsumptionPeriod.Day, Now, Bogota), []);
        Assert.False(summary.HasData);
        Assert.Equal(24, summary.Buckets.Count);
    }

    [Fact]
    public void Current_flow_is_only_known_with_a_recent_reading()
    {
        Assert.Equal(3.5m, CurrentFlow.From(Now.AddSeconds(-10), 3.5m, Now));
        Assert.Equal(0m, CurrentFlow.From(Now.AddSeconds(-10), 0m, Now));       // HU-018: "no hay flujo"
        Assert.Null(CurrentFlow.From(Now.AddMinutes(-5), 3.5m, Now));            // device silent: unknown
        Assert.Null(CurrentFlow.From(null, null, Now));
    }

    [Fact]
    public void A_reading_belongs_to_its_utc_hour_and_rejects_negative_values()
    {
        var r = Reading.Create(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 4, 1, 47, 10, DateTimeKind.Utc), 2, 0.3m, 10, Now);
        Assert.Equal(new DateTime(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc), r.HourStartUtc);
        Assert.Throws<InvalidReadingException>(() => Reading.Create(Guid.NewGuid(), Guid.NewGuid(), Now, -1, 0, 0, Now));
    }
}
