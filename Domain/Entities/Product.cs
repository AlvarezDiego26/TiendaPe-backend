using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("products")]
[Index("IsActive", Name = "idx_products_active")]
public partial class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("category")]
    public string Category { get; set; } = null!;

    [Column("internal_code")]
    public string? InternalCode { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    [Column("brand")]
    public string? Brand { get; set; }

    [Column("presentation")]
    public string? Presentation { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("supplier")]
    public string? Supplier { get; set; }

    [Column("purchase_price")]
    [Precision(12, 2)]
    public decimal PurchasePrice { get; set; }

    [Column("sale_price")]
    [Precision(12, 2)]
    public decimal SalePrice { get; set; }

    [Column("wholesale_price")]
    [Precision(12, 2)]
    public decimal? WholesalePrice { get; set; }

    [Column("stock")]
    public int Stock { get; set; }

    [Column("minimum_stock")]
    public int MinimumStock { get; set; }

    [Column("expiration_date")]
    public DateTime? ExpirationDate { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Product")]
    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    [InverseProperty("Product")]
    public virtual ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();

    [InverseProperty("Product")]
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}
