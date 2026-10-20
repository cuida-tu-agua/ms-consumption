using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyWater.Consumption.Infrastructure.Persistence.Entities;

[Table("consumption_goals", Schema = "goals")]
public sealed class GoalEntity
{
    [Key, Column("id")] public Guid Id { get; set; }
    [Column("place_id")] public Guid PlaceId { get; set; }
    [Column("month_start")] public DateOnly MonthStart { get; set; }
    [Column("target_liters", TypeName = "decimal(18,3)")] public decimal TargetLiters { get; set; }
    [Column("source"), MaxLength(15)] public string Source { get; set; } = "MANUAL";
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
