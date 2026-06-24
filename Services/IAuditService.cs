using LMS.Api.Data.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string entityId,
        string? changes = null,
        CancellationToken ct = default);

    Task<List<AuditLog>> GetLogsAsync(
        string? entityName = null,
        string? entityId = null,
        string? action = null,
        Guid? userId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);
}

