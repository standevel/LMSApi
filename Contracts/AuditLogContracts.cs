using System;

namespace LMS.Api.Contracts;

public record AuditLogDto(
    Guid Id,
    string Action,
    string EntityName,
    string EntityId,
    string? Changes,
    Guid? UserId,
    string? PerformedBy,
    DateTime Timestamp,
    string? HttpMethod = null,
    string? Path = null,
    string? QueryString = null,
    int? StatusCode = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null,
    string? RequestContentType = null,
    string? RequestBodyJson = null);

public record GetAuditLogsRequest(
    string? EntityName = null,
    string? EntityId = null,
    string? Action = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 50);
