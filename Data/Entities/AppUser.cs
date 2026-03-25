namespace LMS.Api.Data.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntraObjectId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
}
