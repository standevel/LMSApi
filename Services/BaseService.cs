using ErrorOr;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Services;

public abstract class BaseService(IAuditService auditService)
{
    protected readonly IAuditService AuditService = auditService;

    protected async Task LogActionAsync(string action, string entityName, string entityId, string details, CancellationToken ct = default)
    {
        await AuditService.LogAsync(action, entityName, entityId, details, ct);
    }
}
