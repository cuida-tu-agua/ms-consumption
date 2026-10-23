using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Application.UseCases;
using SyWater.Consumption.Domain.Consumption;
using SyWater.Consumption.Domain.Goals;

namespace SyWater.Consumption.Application.Tests;

internal sealed class FakeGoals : IGoalStore
{
    public List<Goal> Rows { get; } = [];
    public int Updates { get; private set; }

    /// <summary>Simulates another request saving the same goal between our Find and our Add.</summary>
    public Goal? RaceWinner { get; set; }

    public Task<Goal?> FindAsync(Guid placeId, DateOnly monthStart, CancellationToken ct) =>
        Task.FromResult(Rows.FirstOrDefault(g => g.PlaceId == placeId && g.MonthStart == monthStart));

    public Task<bool> AddAsync(Goal goal, CancellationToken ct)
    {
        if (RaceWinner is { } winner) { Rows.Add(winner); RaceWinner = null; }
        if (Rows.Any(g => g.PlaceId == goal.PlaceId && g.MonthStart == goal.MonthStart)) return Task.FromResult(false);
        Rows.Add(goal);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(Goal goal, CancellationToken ct)
    {
        Updates++;
        return Task.CompletedTask;   // the use case edits the same instance it found
    }
}

public class GoalUseCaseTests
{
    // 2026-10-20 10:00 in Bogotá
    private static readonly DateTime Now = new(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly October = new(2026, 10, 1);
    private readonly FakeStore _readings = new();
    private readonly FakePlaces _places = new();
    private readonly FakeGoals _goals = new();
    private readonly Guid _place = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();

    public GoalUseCaseTests() => _places.Mine.Add(_place);

    private GetGoalProgressUseCase Get(DateTime? now = null) => new(_places, _goals, _readings, new FakeClock(now ?? Now));
    private SetMonthlyGoalUseCase Set(DateTime? now = null) => new(_places, _goals, _readings, new FakeClock(now ?? Now));

    private Task Read(DateTime at, decimal liters) =>
        new RecordReadingUseCase(_readings).ExecuteAsync(new ReadingReceivedEvent(_device, _place, at, 2m, liters, 100m, at), default);

    [Fact]
    public async Task Without_a_goal_it_invites_to_create_one_and_still_shows_the_liters_of_the_month()
    {
        await Read(new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), 300m);

        var view = await Get().ExecuteAsync(_place, "America/Bogota", default);

        Assert.False(view.HasGoal);
        Assert.Null(view.TargetLiters);
        Assert.Null(view.Percentage);
        Assert.Null(view.Status);
        Assert.Equal(300m, view.ConsumedLiters);
        Assert.Equal(October, view.MonthStart);
        Assert.Equal(31, view.DaysInMonth);
        Assert.Equal(12, view.DaysLeft);
    }

    [Fact]
    public async Task Setting_a_goal_creates_it_and_setting_it_again_edits_the_same_one()
    {
        await Read(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc), 8_500m);

        var created = await Set().ExecuteAsync(_place, 10_000m, "America/Bogota", default);
        Assert.True(created.HasGoal);
        Assert.Equal(10_000m, created.TargetLiters);
        Assert.Equal(85m, created.Percentage);
        Assert.Equal(1_500m, created.RemainingLiters);
        Assert.Equal("WARNING", created.Status);

        var edited = await Set(Now.AddHours(1)).ExecuteAsync(_place, 8_000m, "America/Bogota", default);
        Assert.Single(_goals.Rows);
        Assert.Equal(1, _goals.Updates);
        Assert.Equal(8_000m, edited.TargetLiters);
        Assert.Equal("EXCEEDED", edited.Status);
        Assert.Equal(0m, edited.RemainingLiters);
        Assert.Equal(Now.AddHours(1), edited.UpdatedAt);

        var read = await Get().ExecuteAsync(_place, "America/Bogota", default);
        Assert.Equal(8_000m, read.TargetLiters);
    }

    [Fact]
    public async Task The_month_is_the_local_one_not_the_utc_one()
    {
        // 2026-11-01 02:00 UTC is still October 31 at 21:00 in Bogotá
        var lateOctober = new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc);
        await Read(new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc), 50m);   // Oct 31 18:00 local

        var view = await Set(lateOctober).ExecuteAsync(_place, 1_000m, "America/Bogota", default);

        Assert.Equal(October, view.MonthStart);
        Assert.Equal(1, view.DaysLeft);
        Assert.Equal(50m, view.ConsumedLiters);
        Assert.Equal(October, _goals.Rows.Single().MonthStart);

        // A new month starts without a goal: October's goal is not carried over
        var november = await Get(new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc)).ExecuteAsync(_place, "America/Bogota", default);
        Assert.Equal(new DateOnly(2026, 11, 1), november.MonthStart);
        Assert.False(november.HasGoal);
        Assert.Equal(0m, november.ConsumedLiters);
    }

    [Fact]
    public async Task Two_saves_at_the_same_time_end_with_one_goal_and_the_last_value()
    {
        _goals.RaceWinner = Goal.Create(_place, October, 5_000m, Now);

        var view = await Set().ExecuteAsync(_place, 7_000m, null, default);

        Assert.Single(_goals.Rows);
        Assert.Equal(7_000m, view.TargetLiters);
        Assert.Equal(1, _goals.Updates);
    }

    [Fact]
    public async Task A_bad_target_is_400_before_asking_ms_places()
    {
        _places.Down = true;   // would be 503 if ms-places were called

        await Assert.ThrowsAsync<InvalidGoalTargetException>(() => Set().ExecuteAsync(_place, 0m, null, default));
        await Assert.ThrowsAsync<InvalidGoalTargetException>(() => Set().ExecuteAsync(_place, null, null, default));
        Assert.Empty(_goals.Rows);
    }

    [Fact]
    public async Task Someone_elses_place_is_404_a_bad_zone_is_400_and_places_down_is_503()
    {
        var other = Guid.NewGuid();
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Get().ExecuteAsync(other, null, default));
        await Assert.ThrowsAsync<PlaceNotFoundException>(() => Set().ExecuteAsync(other, 100m, null, default));
        await Assert.ThrowsAsync<UnknownTimeZoneException>(() => Get().ExecuteAsync(_place, "Nowhere/City", default));
        Assert.Empty(_goals.Rows);

        _places.Down = true;
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => Get().ExecuteAsync(_place, null, default));
    }
}
