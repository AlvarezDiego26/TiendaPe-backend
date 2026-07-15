using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("expenses")]
[Index("CashSessionId", Name = "idx_expenses_cash_session")]
[Index("OccurredAt", Name = "idx_expenses_occurred_at")]
[Index("IsRecurring", "DueDay", Name = "idx_expenses_recurring")]
public partial class Expense
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("category")]
    public string Category { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("amount")]
    [Precision(12, 2)]
    public decimal Amount { get; set; }

    [Column("payment_method", TypeName = "payment_method")]
    public PaymentMethod PaymentMethod { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("cash_session_id")]
    public Guid? CashSessionId { get; set; }

    [Column("is_recurring")]
    public bool IsRecurring { get; set; }

    [Column("due_day")]
    public short? DueDay { get; set; }

    [Column("supplier_id")]
    public Guid? SupplierId { get; set; }

    [Column("is_supplier_payment")]
    public bool IsSupplierPayment { get; set; }

    [Column("recurring_start")]
    public DateTime? RecurringStart { get; set; }

    [Column("recurring_end")]
    public DateTime? RecurringEnd { get; set; }

    [ForeignKey("CashSessionId")]
    [InverseProperty("Expenses")]
    public virtual CashSession? CashSession { get; set; }

    [ForeignKey("SupplierId")]
    public virtual Supplier? Supplier { get; set; }
}
