using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;

namespace LMS.Api.Services;

public sealed class AuditService(
    LmsDbContext dbContext,
    ICurrentUserContext currentUserContext) : IAuditService
{
    public async Task LogAsync(
        string action,
        string entityName,
        string entityId,
        string? changes = null,
        CancellationToken ct = default)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);

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
        catch (Exception ex)
        {
            // Audit failures must not break the calling business operation
            Console.WriteLine($"[Audit] Failed to write audit log for {entityName}/{entityId}: {ex.Message}");
        }
    }
}
