using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class GetCourseGradebookAuditHistoryEndpoint : ApiEndpointWithoutRequest<List<GradebookAuditHistoryDto>>
{
    private readonly IGradebookService _gradebookService;

    public GetCourseGradebookAuditHistoryEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/audit-history");
        Roles(LmsRoles.SuperAdmin, LmsRoles.Admin);
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (!userRoles.Any(r => r.Equals(LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) || r.Equals(LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only administrators can access gradebook audit history.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var result = await _gradebookService.GetAuditHistoryAsync(offeringId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Audit history retrieved successfully");
    }
}

public sealed class PreviewGradebookSnapshotEndpoint : ApiEndpointWithoutRequest<GradebookSnapshotPreviewDto>
{
    private readonly IGradebookService _gradebookService;

    public PreviewGradebookSnapshotEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/audit-history/{auditLogId:guid}/preview");
        Roles(LmsRoles.SuperAdmin, LmsRoles.Admin);
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (!userRoles.Any(r => r.Equals(LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) || r.Equals(LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only administrators can preview gradebook snapshots.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var auditLogId = Route<Guid>("auditLogId");

        var result = await _gradebookService.PreviewSnapshotAsync(offeringId, auditLogId, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Snapshot preview generated successfully");
    }
}

public sealed class RestoreGradebookSnapshotEndpoint : ApiEndpoint<RestoreGradesFromSnapshotRequest, RestoreGradesResultDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public RestoreGradebookSnapshotEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("gradebook/courses/{offeringId:guid}/audit-history/{auditLogId:guid}/restore");
        Roles(LmsRoles.SuperAdmin, LmsRoles.Admin);
        Tags("Gradebook");
    }

    public override async Task HandleAsync(RestoreGradesFromSnapshotRequest req, CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" || c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (!userRoles.Any(r => r.Equals(LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) || r.Equals(LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "Only administrators can restore historical grade snapshots.", ct);
            return;
        }

        var userId = await _currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var auditLogId = Route<Guid>("auditLogId");

        var result = await _gradebookService.RestoreGradesFromSnapshotAsync(offeringId, auditLogId, req, userId.Value, ct);

        if (result.IsError)
        {
            var error = result.FirstError;
            var statusCode = error.Type switch
            {
                ErrorType.NotFound => 404,
                ErrorType.Forbidden => 403,
                ErrorType.Validation => 400,
                _ => 400
            };
            await SendFailureAsync(statusCode, error.Description, error.Code, error.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, result.Value.Message);
    }
}
