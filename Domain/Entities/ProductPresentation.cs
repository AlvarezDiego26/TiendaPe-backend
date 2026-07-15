using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("product_presentations")]
[Index("ProductId", Name = "idx_product_presentations_product")]
public sealed class ProductPresentation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("unit_label")]
    public string UnitLabel { get; set; } = null!;

    [Column("quantity_in_base_unit")]
    [Precision(14, 3)]
    public decimal QuantityInBaseUnit { get; set; }

    [Column("purchase_enabled")]
    public bool PurchaseEnabled { get; set; }

    [Column("sale_enabled")]
    public bool SaleEnabled { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("purchase_cost")]
    [Precision(12, 2)]
    public decimal? PurchaseCost { get; set; }

    [Column("sale_price")]
    [Precision(12, 2)]
    public decimal? SalePrice { get; set; }

    [Column("wholesale_price")]
    [Precision(12, 2)]
    public decimal? WholesalePrice { get; set; }

    [Column("suggested_price")]
    [Precision(12, 2)]
    public decimal? SuggestedPrice { get; set; }

    [Column("profit_margin_percent")]
    [Precision(8, 2)]
    public decimal? ProfitMarginPercent { get; set; }

    [Column("is_default_purchase")]
    public bool IsDefaultPurchase { get; set; }

    [Column("is_default_sale")]
    public bool IsDefaultSale { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("Presentations")]
    public Product Product { get; set; } = null!;
}
