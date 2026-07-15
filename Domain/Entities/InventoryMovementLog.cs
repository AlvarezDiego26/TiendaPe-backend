using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("inventory_movement_logs")]
[Index("ProductId", "OccurredAt", Name = "idx_inventory_movement_logs_product", IsDescending = new[] { false, true })]
[Index("Reason", "OccurredAt", Name = "idx_inventory_movement_logs_reason", IsDescending = new[] { false, true })]
public sealed class InventoryMovementLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("presentation_id")]
    public Guid? PresentationId { get; set; }

    [Column("reason", TypeName = "inventory_movement_reason")]
    public InventoryMovementReason Reason { get; set; }

    [Column("quantity_input")]
    [Precision(14, 3)]
    public decimal QuantityInput { get; set; }

    [Column("input_unit")]
    public string InputUnit { get; set; } = null!;

    [Column("quantity_base")]
    [Precision(14, 3)]
    public decimal QuantityBase { get; set; }

    [Column("unit_cost_base")]
    [Precision(12, 4)]
    public decimal? UnitCostBase { get; set; }

    [Column("total_cost")]
    [Precision(12, 2)]
    public decimal? TotalCost { get; set; }

    [Column("reference_table")]
    public string? ReferenceTable { get; set; }

    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("InventoryMovementLogs")]
    public Product Product { get; set; } = null!;

    [ForeignKey("PresentationId")]
    public ProductPresentation? Presentation { get; set; }
}
