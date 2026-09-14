using ErrorOr;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public interface IParentAuthorizationService
{
    /// <summary>
    /// Returns true when the given parent is permitted to view the student's data for <paramref name="category"/>.
    /// Returns a Forbidden error otherwise (not linked, category disabled, or consent denied).
    /// </summary>
    Task<ErrorOr<bool>> CanAccessAsync(Guid parentId, Guid studentId, ParentAccessCategory category, CancellationToken ct = default);
}
