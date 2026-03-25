namespace LMS.Api.Data.Entities;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;

    public AppRole Role { get; set; } = null!;
    public AppPermission Permission { get; set; } = null!;
}
