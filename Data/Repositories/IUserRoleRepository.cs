namespace LMS.Api.Data.Repositories;

public interface IUserRoleRepository
{
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
    Task<bool> AssignmentExistsAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task AssignAsync(Guid userId, Guid roleId, DateTime assignedUtc, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
