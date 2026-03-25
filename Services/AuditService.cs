using LMS.Api.Data;
using LMS.Api.Data.Entities;
using System.Security.Claims;

namespace LMS.Api.Services;

public sealed class AuditService(LmsDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task LogAsync(
        string action,
        string entityName,
        string entityId,
        string? changes = null,
        CancellationToken ct = default)
    {
        var userIdString = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? userId = Guid.TryParse(userIdString, out var guid) ? guid : null;

        var log = new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Changes = changes,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(log);
        await dbContext.SaveChangesAsync(ct);
    }
}
