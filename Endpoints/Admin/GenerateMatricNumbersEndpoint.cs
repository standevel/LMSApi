using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin;

public sealed record GenerateMatricNumbersRequest(
    Guid AcademicSessionId,
    string Style, // Alphabetical, AdmissionTime, ProgramByProgram, CollegeByCollege
    Guid? ProgramId = null,
    Guid? FacultyId = null,
    bool PreviewOnly = false
);

public sealed record ProposedMatricAssignmentDto(
    Guid StudentId,
    string FirstName,
    string LastName,
    string OfficialEmail,
    string ProgramCode,
    string ProgramName,
    string FacultyName,
    DateTime CreatedAt,
    string ProposedMatricNumber
);

public sealed record GenerateMatricNumbersResponse(
    int TotalFound,
    int TotalAssigned,
    List<ProposedMatricAssignmentDto> PreviewList,
    string Message
);

/// <summary>
/// Endpoint to auto-generate and batch-assign student matric numbers with custom sorting and formatting options.
/// </summary>
public sealed class GenerateMatricNumbersEndpoint(LmsDbContext dbContext, ILogger<GenerateMatricNumbersEndpoint> logger)
    : ApiEndpoint<GenerateMatricNumbersRequest, GenerateMatricNumbersResponse>
{
    public override void Configure()
    {
        Post("admin/students/generate-matric-numbers");
        Roles("SuperAdmin", "Admin", "Registrar", "Registry");
        Tags("Administration");
    }

    public override async Task HandleAsync(GenerateMatricNumbersRequest req, CancellationToken ct)
    {
        logger.LogInformation("[MATRIC-GEN] Batch matric number generation requested. Style={Style}, PreviewOnly={PreviewOnly}", req.Style, req.PreviewOnly);

        // 1. Load active MatricNumberFormat configuration template
        var config = await dbContext.SystemRegistrationConfigurations.AsNoTracking().FirstOrDefaultAsync(ct);
        var formatTemplate = config?.MatricNumberFormat ?? "WU/{YY}/{PROGRAM}/{SEQ}";

        // Verify template has {SEQ}
        if (!formatTemplate.Contains("{SEQ}"))
        {
            await SendFailureAsync(400, "Invalid matric number format template in settings. Must contain '{SEQ}'.", "INVALID_FORMAT_TEMPLATE", "Invalid Template", ct);
            return;
        }

        // 2. Verify and load academic session
        var session = await dbContext.AcademicSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.AcademicSessionId, ct);
        if (session == null)
        {
            await SendFailureAsync(404, "Academic session not found", "NOT_FOUND", $"No academic session found with ID {req.AcademicSessionId}", ct);
            return;
        }

        // Determine cohort years
        var year4 = session.StartDate.Year.ToString();
        var year2 = year4.Length >= 4 ? year4.Substring(year4.Length - 2) : year4;

        // 3. Load students without matric numbers in this session
        var query = dbContext.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Faculty)
            .Where(s => s.AcademicSessionId == req.AcademicSessionId && (s.StudentNumber == null || s.StudentNumber == ""))
            .AsQueryable();

        // Optional filters
        if (req.ProgramId.HasValue)
        {
            query = query.Where(s => s.AcademicProgramId == req.ProgramId.Value);
        }
        if (req.FacultyId.HasValue)
        {
            query = query.Where(s => s.FacultyId == req.FacultyId.Value);
        }

        var students = await query.ToListAsync(ct);

        if (!students.Any())
        {
            await SendSuccessAsync(new GenerateMatricNumbersResponse(0, 0, new(), "No students found matching filters that require a matric number."), ct);
            return;
        }

        // 4. Sort students based on requested style
        var sortedStudents = req.Style.ToLowerInvariant() switch
        {
            "admissiontime" => students.OrderBy(s => s.CreatedAt).ToList(),
            "programbyprogram" => students
                .OrderBy(s => s.AcademicProgram != null ? s.AcademicProgram.Code : "ZZZ")
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList(),
            "collegebycollege" => students
                .OrderBy(s => s.Faculty != null ? s.Faculty.Name : "ZZZ")
                .ThenBy(s => s.AcademicProgram != null ? s.AcademicProgram.Code : "ZZZ")
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToList(),
            _ => students // default/alphabetical
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ThenBy(s => s.MiddleName ?? string.Empty)
                .ToList()
        };

        // 5. Assign sequential matric numbers
        var previewList = new List<ProposedMatricAssignmentDto>();
        var studentsToUpdate = new List<Student>();

        // Cache the next sequence number per program code to avoid querying database in a loop
        var programSequenceCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Build a regex capture pattern to parse sequence numbers from existing records
        var regexPattern = BuildRegexWithCaptureGroup(formatTemplate);

        foreach (var student in sortedStudents)
        {
            var programCode = student.AcademicProgram?.Code ?? "UNK";
            var cacheKey = $"{session.Id}_{programCode}";

            if (!programSequenceCache.TryGetValue(cacheKey, out var nextSeq))
            {
                // Find highest existing sequence in database matching current prefix template
                // E.g. If format is WU/{YY}/{PROGRAM}/{SEQ}, we look for matching strings
                var programCodeUpper = programCode.ToUpperInvariant();
                
                // Fetch student numbers for same session and program to extract the highest sequence
                var existingStudentNumbers = await dbContext.Students
                    .Where(s => s.AcademicSessionId == session.Id &&
                                s.AcademicProgramId == student.AcademicProgramId &&
                                s.StudentNumber != null &&
                                s.StudentNumber != "")
                    .Select(s => s.StudentNumber!)
                    .ToListAsync(ct);

                var maxSeq = 0;
                foreach (var num in existingStudentNumbers)
                {
                    var match = Regex.Match(num, regexPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var seqStr = match.Groups["seq"].Value;
                        if (int.TryParse(seqStr, out var parsed) && parsed > maxSeq)
                        {
                            maxSeq = parsed;
                        }
                    }
                }

                nextSeq = maxSeq + 1;
                programSequenceCache[cacheKey] = nextSeq;
            }

            // Generate matric number string from template
            var matricStr = formatTemplate
                .Replace("{YYYY}", year4)
                .Replace("{YY}", year2)
                .Replace("{PROGRAM}", programCode.ToUpperInvariant())
                .Replace("{SEQ}", nextSeq.ToString("D4"));

            previewList.Add(new ProposedMatricAssignmentDto(
                student.Id,
                student.FirstName,
                student.LastName,
                student.OfficialEmail,
                programCode,
                student.AcademicProgram?.Name ?? "Unknown Program",
                student.Faculty?.Name ?? "Unknown Faculty",
                student.CreatedAt,
                matricStr
            ));

            if (!req.PreviewOnly)
            {
                student.StudentNumber = matricStr;
                student.UpdatedAt = DateTime.UtcNow;
                studentsToUpdate.Add(student);
            }

            // Increment sequence for the next student in this program
            programSequenceCache[cacheKey] = nextSeq + 1;
        }

        // 6. Commit to database if not in preview mode
        if (!req.PreviewOnly && studentsToUpdate.Any())
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("[MATRIC-GEN] Successfully generated and assigned {Count} matric numbers.", studentsToUpdate.Count);
        }

        var statusMessage = req.PreviewOnly
            ? $"Generated preview of {previewList.Count} proposed matric numbers."
            : $"Successfully assigned and saved {previewList.Count} student matric numbers.";

        await SendSuccessAsync(new GenerateMatricNumbersResponse(
            sortedStudents.Count,
            req.PreviewOnly ? 0 : previewList.Count,
            previewList,
            statusMessage
        ), ct);
    }

    private static string BuildRegexWithCaptureGroup(string template)
    {
        var pattern = Regex.Escape(template);
        pattern = pattern
            .Replace(@"\{YYYY\}", @"\d{4}")
            .Replace(@"\{YY\}", @"\d{2}")
            .Replace(@"\{PROGRAM\}", @"[A-Z0-9]{2,4}")
            .Replace(@"\{SEQ\}", @"(?<seq>\d{4})");
        return $"^{pattern}$";
    }
}
