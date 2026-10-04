namespace SyWater.Consumption.Domain.Consumption;

/// <summary>Liters of one place during one UTC hour (a row of monitoring.consumption_hourly).</summary>
public sealed record HourlyTotal(DateTime HourStartUtc, decimal Liters, int ReadingCount);

/// <summary>One bar of the chart.</summary>
public sealed record ConsumptionBucket(DateTime StartUtc, decimal Liters);

public sealed record ConsumptionSummary(decimal TotalLiters, bool HasData, IReadOnlyList<ConsumptionBucket> Buckets)
{
    /// <summary>Puts every hourly row in its bar. Rows outside the window are ignored.</summary>
    public static ConsumptionSummary From(PeriodWindow window, IEnumerable<HourlyTotal> hours)
    {
        var liters = new decimal[window.BucketStartsUtc.Count];
        var readings = 0;

        foreach (var hour in hours)
        {
            var index = window.BucketOf(hour.HourStartUtc);
            if (index < 0) continue;
            liters[index] += hour.Liters;
            readings += hour.ReadingCount;
        }

        var buckets = window.BucketStartsUtc.Select((start, i) => new ConsumptionBucket(start, liters[i])).ToList();
        
        return new ConsumptionSummary(liters.Sum(), readings > 0, buckets);
    }
}
