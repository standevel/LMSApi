using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class DeleteTemplateRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteTemplateEndpoint : ApiEndpoint<DeleteTemplateRequest, bool>
{
    private readonly IDynamicReportService _reportService;

    public DeleteTemplateEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Delete("reports/builder/templates/{Id}");
        Policies(LmsPolicies.ReportBuilderAccess);
        Tags("Reporting - Dynamic Builder");
    }

    public override async Task HandleAsync(DeleteTemplateRequest req, CancellationToken ct)
    {
        if (User.IsInRole(LmsRoles.Student))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        try
        {
            var success = await _reportService.DeleteTemplateAsync(User, req.Id, ct);
            if (!success)
            {
                await SendFailureAsync(404, "Template not found.", "NOT_FOUND", "No template found with the specified ID.", ct);
                return;
            }

            await SendSuccessAsync(true, ct, "Template deleted successfully.");
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFailureAsync(403, ex.Message, "FORBIDDEN", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to delete template.", "SERVER_ERROR", ex.Message, ct);
        }
    }
}
