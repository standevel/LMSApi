using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services.Reporting;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.Reporting.DynamicReports;

public sealed class ExportDynamicReportEndpoint : Endpoint<ExportDynamicReportRequest>
{
    private readonly IDynamicReportService _reportService;

    public ExportDynamicReportEndpoint(IDynamicReportService reportService)
    {
        _reportService = reportService;
    }

    public override void Configure()
    {
        Post("reports/builder/export");
        Policies(LmsPolicies.ReportBuilderAccess);
        Tags("Reporting - Dynamic Builder");
    }

    public override async Task HandleAsync(ExportDynamicReportRequest req, CancellationToken ct)
    {
        if (User.IsInRole(LmsRoles.Student))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var (content, contentType, fileName) = await _reportService.ExportReportAsync(User, req, ct);
            await Send.BytesAsync(content, fileName: fileName, contentType: contentType, cancellation: ct);
        }
        catch (UnauthorizedAccessException)
        {
            await Send.ForbiddenAsync(ct);
        }
        catch (ArgumentException)
        {
            await Send.ResultAsync(TypedResults.StatusCode(400));
        }
        catch (Exception)
        {
            await Send.ResultAsync(TypedResults.StatusCode(500));
        }
    }
}
