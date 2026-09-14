using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class SaveTemplateEndpoint : ApiEndpoint<SaveReportTemplateRequest, DynamicReportTemplateDto>
{
    private readonly IDynamicReportService _reportService;

    public SaveTemplateEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Post("reports/builder/templates");
        Policies(LmsPolicies.ReportBuilderAccess);
        Tags("Reporting - Dynamic Builder");
    }

    public override async Task HandleAsync(SaveReportTemplateRequest req, CancellationToken ct)
    {
        if (User.IsInRole(LmsRoles.Student))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            await SendFailureAsync(400, "Template name is required.", "VALIDATION_ERROR", "Template name cannot be empty.", ct);
            return;
        }

        try
        {
            var saved = await _reportService.SaveTemplateAsync(User, req, ct);
            await SendCreatedAsync(saved, ct, "Report template saved successfully.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Failed to save template.", "SERVER_ERROR", ex.Message, ct);
        }
    }
}
