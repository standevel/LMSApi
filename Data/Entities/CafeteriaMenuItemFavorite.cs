using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Api.Data.Entities;

public sealed class CafeteriaMenuItemFavorite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(256)]
    public string Username { get; set; } = string.Empty;

    public Guid MenuItemId { get; set; }
    public CafeteriaMenuItem MenuItem { get; set; } = null!;

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
