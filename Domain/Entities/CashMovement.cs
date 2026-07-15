using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("cash_movements")]
[Index("CashSessionId", "OccurredAt", Name = "idx_cash_movements_session", IsDescending = new[] { false, true })]
public sealed class CashMovement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("cash_session_id")]
    public Guid? CashSessionId { get; set; }

    [Column("type")]
    public string Type { get; set; } = null!;

    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "cash";

    [Column("amount")]
    [Precision(12, 2)]
    public decimal Amount { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("reference_table")]
    public string? ReferenceTable { get; set; }

    [Column("reference_id")]
    public Guid? ReferenceId { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CashSessionId")]
    public CashSession? CashSession { get; set; }
}
