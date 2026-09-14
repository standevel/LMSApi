using System;
using System.Collections.Generic;

namespace LMS.Api.Data.Entities;

public sealed class CafeteriaWalletAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 0m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CafeteriaWalletTransaction> Transactions { get; set; } = new List<CafeteriaWalletTransaction>();
}
