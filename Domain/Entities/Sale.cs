using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("sales")]
[Index("CashSessionId", Name = "idx_sales_cash_session")]
[Index("OccurredAt", Name = "idx_sales_occurred_at")]
public partial class Sale
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("payment_method", TypeName = "payment_method")]
    public PaymentMethod PaymentMethod { get; set; }

    [Column("total")]
    [Precision(12, 2)]
    public decimal Total { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("cash_session_id")]
    public Guid? CashSessionId { get; set; }

    [ForeignKey("CashSessionId")]
    [InverseProperty("Sales")]
    public virtual CashSession? CashSession { get; set; }

    [InverseProperty("Sale")]
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}
