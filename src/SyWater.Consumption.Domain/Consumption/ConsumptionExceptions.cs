using SyWater.Consumption.Domain.Common;

namespace SyWater.Consumption.Domain.Consumption;

/// <summary>period is not day / week / month. HTTP 400.</summary>
public sealed class InvalidPeriodException(string? period)
    : DomainException("consumption.invalid_period", $"Unknown period '{period}'. Use day, week or month.");

/// <summary>tz is not an IANA time zone such as America/Bogota. HTTP 400.</summary>
public sealed class UnknownTimeZoneException(string? timeZone)
    : DomainException("consumption.invalid_time_zone", $"Unknown time zone '{timeZone}'. Use an IANA id such as America/Bogota.");

/// <summary>The place does not exist or is not yours (same answer on purpose). HTTP 404.</summary>
public sealed class PlaceNotFoundException(Guid placeId)
    : DomainException("place.not_found", $"Place {placeId} was not found.");

/// <summary>A reading with impossible values (defense in depth: ms-devices already filters). Not HTTP.</summary>
public sealed class InvalidReadingException(string reason)
    : DomainException("reading.invalid", reason);

/// <summary>The guest link does not exist, was deactivated or its place was deleted (same answer on purpose). HTTP 404.</summary>
public sealed class GuestLinkNotFoundException()
    : DomainException("guest_link.not_found", "This link does not exist or is no longer active.");
