namespace SyWater.Consumption.Domain.Consumption;

public sealed record PeriodWindow(ConsumptionPeriod Period, TimeZoneInfo Zone, DateTime FromUtc, DateTime ToUtc,
    IReadOnlyList<DateTime> BucketStartsUtc)
{
    public static PeriodWindow For(ConsumptionPeriod period, DateTime nowUtc, TimeZoneInfo zone)
    {
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone).Date;

        var (localFrom, localTo) = period switch
        {
            ConsumptionPeriod.Day => (localToday, localToday.AddDays(1)),
            ConsumptionPeriod.Week => (localToday.AddDays(-6), localToday.AddDays(1)),
            _ => (new DateTime(localToday.Year, localToday.Month, 1), new DateTime(localToday.Year, localToday.Month, 1).AddMonths(1)),
        };

        var fromUtc = LocalMidnightToUtc(localFrom, zone);
        var toUtc = LocalMidnightToUtc(localTo, zone);

        var buckets = new List<DateTime>();
        if (period == ConsumptionPeriod.Day)
        {
            // UTC hours: a day with a daylight-saving change simply has 23 or 25 bars
            for (var hour = fromUtc; hour < toUtc; hour = hour.AddHours(1)) buckets.Add(hour);
        }
        else
        {
            for (var day = localFrom; day < localTo; day = day.AddDays(1)) buckets.Add(LocalMidnightToUtc(day, zone));
        }

        return new PeriodWindow(period, zone, fromUtc, toUtc, buckets);
    }

    /// <summary>Index of the bar that contains this UTC instant, or -1 if it is outside the window.</summary>
    public int BucketOf(DateTime utc)
    {
        if (utc < FromUtc || utc >= ToUtc) return -1;
        for (var i = BucketStartsUtc.Count - 1; i >= 0; i--)
            if (utc >= BucketStartsUtc[i]) return i;
        return -1;
    }

    private static DateTime LocalMidnightToUtc(DateTime localDate, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        // A few zones skip midnight when daylight saving starts: the day begins at 01:00 then
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
