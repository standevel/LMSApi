namespace LMS.Api.Data.Entities;

public enum PermissionEffect
{
    Grant = 1,
    Revoke = 2
}

public sealed class UserPermission
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
    public PermissionEffect Effect { get; set; }
    public string? Reason { get; set; }
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }

    public AppUser User { get; set; } = null!;
    public AppPermission Permission { get; set; } = null!;
}
