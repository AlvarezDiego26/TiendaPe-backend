using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TiendaPe.Domain.Entities;

[Table("users")]
[Index("Email", Name = "users_email_key", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("email")]
    public string Email { get; set; } = null!;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("ClosedBy")]
    public virtual ICollection<CashSession> ClosedCashSessions { get; set; } = new List<CashSession>();

    [InverseProperty("OpenedBy")]
    public virtual ICollection<CashSession> OpenedCashSessions { get; set; } = new List<CashSession>();
}
