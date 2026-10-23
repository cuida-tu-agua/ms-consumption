using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Api.Tests;

public class GoalsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _place = Guid.NewGuid();

    public GoalsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.CreateSchema();
        _factory.Places.Mine.Add(_place);
    }

    private HttpClient Client()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TokenFor(_user));
        return client;
    }

    private string Url(Guid? place = null) => $"/api/places/{place ?? _place}/goals/current?tz=America/Bogota";

    [Fact]
    public async Task Set_edit_and_read_the_goal_of_the_month_through_the_real_database()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecordReadingUseCase>().ExecuteAsync(
                new ReadingReceivedEvent(Guid.NewGuid(), _place, DateTime.UtcNow.AddMinutes(-1), 2m, 900m, 900m, DateTime.UtcNow), default);
        }

        var empty = await (await Client().GetAsync(Url())).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(empty.GetProperty("hasGoal").GetBoolean());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("targetLiters").ValueKind);
        Assert.Equal(900m, empty.GetProperty("consumedLiters").GetDecimal());

        var created = await Client().PutAsJsonAsync(Url(), new { targetLiters = 1000 });
        created.EnsureSuccessStatusCode();
        var view = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(view.GetProperty("hasGoal").GetBoolean());
        Assert.Equal(1000m, view.GetProperty("targetLiters").GetDecimal());
        Assert.Equal(90m, view.GetProperty("percentage").GetDecimal());
        Assert.Equal("WARNING", view.GetProperty("status").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-01$", view.GetProperty("monthStart").GetString());   // a plain date for the app

        (await Client().PutAsJsonAsync(Url(), new { targetLiters = 2000 })).EnsureSuccessStatusCode();
        var read = await (await Client().GetAsync(Url())).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2000m, read.GetProperty("targetLiters").GetDecimal());
        Assert.Equal("ON_TRACK", read.GetProperty("status").GetString());
        Assert.Equal(1100m, read.GetProperty("remainingLiters").GetDecimal());
    }

    [Theory]
    [InlineData("{\"targetLiters\":0}")]
    [InlineData("{\"targetLiters\":-10}")]
    [InlineData("{}")]
    public async Task A_bad_target_is_400_with_the_code(string json)
    {
        var response = await Client().PutAsync(Url(), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("goal.invalid_target", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Someone_elses_place_is_404_and_without_a_token_401()
    {
        var other = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync(Url(other))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().PutAsJsonAsync(Url(other), new { targetLiters = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(Url())).StatusCode);
    }
}
