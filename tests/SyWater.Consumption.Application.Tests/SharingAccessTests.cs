using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.UseCases;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.Tests;

/// <summary>HU-040/041/045: collaborators and guests read the water of a place; nobody else does.</summary>
public class SharingAccessTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc);
    private readonly FakeStore _store = new();
    private readonly FakePlaces _places = new();
    private readonly FakeGuestLinks _links = new();
    private readonly Guid _place = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();
    private const string Token = "a-guest-token-with-more-than-32-characters-x";

    private async Task Water(decimal liters)
    {
        var at = Now.AddMinutes(-30);
        await new RecordReadingUseCase(_store).ExecuteAsync(
            new Readings.ReadingReceivedEvent(_device, _place, at, 2m, liters, 100m, at), default);
    }

    private GetConsumptionUseCase Get() => new(_places, _store, new FakeClock(Now));
    private GetGuestConsumptionUseCase Guest() => new(_links, _store, new FakeClock(Now));

    [Fact]
    public async Task A_collaborator_reads_the_consumption_of_the_place_they_were_invited_to()
    {
        await Water(2m);
        _places.Shared[_place] = ["VIEW_CONSUMPTION"];

        var view = await Get().ExecuteAsync(_place, "day", "America/Bogota", default);

        Assert.Equal(2m, view.TotalLiters);
    }

    [Fact]
    public async Task A_stranger_still_gets_404_and_a_collaborator_of_another_place_too()
    {
        _places.Shared[Guid.NewGuid()] = ["VIEW_CONSUMPTION"];

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get().ExecuteAsync(_place, "day", null, default));
    }

    [Fact]
    public async Task Access_without_VIEW_CONSUMPTION_is_404_not_a_leak()
    {
        _places.Shared[_place] = ["VIEW_ALERTS"];   // ms-places always includes it; this is the defensive case

        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get().ExecuteAsync(_place, "day", null, default));
    }

    [Fact]
    public async Task The_goal_stays_owner_only_even_for_a_collaborator()
    {
        _places.Shared[_place] = ["VIEW_CONSUMPTION", "VIEW_BILLS"];

        Assert.False(await _places.CanReadAsync(_place, default));
    }

    [Fact]
    public async Task A_guest_with_an_active_link_sees_the_consumption()
    {
        await Water(3m);
        _links.Active[Token] = _place;

        var view = await Guest().ExecuteAsync(Token, "day", "America/Bogota", default);

        Assert.Equal(3m, view.TotalLiters);
        Assert.Equal(_place, view.PlaceId);
    }

    [Fact]
    public async Task A_link_that_opens_nothing_is_404_whatever_the_reason()
    {
        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => Guest().ExecuteAsync(Token, "day", null, default));
        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => Guest().ExecuteAsync("  ", "day", null, default));
        await Assert.ThrowsAsync<GuestLinkNotFoundException>(() => Guest().ExecuteAsync(new string('x', 65), "day", null, default));
    }

    [Fact]
    public async Task A_guest_gets_400_for_a_bad_query_and_503_when_places_is_down_never_a_free_pass()
    {
        _links.Active[Token] = _place;
        await Assert.ThrowsAsync<InvalidPeriodException>(() => Guest().ExecuteAsync(Token, "year", null, default));

        _links.Down = true;
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => Guest().ExecuteAsync(Token, "day", null, default));
    }
}
