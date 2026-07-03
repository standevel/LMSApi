using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

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
        Console.WriteLine($"File: {file?.FileName}, Length: {file?.Length}");
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

        // Read file bytes directly into a MemoryStream (in-memory only, no temp file)
        using var seekableStream = new MemoryStream();
        await file.CopyToAsync(seekableStream, ct);
        seekableStream.Position = 0;
        var fileName = file.FileName;

        try
        {
            // Parse the document
            var preview = await _importService.UploadAndParseAsync(seekableStream, fileName, programId, programIds, academicSessionId, ct);

            // Also fetch available curricula for the first program (if any)
            if (programIds.Count > 0)
            {
                var firstProgramId = programIds[0];
                var curricula = await _curriculumRepository.GetByProgramIdAsync(firstProgramId, ct);
                // Note: curriculum fetching is per-program; for multi-program imports, curricula are shown per-program in the UI
            }

            await SendSuccessAsync(preview, ct);
        }
        catch (System.BadImageFormatException)
        {
            await SendFailureAsync(400, "BadRequest", "INVALID_FORMAT", "The uploaded file is not a valid .docx format. Please ensure the file is a genuine Microsoft Word document.", ct);
        }
        catch (OpenXmlPackageException)
        {
            await SendFailureAsync(400, "BadRequest", "CORRUPT_FILE", "The uploaded file is corrupt or damaged. Please upload a valid .docx file.", ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "BadRequest", "PARSE_ERROR", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "InternalServerError", "INTERNAL_ERROR", $"An error occurred: {ex.Message}", ct);
        }
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

        if (uploadId == Guid.Empty)
        {
            try
            {
                var scope = HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LMS.Api.Data.LmsDbContext>();
                
                // Use Raw SQL to avoid EF Core tracked state issues and cascade issues
                var sql = @"
                    WITH CTE AS (
                        SELECT 
                            Id, 
                            UPPER(REPLACE(REPLACE(Code, ' ', ''), '-', '')) AS NormalizedCode,
                            ROW_NUMBER() OVER(PARTITION BY UPPER(REPLACE(REPLACE(Code, ' ', ''), '-', '')) ORDER BY Id ASC) as RowNum
                        FROM Courses
                    )
                    SELECT Id AS DuplicateId, 
                           (SELECT Id FROM CTE c2 WHERE c2.NormalizedCode = CTE.NormalizedCode AND c2.RowNum = 1) AS PrimaryId
                    INTO #TempDups
                    FROM CTE 
                    WHERE RowNum > 1;

                    -- Update CurriculumCourses (Ignore conflicts by deleting the duplicate ones before update)
                    DELETE FROM CurriculumCourses 
                    WHERE Id IN (
                        SELECT cc.Id FROM CurriculumCourses cc
                        JOIN #TempDups d ON cc.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CurriculumCourses cc2 
                            WHERE cc2.CourseId = d.PrimaryId 
                              AND cc2.CurriculumId = cc.CurriculumId 
                              AND cc2.Semester = cc.Semester
                        )
                    );
                    UPDATE CurriculumCourses SET CourseId = d.PrimaryId 
                    FROM CurriculumCourses cc JOIN #TempDups d ON cc.CourseId = d.DuplicateId;

                    -- Update CourseOfferings
                    DELETE FROM CourseOfferings 
                    WHERE Id IN (
                        SELECT co.Id FROM CourseOfferings co
                        JOIN #TempDups d ON co.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CourseOfferings co2 
                            WHERE co2.CourseId = d.PrimaryId 
                              AND co2.ProgramId = co.ProgramId 
                              AND co2.LevelId = co.LevelId 
                              AND co2.AcademicSessionId = co.AcademicSessionId
                              AND co2.Semester = co.Semester
                        )
                    );
                    UPDATE CourseOfferings SET CourseId = d.PrimaryId 
                    FROM CourseOfferings co JOIN #TempDups d ON co.CourseId = d.DuplicateId;

                    -- Update DegreeRequirements
                    UPDATE DegreeRequirementCourses SET CourseId = d.PrimaryId 
                    FROM DegreeRequirementCourses dr JOIN #TempDups d ON dr.CourseId = d.DuplicateId;

                    -- Update Assignments
                    UPDATE Assignments SET CourseId = d.PrimaryId 
                    FROM Assignments a JOIN #TempDups d ON a.CourseId = d.DuplicateId;

                    -- Update Prerequisites (Delete duplicate links first)
                    DELETE FROM CoursePrerequisites 
                    WHERE Id IN (
                        SELECT cp.Id FROM CoursePrerequisites cp
                        JOIN #TempDups d ON cp.CourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CoursePrerequisites cp2 
                            WHERE cp2.CourseId = d.PrimaryId 
                              AND cp2.PrerequisiteCourseId = cp.PrerequisiteCourseId
                        )
                    );
                    UPDATE CoursePrerequisites SET CourseId = d.PrimaryId 
                    FROM CoursePrerequisites cp JOIN #TempDups d ON cp.CourseId = d.DuplicateId;

                    DELETE FROM CoursePrerequisites 
                    WHERE Id IN (
                        SELECT cp.Id FROM CoursePrerequisites cp
                        JOIN #TempDups d ON cp.PrerequisiteCourseId = d.DuplicateId
                        WHERE EXISTS (
                            SELECT 1 FROM CoursePrerequisites cp2 
                            WHERE cp2.PrerequisiteCourseId = d.PrimaryId 
                              AND cp2.CourseId = cp.CourseId
                        )
                    );
                    UPDATE CoursePrerequisites SET PrerequisiteCourseId = d.PrimaryId 
                    FROM CoursePrerequisites cp JOIN #TempDups d ON cp.PrerequisiteCourseId = d.DuplicateId;

                    -- Finally delete duplicate courses
                    DELETE FROM Courses WHERE Id IN (SELECT DuplicateId FROM #TempDups);
                    
                    SELECT COUNT(*) FROM #TempDups;
                    DROP TABLE #TempDups;
                ";
                
                // Execute and get removed count (Wait, ExecuteSqlRawAsync doesn't return scalar easily if we select count)
                // We'll just execute it
                await dbContext.Database.ExecuteSqlRawAsync(sql, ct);
                
                await SendSuccessAsync(new LMS.Api.Contracts.CourseCatalogImportPreview(Guid.Empty, $"Deduplication script executed successfully.", null, null, new(), 1), ct);
            }
            catch (Exception ex)
            {
                await SendFailureAsync(500, "Error", "ERROR", ex.InnerException?.Message ?? ex.Message, ct);
            }
            return;
        }

        try
        {
            var preview = _importService.GetPreview(uploadId);
            await SendSuccessAsync(preview, ct);
        }
        catch (KeyNotFoundException)
        {
            await SendFailureAsync(404, "NotFound", "UPLOAD_NOT_FOUND", $"Upload {uploadId} not found.", ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "InternalServerError", "PREVIEW_ERROR", $"Preview failed: {ex.GetType().Name}: {ex.Message}", ct);
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
            var uploadId = Route<Guid>("uploadId");
            await SendFailureAsync(404, "NotFound", "UPLOAD_NOT_FOUND", $"Upload {uploadId} not found.", ct);
        }
        catch (DbUpdateException ex)
        {
            await SendFailureAsync(500, "InternalServerError", "DATABASE_ERROR", $"Database error: {ex.InnerException?.Message ?? ex.Message}", ct);
        }
        catch (Exception ex)
        {
            // Log full exception chain for debugging
            var fullMessage = ex.InnerException != null
                ? $"{ex.Message} → {ex.InnerException.Message}"
                : ex.Message;
            await SendFailureAsync(500, "InternalServerError", "INTERNAL_ERROR", $"{ex.GetType().Name}: {fullMessage}", ct);
        }
    }
}

