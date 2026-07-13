using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("inventory_movements")]
[Index("ProductId", "OccurredAt", Name = "idx_inventory_movements_product", IsDescending = new[] { false, true })]
public partial class InventoryMovement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("movement_type", TypeName = "inventory_movement_type")]
    public InventoryMovementType MovementType { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("InventoryMovements")]
    public virtual Product Product { get; set; } = null!;
}
