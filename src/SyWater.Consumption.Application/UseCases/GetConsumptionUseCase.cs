using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.UseCases;

/// <summary>
/// HU-015/016/017/018. Order: validate the query (400) → the caller has access to the place (404, asks ms-places)
/// → read. The caller is the owner or a collaborator (VIEW_CONSUMPTION, which every collaborator has). Someone else's
/// place gets the same 404 as a place that does not exist.
/// </summary>
public sealed class GetConsumptionUseCase(IPlaceAccessChecker places, IConsumptionReader reader, TimeProvider clock)
    : IGetConsumptionUseCase
{
    public async Task<ConsumptionView> ExecuteAsync(Guid placeId, string? period, string? timeZone, CancellationToken ct)
    {
        var parsedPeriod = ConsumptionPeriods.Parse(period);
        var zone = ConsumptionPeriods.ParseTimeZone(timeZone);

        var access = await places.GetAccessAsync(placeId, ct);
        if (access is null || !access.Has(PlaceAccess.ViewConsumption)) throw new PlaceNotFoundException(placeId);

        return await ConsumptionViews.BuildAsync(placeId, parsedPeriod, zone, reader, clock, ct);
    }
}

/// <summary>
/// HU-040/041. A guest has no account: the token of the link is the credential and ms-places says which place it opens.
/// Read-only by construction (this is the only thing a guest can call). A token that does not open anything is the
/// same 404 whether it never existed, was deactivated or its place was deleted.
/// </summary>
public sealed class GetGuestConsumptionUseCase(IGuestLinkResolver links, IConsumptionReader reader, TimeProvider clock)
    : IGetGuestConsumptionUseCase
{
    public async Task<ConsumptionView> ExecuteAsync(string token, string? period, string? timeZone, CancellationToken ct)
    {
        var parsedPeriod = ConsumptionPeriods.Parse(period);
        var zone = ConsumptionPeriods.ParseTimeZone(timeZone);

        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) throw new GuestLinkNotFoundException();
        var placeId = await links.ResolvePlaceAsync(token, ct) ?? throw new GuestLinkNotFoundException();

        return await ConsumptionViews.BuildAsync(placeId, parsedPeriod, zone, reader, clock, ct);
    }
}
