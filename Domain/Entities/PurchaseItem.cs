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

    [Column("presentation_id")]
    public Guid? PresentationId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_cost")]
    [Precision(12, 2)]
    public decimal UnitCost { get; set; }

    [Column("quantity_base")]
    [Precision(14, 3)]
    public decimal QuantityBase { get; set; }

    [Column("input_unit")]
    public string? InputUnit { get; set; }

    [Column("units_per_package")]
    [Precision(14, 3)]
    public decimal UnitsPerPackage { get; set; }

    [Column("total_cost")]
    [Precision(12, 2)]
    public decimal? TotalCost { get; set; }

    [Column("unit_cost_base")]
    [Precision(12, 4)]
    public decimal? UnitCostBase { get; set; }

    [Column("suggested_price")]
    [Precision(12, 2)]
    public decimal? SuggestedPrice { get; set; }

    [Column("profit_margin_percent")]
    [Precision(8, 2)]
    public decimal? ProfitMarginPercent { get; set; }

    [Column("subtotal")]
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("PurchaseItems")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("PresentationId")]
    public ProductPresentation? Presentation { get; set; }

    [ForeignKey("PurchaseId")]
    [InverseProperty("PurchaseItems")]
    public virtual Purchase Purchase { get; set; } = null!;
}
