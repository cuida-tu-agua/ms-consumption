using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Consumption.Infrastructure.Persistence.Entities;

[Table("readings", Schema = "monitoring")]
public sealed class ReadingEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("device_id")] public Guid DeviceId { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("flow_rate_lph", TypeName = "decimal(18,3)")] public decimal FlowRateLph { get; set; }
    [Column("volume_liters", TypeName = "decimal(18,3)")] public decimal VolumeLiters { get; set; }
    [Column("cumulative_liters", TypeName = "decimal(18,3)")] public decimal CumulativeLiters { get; set; }
    [Column("recorded_at")] public DateTime RecordedAt { get; set; }
    [Column("received_at")] public DateTime ReceivedAt { get; set; }
}

[Table("consumption_hourly", Schema = "monitoring")]
public sealed class HourlyEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("hour_start")] public DateTime HourStart { get; set; }
    [Column("total_liters", TypeName = "decimal(18,3)")] public decimal TotalLiters { get; set; }
    [Column("reading_count")] public int ReadingCount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
