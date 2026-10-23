using System.Globalization;
using SyWater.Consumption.Domain.Common;

namespace SyWater.Consumption.Domain.Goals;

/// <summary>The target is missing, 0 or less, or absurdly big (INV-G002). HTTP 400.</summary>
public sealed class InvalidGoalTargetException(decimal? targetLiters)
    : DomainException("goal.invalid_target",
        $"Invalid goal '{targetLiters?.ToString(CultureInfo.InvariantCulture)}'. Use liters greater than 0 and up to " +
        $"{Goal.MaxTargetLiters.ToString(CultureInfo.InvariantCulture)}.");
