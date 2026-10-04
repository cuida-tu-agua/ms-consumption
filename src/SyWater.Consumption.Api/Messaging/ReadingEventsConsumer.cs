using System.Text.Json;
using Microsoft.Extensions.Options;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Readings;

namespace SyWater.Consumption.Api.Messaging;

/// <summary>Queue "consumption.readings": every "device.reading.received" published by device-service.</summary>
public sealed class ReadingEventsConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> options,
    ILogger<ReadingEventsConsumer> logger) : RabbitMqEventConsumer(options, logger)
{
    protected override string QueueName => "consumption.readings";
    protected override IReadOnlyList<string> RoutingKeys => [ReadingReceivedEvent.EventType];

    protected override async Task HandleAsync(string type, JsonElement data, CancellationToken ct)
    {
        var reading = data.Deserialize<ReadingReceivedEvent>(Json)
                      ?? throw new JsonException("Empty reading event.");
        if (reading.DeviceId == Guid.Empty || reading.PlaceId == Guid.Empty)
            throw new JsonException("Reading event without deviceId/placeId.");   // can never be stored: dead-letter

        using var scope = scopes.CreateScope();   // one DbContext per message
        var added = await scope.ServiceProvider.GetRequiredService<IRecordReadingUseCase>().ExecuteAsync(reading, ct);
        if (!added) logger.LogDebug("Duplicate reading of device {Device} at {At} ignored", reading.DeviceId, reading.RecordedAt);
    }
}
