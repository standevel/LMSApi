using LMS.Api.Data.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Services;

public interface IAuditService
{
    Task LogAsync(
        AuditLogEntry entry,
        CancellationToken ct = default);

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

public sealed class AuditLogEntry
{
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Changes { get; set; }
    public Guid? UserId { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }
    public int? StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestContentType { get; set; }
    public string? RequestBodyJson { get; set; }
}
