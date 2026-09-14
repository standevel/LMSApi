using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class GetReportDatasetsEndpoint : ApiEndpointWithoutRequest<List<ReportDatasetMetadataDto>>
{
    private readonly IDynamicReportService _reportService;

    public GetReportDatasetsEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Get("reports/builder/datasets");
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

        var datasets = await _reportService.GetAvailableDatasetsAsync(User, ct);
        await SendSuccessAsync(datasets, ct);
    }
}
