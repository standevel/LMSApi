using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class ExecuteDynamicReportEndpoint : ApiEndpoint<DynamicReportQueryRequest, DynamicReportResultDto>
{
    private readonly IDynamicReportService _reportService;

    public ExecuteDynamicReportEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Post("reports/builder/query");
        Policies(LmsPolicies.ReportBuilderAccess);
        Tags("Reporting - Dynamic Builder");
    }

    public override async Task HandleAsync(DynamicReportQueryRequest req, CancellationToken ct)
    {
        if (User.IsInRole(LmsRoles.Student))
        {
            await SendForbiddenAsync(ct);
            return;
        }

        try
        {
            var result = await _reportService.ExecuteQueryAsync(User, req, ct);
            await SendSuccessAsync(result, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFailureAsync(403, ex.Message, "FORBIDDEN", ex.Message, ct);
        }
        catch (ArgumentException ex)
        {
            await SendFailureAsync(400, ex.Message, "BAD_REQUEST", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "An error occurred while generating the report.", "SERVER_ERROR", ex.Message, ct);
        }
    }
}
