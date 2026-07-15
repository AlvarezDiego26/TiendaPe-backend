using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("suppliers")]
[Index("IsActive", "Name", Name = "idx_suppliers_active")]
public sealed class Supplier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("company")]
    public string? Company { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("last_purchase_at")]
    public DateTime? LastPurchaseAt { get; set; }

    [Column("total_purchased")]
    [Precision(12, 2)]
    public decimal TotalPurchased { get; set; }

    [Column("pending_balance")]
    [Precision(12, 2)]
    public decimal PendingBalance { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("SupplierEntity")]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
