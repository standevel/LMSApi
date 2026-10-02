using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LMS.Api.Data.Entities;

public sealed class CafeteriaWalletAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public string Username { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0m;
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal? ParentDailySpendLimit { get; set; }
    [ConcurrencyCheck]
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CafeteriaWalletTransaction> Transactions { get; set; } = new List<CafeteriaWalletTransaction>();
}
