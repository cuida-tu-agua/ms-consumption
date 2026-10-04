namespace SyWater.Consumption.Domain.Consumption;

/// <summary>HU-016: the three scales of the panel. Day is the default (HU-015).</summary>
public enum ConsumptionPeriod { Day, Week, Month }

public static class ConsumptionPeriods
{
    /// <summary>"day" / "DIA"... → enum. Null or empty = Day (HU-016: "Día" preselected).</summary>
    public static ConsumptionPeriod Parse(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "" or "day" => ConsumptionPeriod.Day,
        "week" => ConsumptionPeriod.Week,
        "month" => ConsumptionPeriod.Month,
        _ => throw new InvalidPeriodException(value),
    };

    /// <summary>"America/Bogota" → TimeZoneInfo. Null or empty = Colombia (main market, ADR-004).</summary>
    public static TimeZoneInfo ParseTimeZone(string? id)
    {
        var value = string.IsNullOrWhiteSpace(id) ? "America/Bogota" : id.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or System.InvalidTimeZoneException or ArgumentException)
        {
            throw new UnknownTimeZoneException(value);
        }
    }
}
