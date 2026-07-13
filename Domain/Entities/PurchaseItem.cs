using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("purchase_items")]
[Index("PurchaseId", Name = "idx_purchase_items_purchase_id")]
public partial class PurchaseItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("purchase_id")]
    public Guid PurchaseId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_cost")]
    [Precision(12, 2)]
    public decimal UnitCost { get; set; }

    [Column("subtotal")]
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("PurchaseItems")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("PurchaseId")]
    [InverseProperty("PurchaseItems")]
    public virtual Purchase Purchase { get; set; } = null!;
}
