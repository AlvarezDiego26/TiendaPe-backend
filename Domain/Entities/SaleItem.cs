using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("sale_items")]
[Index("SaleId", Name = "idx_sale_items_sale_id")]
public partial class SaleItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("sale_id")]
    public Guid SaleId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("presentation_id")]
    public Guid? PresentationId { get; set; }

    [Column("product_name")]
    public string ProductName { get; set; } = null!;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("unit_price")]
    [Precision(12, 2)]
    public decimal UnitPrice { get; set; }

    [Column("subtotal")]
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    [Column("quantity_base")]
    [Precision(14, 3)]
    public decimal QuantityBase { get; set; }

    [Column("input_unit")]
    public string? InputUnit { get; set; }

    [Column("unit_cost_base")]
    [Precision(12, 4)]
    public decimal? UnitCostBase { get; set; }

    [Column("profit")]
    [Precision(12, 2)]
    public decimal? Profit { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("SaleItems")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("PresentationId")]
    public ProductPresentation? Presentation { get; set; }

    [ForeignKey("SaleId")]
    [InverseProperty("SaleItems")]
    public virtual Sale Sale { get; set; } = null!;
}
