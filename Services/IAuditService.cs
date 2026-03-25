namespace LMS.Api.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string entityId,
        string? changes = null,
        CancellationToken ct = default);
}
