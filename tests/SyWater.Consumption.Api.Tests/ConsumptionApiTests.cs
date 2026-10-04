using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Api.Tests;

public class ConsumptionApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _place = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();

    public ConsumptionApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.CreateSchema();
        _factory.Places.Mine.Add(_place);
    }

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _factory.TokenFor(_user));
        return client;
    }

    private async Task<bool> Record(DateTime at, decimal vol, decimal lpm = 2m)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecordReadingUseCase>()
            .ExecuteAsync(new ReadingReceivedEvent(_device, _place, at, lpm, vol, 50m, DateTime.UtcNow), default);
    }

    [Fact]
    public async Task Readings_are_added_per_hour_duplicates_ignored_and_returned_by_the_api()
    {
        var now = DateTime.UtcNow;
        var t1 = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddSeconds(5);
        Assert.True(await Record(t1, 1.25m));
        Assert.True(await Record(t1.AddSeconds(10), 0.75m));
        Assert.False(await Record(t1.AddSeconds(10), 0.75m));                 // same device + same time: duplicate
        Assert.True(await Record(now.AddSeconds(-5), 0.5m, lpm: 3.5m));       // newest reading

        var response = await Client().GetAsync($"/api/places/{_place}/consumption?period=week&tz=America/Bogota");
        response.EnsureSuccessStatusCode();
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("WEEK", view.GetProperty("period").GetString());
        Assert.Equal(2.5m, view.GetProperty("totalLiters").GetDecimal());
        Assert.True(view.GetProperty("hasData").GetBoolean());
        Assert.Equal(7, view.GetProperty("buckets").GetArrayLength());
        Assert.Equal(2.5m, view.GetProperty("buckets")[6].GetProperty("liters").GetDecimal());   // all of it is today
        Assert.Equal(3.5m, view.GetProperty("currentFlowLpm").GetDecimal());
        Assert.EndsWith("Z", view.GetProperty("lastReadingAt").GetString());                     // UTC for the app
    }

    [Fact]
    public async Task Without_a_token_it_is_401()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/places/{_place}/consumption");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_logged_out_token_is_rejected()
    {
        var jti = Guid.NewGuid().ToString();
        _factory.Revocations.Revoked.Add(jti);
        var response = await Client(_factory.TokenFor(_user, jti)).GetAsync($"/api/places/{_place}/consumption");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Someone_elses_place_is_404_with_the_code()
    {
        var response = await Client().GetAsync($"/api/places/{Guid.NewGuid()}/consumption");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("place.not_found", problem.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("period=year", "consumption.invalid_period")]
    [InlineData("tz=Mars/Base", "consumption.invalid_time_zone")]
    public async Task Bad_queries_are_400_with_the_code(string query, string code)
    {
        var response = await Client().GetAsync($"/api/places/{_place}/consumption?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }
}
