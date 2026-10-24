using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.Ports.Out;

public interface IReadingStore
{
    Task<bool> AddAsync(Reading reading, CancellationToken ct);
}

public interface IConsumptionReader
{
    Task<IReadOnlyList<HourlyTotal>> GetHourlyAsync(Guid placeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    Task<LatestReading?> GetLatestAsync(Guid placeId, CancellationToken ct);
}

public sealed record LatestReading(DateTime RecordedAtUtc, decimal FlowLpm);

public interface IPlaceAccessChecker
{
    /// <summary>True only for the OWNER of the place. Collaborators are not included: use <see cref="GetAccessAsync"/>.</summary>
    /// <exception cref="ExternalServiceUnavailableException">place-service did not answer.</exception>
    Task<bool> CanReadAsync(Guid placeId, CancellationToken ct);

    /// <summary>What the caller may do in the place (owner, or collaborator with permissions), or null if nothing.</summary>
    /// <exception cref="ExternalServiceUnavailableException">place-service did not answer.</exception>
    Task<PlaceAccess?> GetAccessAsync(Guid placeId, CancellationToken ct);
}

/// <summary>The role and permission keys that ms-places gives a user in a place (see GET /api/places/{id}/access).</summary>
public sealed record PlaceAccess(string Role, IReadOnlyCollection<string> Permissions)
{
    public const string ViewConsumption = "VIEW_CONSUMPTION";

    public bool IsOwner => Role == "OWNER";

    public bool Has(string permission) => IsOwner || Permissions.Contains(permission);
}

/// <summary>Which place an anonymous guest link opens (ms-places owns the links).</summary>
public interface IGuestLinkResolver
{
    /// <returns>The place of an ACTIVE link, or null if the token is unknown, deactivated or its place was deleted.</returns>
    /// <exception cref="ExternalServiceUnavailableException">place-service did not answer.</exception>
    Task<Guid?> ResolvePlaceAsync(string token, CancellationToken ct);
}

public sealed class ExternalServiceUnavailableException(string service, Exception? inner = null)
    : Exception($"The service '{service}' is not available right now.", inner)
{
    public string Service { get; } = service;
}
