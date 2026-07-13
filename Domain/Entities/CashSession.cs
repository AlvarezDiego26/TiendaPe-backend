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

    [ForeignKey("OpenedById")]
    [InverseProperty("OpenedCashSessions")]
    public virtual User? OpenedBy { get; set; }

    [InverseProperty("CashSession")]
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
