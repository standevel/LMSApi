using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

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
        AllowAnonymous();
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

        Guid? uploadId = null;
        if (HttpContext.Request.Form.TryGetValue("uploadId", out var uploadIdValues) &&
            Guid.TryParse(uploadIdValues.FirstOrDefault(), out var parsedUploadId))
        {
            uploadId = parsedUploadId;
        }

        var result = await _gradebookService.MigrateClassterGradesAsync(academicSessionId, courseId, file, userId.Value, uploadId, ct);

        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct, "Classter migration completed successfully");
    }
}

public sealed class DownloadSenateResultEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly IGradebookService _gradebookService;
    private readonly LmsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;

    public DownloadSenateResultEndpoint(IGradebookService gradebookService, LmsDbContext dbContext, ICurrentUserContext currentUser)
    {
        _gradebookService = gradebookService;
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public override void Configure()
    {
        Get("gradebook/courses/{offeringId:guid}/senate-result");
        Roles("SuperAdmin", "Admin", "Dean");
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var offeringId = Route<Guid>("offeringId");
        var userId     = await _currentUser.GetUserIdAsync(ct);

        string? collegeName = null;

        // Admin/SuperAdmin: accept optional collegeId query param
        var collegeIdStr = Query<string?>("collegeId", isRequired: false);
        if (!string.IsNullOrWhiteSpace(collegeIdStr) && Guid.TryParse(collegeIdStr, out var collegeId))
        {
            var faculty = await _dbContext.Faculties.FindAsync([collegeId], ct);
            if (faculty != null)
                collegeName = $"{faculty.Label.ToUpper()} OF {faculty.Name.ToUpper()}";
        }
        // Dean: auto-resolve their own college
        else if (User.IsInRole("Dean") && userId.HasValue)
        {
            var deanFaculty = await _dbContext.Faculties
                .FirstOrDefaultAsync(f => f.DeanId == userId.Value, ct);
            if (deanFaculty != null)
                collegeName = $"{deanFaculty.Label.ToUpper()} OF {deanFaculty.Name.ToUpper()}";
        }

        var result = await _gradebookService.GenerateSenateResultTemplateAsync(offeringId, collegeName, ct);

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

public sealed class DownloadCollegeSenateResultEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly IGradebookService _gradebookService;
    private readonly LmsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;

    public DownloadCollegeSenateResultEndpoint(IGradebookService gradebookService, LmsDbContext dbContext, ICurrentUserContext currentUser)
    {
        _gradebookService = gradebookService;
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public override void Configure()
    {
        Get("gradebook/senate-result/college");
        Roles("SuperAdmin", "Admin", "Dean");
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await _currentUser.GetUserIdAsync(ct);
        Guid resolvedCollegeId = Guid.Empty;

        // Auto-resolve college if the user is a Dean
        if (User.IsInRole("Dean") && userId.HasValue)
        {
            var deanFaculty = await _dbContext.Faculties
                .FirstOrDefaultAsync(f => f.DeanId == userId.Value, ct);
            if (deanFaculty != null)
            {
                resolvedCollegeId = deanFaculty.Id;
            }
        }

        // Otherwise read collegeId query parameter
        if (resolvedCollegeId == Guid.Empty)
        {
            var collegeIdStr = Query<string>("collegeId", isRequired: true);
            if (!Guid.TryParse(collegeIdStr, out resolvedCollegeId))
            {
                await SendFailureAsync(400, "Invalid college ID", "INVALID_COLLEGE", "Please provide a valid college ID", ct);
                return;
            }
        }

        var sessionIdStr = Query<string>("academicSessionId", isRequired: true);
        var semesterStr = Query<string>("semester", isRequired: true);
        var levelIdStr = Query<string>("levelId", isRequired: true);

        if (!Guid.TryParse(sessionIdStr, out var sessionId) ||
            !Enum.TryParse<LMS.Api.Data.Enums.Semester>(semesterStr, ignoreCase: true, out var semester) ||
            !Guid.TryParse(levelIdStr, out var levelId))
        {
            await SendFailureAsync(400, "Invalid query parameters", "INVALID_PARAMS", "Please provide valid session, semester, and level IDs", ct);
            return;
        }

        var result = await _gradebookService.GenerateCollegeSenateResultAsync(sessionId, semester, resolvedCollegeId, levelId, ct);

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

public sealed class AutoImportClassterDataEndpoint : ApiEndpointWithoutRequest<object>
{
    private readonly IGradebookService _gradebookService;
    private readonly LmsDbContext _dbContext;

    public AutoImportClassterDataEndpoint(IGradebookService gradebookService, LmsDbContext dbContext)
    {
        _gradebookService = gradebookService;
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("gradebook/auto-import");
        AllowAnonymous();
        Tags("Gradebook");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var academicSessionId = Guid.Parse("cc64de5c-ddb7-4222-af07-e6a2a3ae3981");
        var userId = Guid.Parse("d5d33b21-5d7b-41e3-83bd-ec03a4131d31");

        // The path depends on where the app runs. Assume root is LMSApi directory
        var dataDir = System.IO.Path.Combine("..", "Classter Data");
        if (!System.IO.Directory.Exists(dataDir))
        {
            await SendFailureAsync(404, "Data directory not found", "NOT_FOUND", dataDir, ct);
            return;
        }

        var files = System.IO.Directory.GetFiles(dataDir, "*.xlsx");
        var results = new System.Collections.Generic.List<string>();

        foreach (var file in files)
        {
            var fileName = System.IO.Path.GetFileName(file);
            if (fileName.StartsWith(".~") || fileName.StartsWith("Students per Educational"))
                continue;

            var codeString = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (codeString.Contains("Examination", StringComparison.OrdinalIgnoreCase))
            {
                var idx = codeString.IndexOf("Examination", StringComparison.OrdinalIgnoreCase);
                codeString = codeString.Substring(0, idx).Trim();
            }

            var codeHyphensToSpaces = codeString.Replace("-", " ");
            var codeHyphensToEmpty = codeString.Replace("-", "");

            var course = await _dbContext.Courses.FirstOrDefaultAsync(c => 
                c.Code == codeString || c.Code == codeHyphensToSpaces || c.Code == codeHyphensToEmpty, ct);

            if (course == null)
            {
                results.Add($"[SKIPPED] Course not found: {fileName}");
                continue;
            }

            using var stream = new System.IO.FileStream(file, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

            var result = await _gradebookService.MigrateClassterGradesAsync(academicSessionId, course.Id, formFile, userId, null, ct);

            if (result.IsError)
            {
                results.Add($"[ERROR] {fileName}: {result.FirstError.Description}");
            }
            else
            {
                results.Add($"[SUCCESS] {fileName}");
            }
        }

        await SendSuccessAsync(new { Results = results }, ct);
    }
}
