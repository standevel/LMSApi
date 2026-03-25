using LMS.Api.Data.Repositories;
using System.Security.Claims;

namespace LMS.Api.Security;

public interface ICurrentUserContext
{
    Task<Guid?> GetUserIdAsync(CancellationToken ct = default);
    string? GetEntraObjectId();
    string? GetSubject();
}

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor, IUserRepository userRepository) : ICurrentUserContext
{
    private const string UserIdItemKey = "CurrentUserId";

    public string? GetEntraObjectId() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("oid")
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

    public string? GetSubject() =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<Guid?> GetUserIdAsync(CancellationToken ct = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        if (httpContext.Items.TryGetValue(UserIdItemKey, out var existing) && existing is Guid cachedUserId)
        {
            return cachedUserId;
        }

        var entraObjectId = GetEntraObjectId();
        if (!string.IsNullOrWhiteSpace(entraObjectId))
        {
            var userIdByOid = await userRepository.GetIdByEntraObjectIdAsync(entraObjectId, ct);

            if (userIdByOid.HasValue)
            {
                httpContext.Items[UserIdItemKey] = userIdByOid.Value;
                return userIdByOid;
            }
        }

        var subject = GetSubject();
        if (!Guid.TryParse(subject, out var subjectUserId))
        {
            return null;
        }

        var userId = await userRepository.GetIdBySubjectAsync(subjectUserId, ct);

        if (userId.HasValue)
        {
            httpContext.Items[UserIdItemKey] = userId.Value;
        }

        return userId;
    }
}
