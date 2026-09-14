using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Services.Reporting;

public interface IDynamicReportService
{
    Task<List<ReportDatasetMetadataDto>> GetAvailableDatasetsAsync(ClaimsPrincipal user, CancellationToken ct = default);

    Task<DynamicReportResultDto> ExecuteQueryAsync(ClaimsPrincipal user, DynamicReportQueryRequest request, CancellationToken ct = default);

    Task<(byte[] Content, string ContentType, string FileName)> ExportReportAsync(ClaimsPrincipal user, ExportDynamicReportRequest request, CancellationToken ct = default);

    Task<List<DynamicReportTemplateDto>> GetTemplatesAsync(ClaimsPrincipal user, string? datasetId = null, CancellationToken ct = default);

    Task<DynamicReportTemplateDto> SaveTemplateAsync(ClaimsPrincipal user, SaveReportTemplateRequest request, CancellationToken ct = default);

    Task<bool> DeleteTemplateAsync(ClaimsPrincipal user, Guid templateId, CancellationToken ct = default);
}
