using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Gradebook;

public sealed class DownloadGradebookTemplateEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly IGradebookService _gradebookService;

    public DownloadGradebookTemplateEndpoint(IGradebookService gradebookService)
    {
        _gradebookService = gradebookService;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/template");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");

        var result = await _gradebookService.GenerateExcelTemplateAsync(offeringId, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        var template = result.Value;
        
        HttpContext.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{template.FileName}\"";
        HttpContext.Response.ContentType = template.ContentType;
        
        await HttpContext.Response.Body.WriteAsync(template.FileContent, ct);
        await HttpContext.Response.CompleteAsync();
    }
}

public sealed class UploadGradesExcelEndpoint : ApiEndpointWithoutRequest<GradeUploadResultDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public UploadGradesExcelEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("gradebook/courses/{offeringId:guid}/upload");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        var offeringId = Route<Guid>("offeringId");
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await _gradebookService.BulkUploadGradesAsync(offeringId, file, userId.Value, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Grades uploaded successfully");
    }
}

public sealed class MigrateClassterResultsEndpoint : ApiEndpointWithoutRequest<GradeUploadResultDto>
{
    private readonly IGradebookService _gradebookService;
    private readonly ICurrentUserContext _currentUserContext;

    public MigrateClassterResultsEndpoint(IGradebookService gradebookService, ICurrentUserContext currentUserContext)
    {
        _gradebookService = gradebookService;
        _currentUserContext = currentUserContext;
    }

    public override void Configure()
    {
        Post("gradebook/migrate-classter");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await _currentUserContext.GetUserIdAsync(ct);

        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        if (!HttpContext.Request.Form.TryGetValue("academicSessionId", out var sessionValues) || 
            !Guid.TryParse(sessionValues.FirstOrDefault(), out var academicSessionId))
        {
            await SendFailureAsync(400, "Missing academicSessionId", "MISSING_SESSION", "Academic session is required", ct);
            return;
        }

        if (!HttpContext.Request.Form.TryGetValue("courseId", out var courseValues) || 
            !Guid.TryParse(courseValues.FirstOrDefault(), out var courseId))
        {
            await SendFailureAsync(400, "Missing courseId", "MISSING_COURSE", "Course is required", ct);
            return;
        }

        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file == null)
        {
            await SendFailureAsync(400, "No file uploaded", "FILE_REQUIRED", "Please upload an Excel file", ct);
            return;
        }

        var result = await _gradebookService.MigrateClassterGradesAsync(academicSessionId, courseId, file, userId.Value, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Classter migration completed successfully");
    }
}
