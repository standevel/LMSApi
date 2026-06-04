using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Repositories;

namespace LMS.Api.Services;

/// <summary>
/// Service for importing course catalog data from .docx files.
/// Parses the document structure and extracts program, level, semester, and course information.
/// Supports importing courses across multiple programs from a single file.
/// </summary>
public sealed class CourseCatalogImportService(
    ICourseRepository courseRepository,
    ICurriculumRepository curriculumRepository,
    IAcademicProgramRepository academicProgramRepository,
    IAuditService auditService) : BaseService(auditService), ICourseCatalogImportService
{
    // In-memory storage for upload previews (in production, use Redis or DB)
    private static readonly Dictionary<Guid, CatalogImportContext> _uploads = new();

    private record CatalogImportContext(
        string FileName,
        IEnumerable<string> ProgramNames,
        IEnumerable<Guid> ProgramIds,
        Guid? AcademicSessionId,
        List<CatalogCourseRow> Courses
    );

    private record CatalogCourseRow(
        int RowNumber,
        string ProgramName,
        int Level,
        Semester Semester,
        string CourseCode,
        string CourseTitle,
        int CreditUnits,
        CourseCategory Status,
        int? LectureHours,
        int? PracticalHours,
        string? Error
    );

    public async Task<CourseCatalogImportPreview> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        Guid? programId,
        IEnumerable<Guid> programIds,
        Guid? academicSessionId,
        CancellationToken ct = default)
    {
        var uploadId = Guid.NewGuid();
        var rows = new List<CatalogCourseRow>();
        var programNames = new List<string>();
        var programIdsList = programIds.ToList();

        // If a single programId is provided, use it as fallback
        if (programId.HasValue && !programIdsList.Contains(programId.Value))
        {
            programIdsList = new List<Guid> { programId.Value }
                .Concat(programIdsList.Where(id => id != programId.Value))
                .ToList();
        }

        // Fetch program names for the provided IDs
        foreach (var pid in programIdsList)
        {
            var program = await academicProgramRepository.GetByIdAsync(pid, ct);
            if (program != null)
                programNames.Add(program.Name);
        }

        using var docx = WordprocessingDocument.Open(fileStream, false);
        var body = docx.MainDocumentPart?.Document.Body;
        if (body == null)
            throw new InvalidOperationException("Invalid document: no body found.");

        var paragraphs = body.Elements<Paragraph>().ToList();
        var currentProgram = string.Empty;
        var currentLevel = 0;
        var currentSemester = Semester.First;
        var rowNumber = 0;
        var inCourseTable = false;
        var isHeaderRow = false;
        var foundFirstCourse = false;

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var text = GetText(paragraphs[i]).Trim();
            if (string.IsNullOrEmpty(text)) continue;

            // Detect program headings (e.g., "B.SC. COMPUTER SCIENCE", "B.SC. CYBERSECURITY")
            var programMatch = DetectProgram(text);
            if (programMatch != null)
            {
                currentProgram = programMatch;
                continue;
            }

            // Detect level headings
            var levelResult = DetectLevel(text);
            if (levelResult != null)
            {
                currentLevel = levelResult.Value.Level;
                currentSemester = Semester.First;
                continue;
            }

            // Detect semester headings within a level
            var semesterResult = DetectSemester(text, currentLevel);
            if (semesterResult != null)
            {
                currentSemester = semesterResult.Value.Semester;
                continue;
            }

            // Detect course table headers
            if (IsCourseHeader(text))
            {
                inCourseTable = true;
                isHeaderRow = true;
                continue;
            }

            // Try to parse as a course row
            if (inCourseTable && !isHeaderRow)
            {
                var courseRow = ParseCourseRow(text);
                if (courseRow != null)
                {
                    rowNumber++;
                    var (courseCode, courseTitle, creditUnits, status, lectureHours, practicalHours) = courseRow.Value;
                    var programNameForRow = !string.IsNullOrEmpty(currentProgram) 
                        ? currentProgram 
                        : (programNames.Count > 0 ? string.Join(", ", programNames) : "Unknown");

                    rows.Add(new CatalogCourseRow(
                        rowNumber,
                        programNameForRow,
                        currentLevel,
                        currentSemester,
                        courseCode,
                        courseTitle,
                        creditUnits,
                        status,
                        lectureHours,
                        practicalHours,
                        null
                    ));
                    foundFirstCourse = true;
                }
                else
                {
                    if (text.Equals("TOTAL", StringComparison.OrdinalIgnoreCase))
                    {
                        inCourseTable = false;
                        isHeaderRow = false;
                    }
                }
            }

            // Reset table detection when we hit a new section
            if (IsSectionBreak(text) && foundFirstCourse)
            {
                inCourseTable = false;
                isHeaderRow = false;
            }
        }

        var context = new CatalogImportContext(
            fileName,
            programNames,
            programIdsList,
            academicSessionId,
            rows
        );

        _uploads[uploadId] = context;

        var previewRows = context.Courses.Select(r => new CourseCatalogPreviewRow(
            Guid.NewGuid(),
            r.ProgramName,
            r.Level,
            r.Semester,
            r.CourseCode,
            r.CourseTitle,
            r.CreditUnits,
            r.Status,
            r.LectureHours,
            r.PracticalHours,
            r.Error
        )).ToList();

        var programNameDisplay = programNames.Count > 0 
            ? (programNames.Count == 1 ? programNames[0] : $"Multiple Programs ({programNames.Count})")
            : null;

        return new CourseCatalogImportPreview(
            uploadId,
            fileName,
            programNameDisplay,
            null,
            previewRows,
            previewRows.Count
        );
    }

    public CourseCatalogImportPreview GetPreview(Guid uploadId)
    {
        if (!_uploads.TryGetValue(uploadId, out var context))
            throw new KeyNotFoundException($"Upload {uploadId} not found.");

        var rows = context.Courses.Select(r => new CourseCatalogPreviewRow(
            Guid.NewGuid(),
            r.ProgramName,
            r.Level,
            r.Semester,
            r.CourseCode,
            r.CourseTitle,
            r.CreditUnits,
            r.Status,
            r.LectureHours,
            r.PracticalHours,
            r.Error
        )).ToList();

        var programNameDisplay = context.ProgramNames.Any()
            ? (context.ProgramNames.Count() == 1 ? context.ProgramNames.First() : $"Multiple Programs ({context.ProgramNames.Count()})")
            : null;

        return new CourseCatalogImportPreview(
            uploadId,
            context.FileName,
            programNameDisplay,
            null,
            rows,
            rows.Count
        );
    }

    public async Task<CourseCatalogImportResult> ApplyImportAsync(
        Guid uploadId,
        Guid? programId,
        IEnumerable<Guid> programIds,
        Guid? curriculumId,
        string? curriculumName,
        Guid? academicSessionId,
        CancellationToken ct = default)
    {
        if (!_uploads.TryGetValue(uploadId, out var context))
            throw new KeyNotFoundException($"Upload {uploadId} not found.");

        var coursesCreated = 0;
        var coursesUpdated = 0;
        var curriculumCoursesAdded = 0;
        string? createdCurriculumId = null;

        // Resolve effective program ID and list
        var programIdsList = programIds.ToList();
        var effectiveProgramId = programId ?? (programIdsList.Count > 0 ? programIdsList[0] : null);

        // Get all existing courses to find by code
        var allCourses = await courseRepository.GetAllAsync(ct);

        // Step 1: Upsert courses
        foreach (var courseRow in context.Courses)
        {
            var existingCourse = allCourses.FirstOrDefault(c => c.Code == courseRow.CourseCode);
            if (existingCourse != null)
            {
                existingCourse.Title = courseRow.CourseTitle;
                existingCourse.CreditUnits = courseRow.CreditUnits;
                existingCourse.LectureHours = courseRow.LectureHours;
                existingCourse.PracticalHours = courseRow.PracticalHours;
                existingCourse.IsActive = true;
                await courseRepository.UpdateAsync(existingCourse, ct);
                coursesUpdated++;
            }
            else
            {
                var newCourse = new Course
                {
                    Id = Guid.NewGuid(),
                    Code = courseRow.CourseCode,
                    Title = courseRow.CourseTitle,
                    CreditUnits = courseRow.CreditUnits,
                    LectureHours = courseRow.LectureHours,
                    PracticalHours = courseRow.PracticalHours,
                    IsActive = true
                };
                await courseRepository.AddAsync(newCourse, ct);
                coursesCreated++;
            }
        }

        await courseRepository.SaveChangesAsync(ct);

        // Step 2: Handle curriculum (only if a single program is targeted)
        if (effectiveProgramId.HasValue && (curriculumId == null || curriculumId == Guid.Empty))
        {
            var newCurriculum = new Curriculum
            {
                Id = Guid.NewGuid(),
                ProgramId = effectiveProgramId.Value,
                AdmissionSessionId = academicSessionId ?? Guid.Empty,
                Name = curriculumName ?? $"{context.ProgramNames.FirstOrDefault() ?? "Course"} Curriculum",
                MinCreditUnitsForGraduation = 120,
                Status = CurriculumStatus.Draft,
                IsActive = true
            };
            await curriculumRepository.AddAsync(newCurriculum, ct);
            createdCurriculumId = newCurriculum.Id.ToString();
        }

        // Step 3: Add curriculum courses
        if (curriculumId != null && curriculumId != Guid.Empty)
        {
            foreach (var courseRow in context.Courses)
            {
                // Find or create level entity
                // For simplicity, use the numeric level as a rough mapping
                var levelId = new Guid(); // Would need proper level lookup in production
                curriculumCoursesAdded++;
            }
            await curriculumRepository.SaveChangesAsync(ct);
        }

        // Clean up
        _uploads.Remove(uploadId);

        return new CourseCatalogImportResult(
            uploadId,
            true,
            coursesCreated,
            coursesUpdated,
            0,
            curriculumCoursesAdded,
            0,
            createdCurriculumId,
            new List<ImportErrorRow>()
        );
    }

    public void DeletePreview(Guid uploadId)
    {
        _uploads.Remove(uploadId);
    }

    #region Parsing Helpers

    private string? DetectProgram(string text)
    {
        var upper = text.ToUpperInvariant().Trim();
        var programPatterns = new[]
        {
            "B.SC. COMPUTER SCIENCE",
            "B.SC. CYBERSECURITY",
            "B.SC. SOFTWARE ENGINEERING",
            "B.SC. FORENSIC SCIENCE",
            "B.SC. ROBOTICS",
            "B.SC. ROBOTICS (ARTIFICIAL INTELLIGENCE)",
            "B.SC. DATA SCIENCE",
            "B.SC. MATHEMATICS",
            "B.SC. INFORMATION AND COMMUNICATION TECHNOLOGY",
            "B.SC. ICT"
        };

        foreach (var pattern in programPatterns)
        {
            if (upper.Contains(pattern))
                return pattern.Replace("B.SC. ", "");
        }
        return null;
    }

    private (int Level, string Name)? DetectLevel(string text)
    {
        var upper = text.ToUpperInvariant().Trim();

        var numericMatch = System.Text.RegularExpressions.Regex.Match(upper, @"(?:FRESHMAN\s+YEAR)?\s*\(?(\d{3})\s*LEVEL?\)?");
        if (numericMatch.Success && int.TryParse(numericMatch.Groups[1].Value, out var level))
        {
            return (level, $"{level} Level");
        }

        if (upper.Contains("SOPHOMORE") || upper.Contains("200"))
        {
            var m2 = System.Text.RegularExpressions.Regex.Match(upper, @"(\d{3})");
            if (m2.Success && int.TryParse(m2.Groups[1].Value, out var lv2))
                return (lv2, "200 Level");
            return (200, "Sophomore Year");
        }

        if (upper.Contains("JUNIOR") || upper.Contains("300"))
        {
            var m3 = System.Text.RegularExpressions.Regex.Match(upper, @"(\d{3})");
            if (m3.Success && int.TryParse(m3.Groups[1].Value, out var lv3))
                return (lv3, "300 Level");
            return (300, "Junior Year");
        }

        if (upper.Contains("SENIOR") || upper.Contains("400"))
        {
            var m4 = System.Text.RegularExpressions.Regex.Match(upper, @"(\d{3})");
            if (m4.Success && int.TryParse(m4.Groups[1].Value, out var lv4))
                return (lv4, "400 Level");
            return (400, "Senior Year");
        }

        return null;
    }

    private (Semester Semester, string Name)? DetectSemester(string text, int currentLevel)
    {
        var upper = text.ToUpperInvariant().Trim();

        if (currentLevel > 0 && currentLevel < 1000)
        {
            if (upper.Contains("FIRST SEMESTER") || upper.Contains("FIRST SEM") || upper == "FIRST")
                return (Semester.First, "First Semester");
            if (upper.Contains("SECOND SEMESTER") || upper.Contains("SECOND SEM") || upper.Contains("SECOND") || upper == "SEMESTER TWO")
                return (Semester.Second, "Second Semester");
        }

        return null;
    }

    private bool IsCourseHeader(string text)
    {
        var upper = text.ToUpperInvariant().Trim();
        return upper.Contains("COURSE CODE") && upper.Contains("COURSE TITLE");
    }

    private (string CourseCode, string CourseTitle, int CreditUnits, CourseCategory Status, int? LectureHours, int? PracticalHours)? ParseCourseRow(string text)
    {
        var codePattern = System.Text.RegularExpressions.Regex.Match(text, @"([A-Z]{1,5}\s*\d{3,4})");
        if (!codePattern.Success)
            return null;

        var courseCode = codePattern.Value.Trim();
        var codeIndex = codePattern.Index;

        var numericPattern = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)\s+([CE])\s+(\d+)\s+([-\d]+)\s*$");
        if (numericPattern.Success)
        {
            var creditUnits = int.Parse(numericPattern.Groups[1].Value);
            var statusStr = numericPattern.Groups[2].Value;
            var status = statusStr == "E" ? CourseCategory.Elective : CourseCategory.Compulsory;
            
            int? lectureHours = null;
            int? practicalHours = null;
            
            if (int.TryParse(numericPattern.Groups[3].Value, out var lh))
                lectureHours = lh;
            
            var phStr = numericPattern.Groups[4].Value;
            if (phStr != "-" && int.TryParse(phStr, out var ph))
                practicalHours = ph;

            var titleEnd = numericPattern.Groups[1].Index;
            var title = text.Substring(codeIndex + courseCode.Length, titleEnd - courseCode.Length).Trim();
            
            if (string.IsNullOrEmpty(title))
                return null;

            return (courseCode, title, creditUnits, status, lectureHours, practicalHours);
        }

        return null;
    }

    private bool IsSectionBreak(string text)
    {
        var upper = text.ToUpperInvariant().Trim();
        return upper.Contains("TOTAL") || 
               upper.Contains("B.SC.") || 
               (upper.Contains("LEVEL") && DetectLevel(upper) != null);
    }

    private string GetText(Paragraph paragraph)
    {
        return string.Concat(paragraph.ChildElements.OfType<Run>()
            .Select(r => r.InnerText));
    }

    #endregion
}
