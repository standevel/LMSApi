using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class GetSavedTemplatesEndpoint : ApiEndpointWithoutRequest<List<DynamicReportTemplateDto>>
{
    private readonly IDynamicReportService _reportService;

    public GetSavedTemplatesEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Get("reports/builder/templates");
        Policies(LmsPolicies.ReportBuilderAccess);
        Tags("Reporting - Dynamic Builder");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (User.IsInRole(LmsRoles.Student))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        var datasetId = Query<string>("datasetId", false);
        var templates = await _reportService.GetTemplatesAsync(User, datasetId, ct);
        await SendSuccessAsync(templates, ct);
    }
}