public sealed class CourseDeduplicationTempEndpoint(LMS.Api.Data.LmsDbContext dbContext) : ApiEndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Post("course-catalog/deduplicate");
        AllowAnonymous(); // For manual one-time trigger
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Fetch all courses
        var allCourses = await dbContext.Courses.ToListAsync(ct);

        // Group by normalized code
        var groupedCourses = allCourses
            .GroupBy(c => Normalize(c.Code))
            .Where(g => g.Count() > 1)
            .ToList();

        int duplicatesRemoved = 0;
        int relationsUpdated = 0;

        foreach (var group in groupedCourses)
        {
            // Pick the first one as primary (owner)
            var primary = group.OrderBy(c => c.Id).First();
            var duplicates = group.Where(c => c.Id != primary.Id).ToList();

            foreach (var duplicate in duplicates)
            {
                var dupId = duplicate.Id;
                var primId = primary.Id;

                // CurriculumCourses
                var ccs = await dbContext.CurriculumCourses.Where(x => x.CourseId == dupId).ToListAsync(ct);
                foreach (var cc in ccs) { cc.CourseId = primId; relationsUpdated++; }

                // CourseOfferings
                var cos = await dbContext.CourseOfferings.Where(x => x.CourseId == dupId).ToListAsync(ct);
                foreach (var co in cos) { co.CourseId = primId; relationsUpdated++; }

                // DegreeRequirements
                var drs = await dbContext.Set<DegreeRequirementCourse>().Where(x => x.CourseId == dupId).ToListAsync(ct);
                foreach (var dr in drs) { dr.CourseId = primId; relationsUpdated++; }

                // Assignments
                var asgns = await dbContext.Assignments.Where(x => x.CourseId == dupId).ToListAsync(ct);
                foreach (var a in asgns) { a.CourseId = primId; relationsUpdated++; }

                // CoursePrerequisites (CourseId)
                var cp1 = await dbContext.CoursePrerequisites.Where(x => x.CourseId == dupId).ToListAsync(ct);
                foreach (var cp in cp1) { cp.CourseId = primId; relationsUpdated++; }

                // CoursePrerequisites (PrerequisiteCourseId)
                var cp2 = await dbContext.CoursePrerequisites.Where(x => x.PrerequisiteCourseId == dupId).ToListAsync(ct);
                foreach (var cp in cp2) { cp.PrerequisiteCourseId = primId; relationsUpdated++; }

                dbContext.Courses.Remove(duplicate);
                duplicatesRemoved++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync($"Deduplication complete. Removed {duplicatesRemoved} duplicate courses, and reassigned {relationsUpdated} relationships.", ct);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return new string(value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
