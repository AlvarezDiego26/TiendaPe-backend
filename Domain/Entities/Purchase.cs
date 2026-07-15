using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("purchases")]
[Index("OccurredAt", Name = "idx_purchases_occurred_at")]
public partial class Purchase
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("supplier")]
    public string Supplier { get; set; } = null!;

    [Column("supplier_id")]
    public Guid? SupplierId { get; set; }

    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "cash";

    [Column("paid_amount")]
    [Precision(12, 2)]
    public decimal PaidAmount { get; set; }

    [Column("pending_amount")]
    [Precision(12, 2)]
    public decimal PendingAmount { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("total")]
    [Precision(12, 2)]
    public decimal Total { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Purchase")]
    public virtual ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();

    [ForeignKey("SupplierId")]
    public Supplier? SupplierEntity { get; set; }
}
