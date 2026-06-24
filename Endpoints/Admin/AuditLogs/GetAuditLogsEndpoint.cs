using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Endpoints;
using LMS.Api.Security;
using LMS.Api.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admin.AuditLogs;

public sealed class GetAuditLogsEndpoint(IAuditService auditService)
    : ApiEndpoint<GetAuditLogsRequest, List<AuditLogDto>>
{
    public override void Configure()
    {
        Get("admin/audit-logs");
        Policies(PermissionPolicy.Build(LmsPermissions.AccessManage));
        Tags("Administration");
        Summary(s =>
        {
            s.Summary = "Get system activity logs";
            s.Description = "Retrieves a paginated list of audit/activity logs matching filter criteria.";
            s.Responses[200] = "Successfully retrieved logs.";
            s.Responses[401] = "Unauthorized access.";
            s.Responses[403] = "Forbidden access.";
        });
    }

    public override async Task HandleAsync(GetAuditLogsRequest req, CancellationToken ct)
    {
        var logs = await auditService.GetLogsAsync(
            req.EntityName,
            req.EntityId,
            req.Action,
            req.UserId,
            req.Page,
            req.PageSize,
            ct);

        var dtos = logs.Select(x => new AuditLogDto(
            x.Id,
            x.Action,
            x.EntityName,
            x.EntityId,
            x.Changes,
            x.UserId,
            x.User?.DisplayName ?? x.User?.Email ?? "System",
            x.Timestamp)).ToList();

        await SendSuccessAsync(dtos, ct);
    }
}
