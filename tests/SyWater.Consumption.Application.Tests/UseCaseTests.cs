using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Application.UseCases;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.Tests;

internal sealed class FakeStore : IReadingStore, IConsumptionReader
{
    public List<Reading> Readings { get; } = [];

    public Task<bool> AddAsync(Reading reading, CancellationToken ct)
    {
        if (Readings.Any(r => r.DeviceId == reading.DeviceId && r.RecordedAtUtc == reading.RecordedAtUtc))
            return Task.FromResult(false);
        Readings.Add(reading);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<HourlyTotal>> GetHourlyAsync(Guid placeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HourlyTotal>>(Readings
            .Where(r => r.PlaceId == placeId && r.HourStartUtc >= fromUtc && r.HourStartUtc < toUtc)
            .GroupBy(r => r.HourStartUtc)
            .Select(g => new HourlyTotal(g.Key, g.Sum(r => r.VolumeLiters), g.Count()))
            .ToList());

    public Task<LatestReading?> GetLatestAsync(Guid placeId, CancellationToken ct) =>
        Task.FromResult(Readings.Where(r => r.PlaceId == placeId).OrderByDescending(r => r.RecordedAtUtc)
            .Select(r => new LatestReading(r.RecordedAtUtc, r.FlowLpm)).FirstOrDefault());
}

internal sealed class FakePlaces : IPlaceAccessChecker
{
    public HashSet<Guid> Mine { get; } = [];
    public bool Down { get; set; }

    public Task<bool> CanReadAsync(Guid placeId, CancellationToken ct) =>
        Down ? throw new ExternalServiceUnavailableException("place-service") : Task.FromResult(Mine.Contains(placeId));
}

internal sealed class FakeClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}

public class UseCaseTests
{
    // 2026-10-03 10:00 in Bogotá
    private static readonly DateTime Now = new(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc);
    private readonly FakeStore _store = new();
    private readonly FakePlaces _places = new();
    private readonly Guid _place = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();

    public UseCaseTests() => _places.Mine.Add(_place);

    private ReadingReceivedEvent Event(DateTime at, decimal vol, decimal lpm = 2m) =>
        new(_device, _place, at, lpm, vol, 100m, at);

    private GetConsumptionUseCase Get() => new(_places, _store, new FakeClock(Now));

    [Fact]
    public async Task The_same_reading_delivered_twice_is_stored_once()
    {
        var record = new RecordReadingUseCase(_store);
        Assert.True(await record.ExecuteAsync(Event(Now.AddMinutes(-5), 1.5m), default));
        Assert.False(await record.ExecuteAsync(Event(Now.AddMinutes(-5), 1.5m), default));
        Assert.Single(_store.Readings);
    }

    [Fact]
    public async Task Today_adds_up_the_readings_of_today_in_local_time()
    {
        var record = new RecordReadingUseCase(_store);
        await record.ExecuteAsync(Event(Now.AddMinutes(-30), 2m), default);                       // 09:30 local today
        await record.ExecuteAsync(Event(Now.AddSeconds(-10), 0.5m, lpm: 3m), default);           // 09:59:50 local today
        await record.ExecuteAsync(Event(new DateTime(2026, 10, 3, 4, 0, 0, DateTimeKind.Utc), 9m), default); // 23:00 local YESTERDAY

        var view = await Get().ExecuteAsync(_place, "day", "America/Bogota", default);

        Assert.Equal(2.5m, view.TotalLiters);
        Assert.True(view.HasData);
        Assert.Equal("DAY", view.Period);
        Assert.Equal(24, view.Buckets.Count);
        Assert.Equal(3m, view.CurrentFlowLpm);                     // HU-018: last reading is 10 s old
        Assert.Equal(Now.AddSeconds(-10), view.LastReadingAt);
    }

    [Fact]
    public async Task Someone_elses_place_is_404_and_bad_queries_are_400()
    {
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get().ExecuteAsync(Guid.NewGuid(), "day", null, default));
        await Assert.ThrowsAsync<InvalidPeriodException>(() => Get().ExecuteAsync(_place, "year", null, default));
        await Assert.ThrowsAsync<UnknownTimeZoneException>(() => Get().ExecuteAsync(_place, "day", "Nowhere/City", default));
    }

    [Fact]
    public async Task Without_readings_there_is_no_data_and_no_flow()
    {
        var view = await Get().ExecuteAsync(_place, "month", null, default);
        Assert.False(view.HasData);
        Assert.Null(view.CurrentFlowLpm);
        Assert.Null(view.LastReadingAt);
    }

    [Fact]
    public async Task Places_service_down_bubbles_up_as_unavailable()
    {
        _places.Down = true;
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => Get().ExecuteAsync(_place, "day", null, default));
    }
}
