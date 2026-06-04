using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Courses;

public sealed class CourseCatalogImportEndpoint : ApiEndpointWithoutRequest<CourseCatalogImportPreview>
{
    private readonly ICourseCatalogImportService _importService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAcademicProgramRepository _programRepository;
    private readonly ICurriculumRepository _curriculumRepository;

    public CourseCatalogImportEndpoint(
        ICourseCatalogImportService importService,
        ICurrentUserContext currentUserContext,
        IAcademicProgramRepository programRepository,
        ICurriculumRepository curriculumRepository)
    {
        _importService = importService;
        _currentUserContext = currentUserContext;
        _programRepository = programRepository;
        _curriculumRepository = curriculumRepository;
    }

public override void Configure()
{
    Post("course-catalog/upload");
    Tags("CourseCatalog");
    AllowAnonymous();
}

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Check authentication
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in to access this resource.", ct);
            return;
        }

        // Check roles
        var userRoles = HttpContext.User.Claims
            .Where(c => c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
            .Select(c => c.Value)
            .ToList();

        var allowedRoles = new[] { "Admin", "SuperAdmin", "Dean", "HOD" };
        if (!userRoles.Any(r => allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            await SendFailureAsync(403, "Forbidden", "FORBIDDEN", "You do not have permission to upload course catalogs.", ct);
            return;
        }

        // Get the uploaded file
        var file = HttpContext.Request.Form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
        {
            await SendFailureAsync(400, "BadRequest", "NO_FILE", "No file uploaded. Please select a .docx file.", ct);
            return;
        }

        // Validate file type
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (fileExtension != ".docx")
        {
            await SendFailureAsync(400, "BadRequest", "INVALID_FILE_TYPE", "Only .docx files are supported.", ct);
            return;
        }

        // Get program IDs from query string (supports both single 'programId' and multi 'programIds')
        Guid? programId = null;
        var programIds = new List<Guid>();

        // Check for multi-program query parameter
        var programIdsStr = HttpContext.Request.Query["programIds"];
        if (!string.IsNullOrEmpty(programIdsStr.ToString()))
        {
            programIds = programIdsStr.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => Guid.TryParse(s, out _))
                .Select(s => Guid.Parse(s))
                .ToList();
        }

        // Fall back to single programId if no programIds provided
        if (programIds.Count == 0 && Guid.TryParse(HttpContext.Request.Query["programId"].ToString(), out var singleProgramId))
        {
            programId = singleProgramId;
            programIds = new List<Guid> { singleProgramId };
        }

        // If no programs specified, the import will be college-wide (program names detected from file content)
        // This allows importing entire college courses not just per program

        // Get academic session ID from query string (optional)
        var sessionIdStr = HttpContext.Request.Query["sessionId"];
        Guid? academicSessionId = null;
        if (!string.IsNullOrEmpty(sessionIdStr.ToString()) && Guid.TryParse(sessionIdStr.ToString(), out var sessionId))
            academicSessionId = sessionId;

        // Read file stream
        using var stream = file.OpenReadStream();
        var fileName = file.FileName;

        // Parse the document
        var preview = await _importService.UploadAndParseAsync(stream, fileName, programId, programIds, academicSessionId, ct);

        // Also fetch available curricula for the first program (if any)
        if (programIds.Count > 0)
        {
            var firstProgramId = programIds[0];
            var curricula = await _curriculumRepository.GetByProgramIdAsync(firstProgramId, ct);
            // Note: curriculum fetching is per-program; for multi-program imports, curricula are shown per-program in the UI
        }

        await SendSuccessAsync(preview, ct);
    }
}

// Endpoint for getting preview
public sealed class CourseCatalogPreviewEndpoint : ApiEndpointWithoutRequest<CourseCatalogImportPreview>
{
    private readonly ICourseCatalogImportService _importService;

    public CourseCatalogPreviewEndpoint(ICourseCatalogImportService importService)
    {
        _importService = importService;
    }

public override void Configure()
{
    Get("course-catalog/preview/{uploadId:guid}");
    Tags("CourseCatalog");
    AllowAnonymous();
}

    public override async Task HandleAsync(CancellationToken ct)
    {
        var uploadId = Route<Guid>("uploadId");

        try
        {
            var preview = _importService.GetPreview(uploadId);
            await SendSuccessAsync(preview, ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "NotFound", "UPLOAD_NOT_FOUND", $"Upload {uploadId} not found.", ct);
        }
    }
}

// Endpoint for applying the import
public sealed class CourseCatalogApplyEndpoint : ApiEndpoint<ApplyCourseCatalogImportRequest, CourseCatalogImportResult>
{
    private readonly ICourseCatalogImportService _importService;

    public CourseCatalogApplyEndpoint(ICourseCatalogImportService importService)
    {
        _importService = importService;
    }

public override void Configure()
{
    Post("course-catalog/apply/{uploadId:guid}");
    Tags("CourseCatalog");
    AllowAnonymous();
}

    public override async Task HandleAsync(ApplyCourseCatalogImportRequest req, CancellationToken ct)
    {
        var uploadId = Route<Guid>("uploadId");

        // Check authentication
        if (HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Please log in.", ct);
            return;
        }

        try
        {
            var result = await _importService.ApplyImportAsync(
                req.UploadId,
                req.ProgramId,
                req.ProgramIds ?? Enumerable.Empty<Guid>(),
                req.CurriculumId,
                req.CurriculumName,
                req.AcademicSessionId,
                ct);

            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "NotFound", "UPLOAD_NOT_FOUND", $"Upload {uploadId} not found.", ct);
        }
    }
}
