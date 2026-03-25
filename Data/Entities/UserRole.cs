namespace LMS.Api.Data.Entities;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedUtc { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}
