namespace LMS.Api.Data.Entities;

public sealed class AppPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserPermission> UserPermissions { get; set; } = [];
}
