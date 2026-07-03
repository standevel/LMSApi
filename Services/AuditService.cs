using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Services;

public sealed class AuditService(
    LmsDbContext dbContext,
    ICurrentUserContext currentUserContext) : IAuditService
{
    public async Task LogAsync(
        AuditLogEntry entry,
        CancellationToken ct = default)
    {
        try
        {
            var resolvedUserId = entry.UserId ?? await currentUserContext.GetUserIdAsync(ct);

            var log = new AuditLog
            {
                Action = entry.Action,
                EntityName = entry.EntityName,
                EntityId = entry.EntityId,
                Changes = entry.Changes,
                UserId = resolvedUserId,
                Timestamp = DateTime.UtcNow,
                HttpMethod = entry.HttpMethod,
                Path = entry.Path,
                QueryString = entry.QueryString,
                StatusCode = entry.StatusCode,
                IpAddress = entry.IpAddress,
                UserAgent = entry.UserAgent,
                CorrelationId = entry.CorrelationId,
                RequestContentType = entry.RequestContentType,
                RequestBodyJson = entry.RequestBodyJson
            };

            dbContext.AuditLogs.Add(log);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit failures must not break the calling business operation
            Console.WriteLine($"[Audit] Failed to write audit log for {entry.EntityName}/{entry.EntityId}: {ex.Message}");
        }
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string entityId,
        string? changes = null,
        CancellationToken ct = default)
    {
        await LogAsync(new AuditLogEntry
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Changes = changes
        }, ct);
    }

    public async Task<List<AuditLog>> GetLogsAsync(
        string? entityName = null,
        string? entityId = null,
        string? action = null,
        Guid? userId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = dbContext.AuditLogs
            .Include(x => x.User)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(x => x.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(x => x.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
