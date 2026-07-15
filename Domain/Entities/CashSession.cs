using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("cash_sessions")]
public partial class CashSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("opened_at")]
    public DateTime OpenedAt { get; set; }

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("opening_amount")]
    [Precision(12, 2)]
    public decimal OpeningAmount { get; set; }

    [Column("expected_amount")]
    [Precision(12, 2)]
    public decimal? ExpectedAmount { get; set; }

    [Column("counted_amount")]
    [Precision(12, 2)]
    public decimal? CountedAmount { get; set; }

    [Column("difference")]
    [Precision(12, 2)]
    public decimal? Difference { get; set; }

    [Column("cash_sales")]
    [Precision(12, 2)]
    public decimal CashSales { get; set; }

    [Column("yape_sales")]
    [Precision(12, 2)]
    public decimal YapeSales { get; set; }

    [Column("plin_sales")]
    [Precision(12, 2)]
    public decimal PlinSales { get; set; }

    [Column("transfer_sales")]
    [Precision(12, 2)]
    public decimal TransferSales { get; set; }

    [Column("supplier_payments")]
    [Precision(12, 2)]
    public decimal SupplierPayments { get; set; }

    [Column("personal_withdrawals")]
    [Precision(12, 2)]
    public decimal PersonalWithdrawals { get; set; }

    [Column("final_cash")]
    [Precision(12, 2)]
    public decimal? FinalCash { get; set; }

    [Column("opened_by")]
    public Guid? OpenedById { get; set; }

    [Column("closed_by")]
    public Guid? ClosedById { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ClosedById")]
    [InverseProperty("ClosedCashSessions")]
    public virtual User? ClosedBy { get; set; }

    [InverseProperty("CashSession")]
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    [InverseProperty("CashSession")]
    public virtual ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();

    [ForeignKey("OpenedById")]
    [InverseProperty("OpenedCashSessions")]
    public virtual User? OpenedBy { get; set; }

    [InverseProperty("CashSession")]
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
