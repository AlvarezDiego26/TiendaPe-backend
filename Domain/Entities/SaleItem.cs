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

    [ForeignKey("ProductId")]
    [InverseProperty("SaleItems")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SaleId")]
    [InverseProperty("SaleItems")]
    public virtual Sale Sale { get; set; } = null!;
}
