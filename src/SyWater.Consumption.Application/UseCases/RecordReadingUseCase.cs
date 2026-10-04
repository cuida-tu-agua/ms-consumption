using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.Readings;
using SyWater.Consumption.Domain.Consumption;

namespace SyWater.Consumption.Application.UseCases;

/// <summary>Stores one reading (once) and adds it to the hourly totals of its place.</summary>
public sealed class RecordReadingUseCase(IReadingStore store) : IRecordReadingUseCase
{
    public Task<bool> ExecuteAsync(ReadingReceivedEvent e, CancellationToken ct)
    {
        var reading = Reading.Create(e.DeviceId, e.PlaceId, e.RecordedAt, e.FlowLpm, e.VolumeLiters, e.TotalLiters, e.ReceivedAt);
        return store.AddAsync(reading, ct);
    }
}
