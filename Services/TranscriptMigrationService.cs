using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class TranscriptMigrationService(LmsDbContext context) : ITranscriptMigrationService
{
    public async Task<PreviewTranscriptMigrationResponse> PreviewMigrationAsync(PreviewTranscriptMigrationRequest request, CancellationToken ct)
    {
        var student = await context.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.Level)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, ct);

        if (student == null)
        {
            throw new KeyNotFoundException($"Student with ID '{request.StudentId}' not found.");
        }

        var allActiveCourses = await context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Program)
                .ThenInclude(p => p.Department)
            .Include(c => c.Level)
            .ToListAsync(ct);

        var equivalencies = await context.CourseEquivalencies
            .AsNoTracking()
            .Where(e => e.IsActive && e.TargetCourseId.HasValue)
            .Include(e => e.TargetCourse)
            .ToListAsync(ct);

        var previewItems = new List<TranscriptMappingPreviewItem>();

        foreach (var source in request.Courses)
        {
            var srcCodeNorm = NormalizeCode(source.CourseCode);
            Course? matchedCourse = null;
            string? notes = null;

            // 1. Check Course Equivalency table
            var eqMatch = equivalencies.FirstOrDefault(e =>
                NormalizeCode(e.SourceCourseCode) == srcCodeNorm && e.TargetCourse != null);

            if (eqMatch?.TargetCourse != null)
            {
                matchedCourse = eqMatch.TargetCourse;
                notes = "Auto-mapped via Course Equivalency rule";
            }

            // 2. Exact code match
            if (matchedCourse == null && !string.IsNullOrWhiteSpace(srcCodeNorm))
            {
                matchedCourse = allActiveCourses.FirstOrDefault(c => NormalizeCode(c.Code) == srcCodeNorm);
                if (matchedCourse != null)
                {
                    notes = "Auto-mapped by exact course code match";
                }
            }

            // 3. Fuzzy / Exact title match
            if (matchedCourse == null && !string.IsNullOrWhiteSpace(source.CourseTitle))
            {
                var srcTitleNorm = source.CourseTitle.Trim().ToLowerInvariant();
                matchedCourse = allActiveCourses.FirstOrDefault(c => c.Title.Trim().ToLowerInvariant() == srcTitleNorm);
                if (matchedCourse != null)
                {
                    notes = "Auto-mapped by matching course title";
                }
            }

            if (matchedCourse != null)
            {
                previewItems.Add(new TranscriptMappingPreviewItem
                {
                    SourceCourseCode = source.CourseCode,
                    SourceCourseTitle = source.CourseTitle,
                    SourceCredits = source.CreditUnits,
                    SourceGrade = source.Grade,
                    SourceScore = source.Score,
                    TermOrSession = source.TermOrSession,
                    TargetCourseId = matchedCourse.Id,
                    TargetCourseCode = matchedCourse.Code,
                    TargetCourseTitle = matchedCourse.Title,
                    TargetCredits = matchedCourse.CreditUnits,
                    AutoMapped = true,
                    Status = "Mapped",
                    MappingNotes = notes
                });
            }
            else
            {
                previewItems.Add(new TranscriptMappingPreviewItem
                {
                    SourceCourseCode = source.CourseCode,
                    SourceCourseTitle = source.CourseTitle,
                    SourceCredits = source.CreditUnits,
                    SourceGrade = source.Grade,
                    SourceScore = source.Score,
                    TermOrSession = source.TermOrSession,
                    TargetCourseId = null,
                    TargetCourseCode = null,
                    TargetCourseTitle = null,
                    TargetCredits = source.CreditUnits,
                    AutoMapped = false,
                    Status = "Unmapped",
                    MappingNotes = "No automated match found. Please select an LMS course manually."
                });
            }
        }

        return new PreviewTranscriptMigrationResponse
        {
            StudentId = student.Id,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            StudentNumber = student.StudentNumber,
            ProgramId = student.AcademicProgramId,
            ProgramName = student.AcademicProgram?.Name,
            LevelId = student.LevelId,
            LevelName = student.Level?.Name,
            IsDirectEntry = student.IsDirectEntry,
            Mappings = previewItems,
            TotalSourceCourses = previewItems.Count,
            AutoMappedCount = previewItems.Count(p => p.AutoMapped),
            UnmappedCount = previewItems.Count(p => !p.AutoMapped),
            TotalSourceCredits = previewItems.Sum(p => p.SourceCredits),
            TotalMappedCredits = previewItems.Where(p => p.TargetCourseId.HasValue).Sum(p => p.TargetCredits)
        };
    }

    public async Task<CommitTranscriptMigrationResponse> CommitMigrationAsync(CommitTranscriptMigrationRequest request, Guid currentUserId, CancellationToken ct)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new InvalidOperationException("No courses provided to migrate.");
        }

        var student = await context.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, ct);

        if (student == null)
        {
            throw new KeyNotFoundException($"Student with ID '{request.StudentId}' not found.");
        }

        var appUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == student.Id 
                || (!string.IsNullOrEmpty(student.OfficialEmail) && u.Email == student.OfficialEmail)
                || (student.EntraObjectId != null && u.EntraObjectId == student.EntraObjectId), ct);

        var studentUserId = appUser?.Id ?? student.Id;

        // Session resolution
        Guid targetSessionId;
        string sessionName = "Current Session";
        if (request.AcademicSessionId.HasValue && request.AcademicSessionId.Value != Guid.Empty)
        {
            var session = await context.AcademicSessions.FirstOrDefaultAsync(s => s.Id == request.AcademicSessionId.Value, ct);
            if (session == null) throw new InvalidOperationException("Specified academic session not found.");
            targetSessionId = session.Id;
            sessionName = session.Name;
        }
        else if (student.AcademicSessionId != Guid.Empty)
        {
            var session = await context.AcademicSessions.FirstOrDefaultAsync(s => s.Id == student.AcademicSessionId, ct);
            targetSessionId = session?.Id ?? student.AcademicSessionId;
            sessionName = session?.Name ?? "Student Session";
        }
        else
        {
            var currentSession = await context.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct)
                ?? await context.AcademicSessions.OrderByDescending(s => s.StartDate).FirstOrDefaultAsync(ct);

            if (currentSession == null) throw new InvalidOperationException("No active academic session found in the system.");
            targetSessionId = currentSession.Id;
            sessionName = currentSession.Name;
        }

        // Mark student as Direct Entry if not already marked
        student.IsDirectEntry = true;
        if (!string.IsNullOrWhiteSpace(request.SourceInstitution) && string.IsNullOrWhiteSpace(student.DirectEntryInstitution))
        {
            student.DirectEntryInstitution = request.SourceInstitution.Trim();
        }
        student.UpdatedAt = DateTime.UtcNow;

        // Grading configuration
        var sysConfig = await context.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<GradeMappingDto>()
            : JsonSerializer.Deserialize<List<GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
              ?? new List<GradeMappingDto>();

        // Load target courses
        var targetCourseIds = request.Items.Select(i => i.TargetCourseId).Distinct().ToList();
        var targetCourses = await context.Courses
            .Where(c => targetCourseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        // Load or create course offerings for this session
        var existingOfferings = await context.CourseOfferings
            .Where(co => co.AcademicSessionId == targetSessionId && targetCourseIds.Contains(co.CourseId))
            .ToListAsync(ct);

        var existingEnrollments = await context.CourseEnrollments
            .Where(e => e.StudentId == studentUserId)
            .ToListAsync(ct);

        var existingResults = await context.StudentCourseResults
            .Where(r => r.StudentId == studentUserId && r.AcademicSessionId == targetSessionId)
            .ToListAsync(ct);

        var existingEquivalencies = await context.CourseEquivalencies
            .Where(e => e.IsActive)
            .ToListAsync(ct);

        var migratedResults = new List<MigratedCourseResultDto>();
        var now = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            if (!targetCourses.TryGetValue(item.TargetCourseId, out var targetCourse))
            {
                throw new InvalidOperationException($"Target course '{item.TargetCourseId}' does not exist.");
            }

            // Find or create course offering
            var offering = existingOfferings.FirstOrDefault(o => o.CourseId == targetCourse.Id);
            if (offering == null)
            {
                offering = new CourseOffering
                {
                    Id = Guid.NewGuid(),
                    CourseId = targetCourse.Id,
                    AcademicSessionId = targetSessionId,
                    Semester = targetCourse.Semester ?? Semester.First,
                    IsRegistrationClosed = false
                };
                context.CourseOfferings.Add(offering);
                existingOfferings.Add(offering);
            }

            // Ensure CourseEnrollment exists
            if (!existingEnrollments.Any(e => e.CourseOfferingId == offering.Id))
            {
                var enrollment = new CourseEnrollment
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentUserId,
                    CourseOfferingId = offering.Id,
                    Status = "Registered",
                    RegisteredAtUtc = now,
                    CreatedById = currentUserId
                };
                context.CourseEnrollments.Add(enrollment);
                existingEnrollments.Add(enrollment);
            }

            // Calculate Grade Points & Score
            var (letterGrade, gradePoints, totalScore) = ComputeGradeAndPoints(item.Grade, item.Score, mappings);

            // Calculation snapshot metadata
            var snapshotObj = new
            {
                isMigrated = true,
                sourceCourseCode = item.SourceCourseCode,
                sourceCourseTitle = item.SourceCourseTitle,
                sourceCredits = item.SourceCredits,
                sourceInstitution = request.SourceInstitution,
                termOrSession = item.TermOrSession,
                migratedAt = now,
                migratedBy = currentUserId,
                originalGrade = item.Grade,
                originalScore = item.Score
            };
            var snapshotJson = JsonSerializer.Serialize(snapshotObj);

            // Find or create StudentCourseResult
            var courseResult = existingResults.FirstOrDefault(r => r.CourseOfferingId == offering.Id);
            if (courseResult == null)
            {
                courseResult = new StudentCourseResult
                {
                    Id = Guid.NewGuid(),
                    CourseOfferingId = offering.Id,
                    StudentId = studentUserId,
                    AcademicSessionId = targetSessionId,
                    Semester = (int)offering.Semester,
                    CreditUnits = targetCourse.CreditUnits,
                    TotalScore = totalScore,
                    ExamScore = totalScore,
                    LetterGrade = letterGrade,
                    GradePoints = gradePoints,
                    IsPublished = true,
                    PublishedAt = now,
                    PublishedById = currentUserId,
                    CalculationSnapshotJson = snapshotJson,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.StudentCourseResults.Add(courseResult);
                existingResults.Add(courseResult);
            }
            else
            {
                courseResult.TotalScore = totalScore;
                courseResult.ExamScore = totalScore;
                courseResult.LetterGrade = letterGrade;
                courseResult.GradePoints = gradePoints;
                courseResult.CreditUnits = targetCourse.CreditUnits;
                courseResult.IsPublished = true;
                courseResult.PublishedAt = now;
                courseResult.PublishedById = currentUserId;
                courseResult.CalculationSnapshotJson = snapshotJson;
                courseResult.UpdatedAt = now;
            }

            // Save equivalency rule if requested
            if (item.SaveAsEquivalencyRule)
            {
                var srcCodeNorm = NormalizeCode(item.SourceCourseCode);
                var alreadyHasRule = existingEquivalencies.Any(e =>
                    NormalizeCode(e.SourceCourseCode) == srcCodeNorm && e.TargetCourseId == targetCourse.Id);

                if (!alreadyHasRule)
                {
                    var newEquivalency = new CourseEquivalency
                    {
                        Id = Guid.NewGuid(),
                        SourceInstitution = request.SourceInstitution?.Trim() ?? "External Institution",
                        SourceCourseCode = item.SourceCourseCode.Trim(),
                        SourceCourseName = item.SourceCourseTitle.Trim(),
                        SourceCredits = item.SourceCredits,
                        TargetCourseId = targetCourse.Id,
                        TargetCredits = targetCourse.CreditUnits,
                        Description = $"Created via Direct Entry transcript migration for student {student.StudentNumber ?? student.FirstName}",
                        IsActive = true,
                        CreatedAt = now
                    };
                    context.CourseEquivalencies.Add(newEquivalency);
                    existingEquivalencies.Add(newEquivalency);
                }
            }

            migratedResults.Add(new MigratedCourseResultDto
            {
                CourseOfferingId = offering.Id,
                TargetCourseId = targetCourse.Id,
                TargetCourseCode = targetCourse.Code,
                TargetCourseTitle = targetCourse.Title,
                CreditUnits = targetCourse.CreditUnits,
                Semester = offering.Semester.ToString(),
                AcademicSessionName = sessionName,
                LetterGrade = letterGrade,
                GradePoints = gradePoints,
                TotalScore = totalScore,
                SourceCourseCode = item.SourceCourseCode,
                SourceCourseTitle = item.SourceCourseTitle,
                SourceInstitution = request.SourceInstitution,
                MigratedAt = now
            });
        }

        // Audit Log
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Action = "CommitTranscriptMigration",
            EntityName = nameof(StudentCourseResult),
            EntityId = student.Id.ToString(),
            Changes = $"Migrated {migratedResults.Count} courses for student {student.FirstName} {student.LastName} ({student.StudentNumber}) from {request.SourceInstitution ?? "external"}",
            Timestamp = now
        });

        await context.SaveChangesAsync(ct);

        return new CommitTranscriptMigrationResponse
        {
            StudentId = student.Id,
            StudentName = $"{student.FirstName} {student.LastName}".Trim(),
            MigratedCount = migratedResults.Count,
            TotalCreditsTransferred = migratedResults.Sum(m => m.CreditUnits),
            MigratedCourses = migratedResults,
            Message = $"Successfully migrated {migratedResults.Count} course(s) ({migratedResults.Sum(m => m.CreditUnits)} credit units) for {student.FirstName} {student.LastName}."
        };
    }

    public byte[] GenerateTranscriptTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Transcript Migration");

        // Headers
        var headers = new[]
        {
            "Course Code",
            "Course Title",
            "Credit Units",
            "Grade",
            "Score",
            "Term or Session"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59); // Slate-800
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Sample Rows
        var sampleRows = new[]
        {
            new { Code = "CMP 101", Title = "Introduction to Computer Science", Credits = 3, Grade = "A", Score = 78, Term = "Year 1 First Semester" },
            new { Code = "MTH 101", Title = "Elementary Mathematics I", Credits = 4, Grade = "B", Score = 65, Term = "Year 1 First Semester" },
            new { Code = "PHY 101", Title = "General Physics I", Credits = 3, Grade = "A", Score = 72, Term = "Year 1 First Semester" },
            new { Code = "CMP 102", Title = "Introduction to Problem Solving", Credits = 3, Grade = "B", Score = 68, Term = "Year 1 Second Semester" }
        };

        for (int r = 0; r < sampleRows.Length; r++)
        {
            var row = sampleRows[r];
            var rowIdx = r + 2;
            ws.Cell(rowIdx, 1).Value = row.Code;
            ws.Cell(rowIdx, 2).Value = row.Title;
            ws.Cell(rowIdx, 3).Value = row.Credits;
            ws.Cell(rowIdx, 4).Value = row.Grade;
            ws.Cell(rowIdx, 5).Value = row.Score;
            ws.Cell(rowIdx, 6).Value = row.Term;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public List<TranscriptCourseSourceItem> ParseTranscriptFile(Stream fileStream, string fileName)
    {
        var items = new List<TranscriptCourseSourceItem>();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".csv")
        {
            using var reader = new StreamReader(fileStream);
            string? headerLine = reader.ReadLine();
            if (headerLine == null) return items;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = ParseCsvLine(line);
                if (cols.Count == 0 || string.IsNullOrWhiteSpace(cols[0])) continue;

                var code = cols.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code)) continue;

                var title = cols.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                decimal.TryParse(cols.ElementAtOrDefault(2)?.Trim(), out var credits);
                var grade = cols.ElementAtOrDefault(3)?.Trim() ?? "A";
                decimal? score = decimal.TryParse(cols.ElementAtOrDefault(4)?.Trim(), out var sc) ? sc : null;
                var term = cols.ElementAtOrDefault(5)?.Trim();

                items.Add(new TranscriptCourseSourceItem
                {
                    CourseCode = code,
                    CourseTitle = title,
                    CreditUnits = credits > 0 ? credits : 3m,
                    Grade = string.IsNullOrWhiteSpace(grade) ? "A" : grade.ToUpperInvariant(),
                    Score = score,
                    TermOrSession = term
                });
            }
        }
        else
        {
            using var workbook = new XLWorkbook(fileStream);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null) return items;

            var rows = ws.RangeUsed()?.RowsUsed()?.ToList();
            if (rows == null || rows.Count <= 1) return items;

            // Skip header row
            foreach (var row in rows.Skip(1))
            {
                var code = row.Cell(1).GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var title = row.Cell(2).GetString()?.Trim() ?? string.Empty;
                var creditsStr = row.Cell(3).GetString()?.Trim();
                decimal.TryParse(creditsStr, out var credits);

                var grade = row.Cell(4).GetString()?.Trim() ?? "A";
                var scoreStr = row.Cell(5).GetString()?.Trim();
                decimal? score = decimal.TryParse(scoreStr, out var sc) ? sc : null;

                var term = row.Cell(6).GetString()?.Trim();

                items.Add(new TranscriptCourseSourceItem
                {
                    CourseCode = code,
                    CourseTitle = title,
                    CreditUnits = credits > 0 ? credits : 3m,
                    Grade = string.IsNullOrWhiteSpace(grade) ? "A" : grade.ToUpperInvariant(),
                    Score = score,
                    TermOrSession = term
                });
            }
        }

        return items;
    }

    public async Task<IEnumerable<MigratedCourseResultDto>> GetStudentMigratedCoursesAsync(Guid studentId, CancellationToken ct)
    {
        var student = await context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null) return [];

        var appUser = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == student.Id 
                || (!string.IsNullOrEmpty(student.OfficialEmail) && u.Email == student.OfficialEmail)
                || (student.EntraObjectId != null && u.EntraObjectId == student.EntraObjectId), ct);

        var studentUserId = appUser?.Id ?? student.Id;

        var results = await context.StudentCourseResults
            .AsNoTracking()
            .Where(r => r.StudentId == studentUserId && r.CalculationSnapshotJson != null)
            .Include(r => r.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(r => r.AcademicSession)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var migratedDtos = new List<MigratedCourseResultDto>();

        foreach (var r in results)
        {
            if (string.IsNullOrWhiteSpace(r.CalculationSnapshotJson)) continue;

            try
            {
                using var doc = JsonDocument.Parse(r.CalculationSnapshotJson);
                if (doc.RootElement.TryGetProperty("isMigrated", out var isMigProp) && isMigProp.GetBoolean())
                {
                    string srcCode = doc.RootElement.TryGetProperty("sourceCourseCode", out var cc) ? cc.GetString() ?? "" : "";
                    string srcTitle = doc.RootElement.TryGetProperty("sourceCourseTitle", out var ctProp) ? ctProp.GetString() ?? "" : "";
                    string? srcInst = doc.RootElement.TryGetProperty("sourceInstitution", out var inst) ? inst.GetString() : null;

                    migratedDtos.Add(new MigratedCourseResultDto
                    {
                        CourseOfferingId = r.CourseOfferingId,
                        TargetCourseId = r.CourseOffering?.CourseId ?? Guid.Empty,
                        TargetCourseCode = r.CourseOffering?.Course?.Code ?? "",
                        TargetCourseTitle = r.CourseOffering?.Course?.Title ?? "",
                        CreditUnits = r.CreditUnits,
                        Semester = r.CourseOffering != null ? r.CourseOffering.Semester.ToString() : r.Semester.ToString(),
                        AcademicSessionName = r.AcademicSession?.Name ?? "",
                        LetterGrade = r.LetterGrade,
                        GradePoints = r.GradePoints,
                        TotalScore = r.TotalScore,
                        SourceCourseCode = srcCode,
                        SourceCourseTitle = srcTitle,
                        SourceInstitution = srcInst,
                        MigratedAt = r.CreatedAt
                    });
                }
            }
            catch
            {
                // Ignore parsing errors for non-matching snapshots
            }
        }

        return migratedDtos;
    }

    public async Task<IEnumerable<TranscriptTargetCourseDto>> GetAvailableTargetCoursesAsync(CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Program)
                .ThenInclude(p => p.Department)
            .Include(c => c.Level)
            .OrderBy(c => c.Code)
            .Select(c => new TranscriptTargetCourseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditUnits = c.CreditUnits,
                DepartmentName = c.Program != null && c.Program.Department != null ? c.Program.Department.Name : null,
                ProgramName = c.Program != null ? c.Program.Name : null,
                Semester = c.Semester != null ? c.Semester.ToString() : null,
                LevelName = c.Level != null ? c.Level.Name : null
            })
            .ToListAsync(ct);
    }

    private static (string LetterGrade, decimal GradePoints, decimal TotalScore) ComputeGradeAndPoints(
        string? gradeInput,
        decimal? scoreInput,
        List<GradeMappingDto> mappings)
    {
        var grade = gradeInput?.Trim().ToUpperInvariant() ?? string.Empty;

        // If score is provided, determine grade and points based on score
        if (scoreInput.HasValue)
        {
            var score = scoreInput.Value;
            if (mappings.Any())
            {
                var match = mappings.OrderByDescending(m => m.MinPercentage)
                    .FirstOrDefault(m => score >= m.MinPercentage);
                if (match != null)
                {
                    return (match.LetterGrade, match.GradePoints, score);
                }
            }

            // Fallback standard scale
            if (score >= 70) return ("A", 5.0m, score);
            if (score >= 60) return ("B", 4.0m, score);
            if (score >= 50) return ("C", 3.0m, score);
            if (score >= 45) return ("D", 2.0m, score);
            if (score >= 40) return ("E", 1.0m, score);
            return ("F", 0.0m, score);
        }

        // If only letter grade is provided
        if (!string.IsNullOrWhiteSpace(grade))
        {
            if (mappings.Any())
            {
                var match = mappings.FirstOrDefault(m => m.LetterGrade.Equals(grade, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    decimal defaultScore = match.MinPercentage;
                    return (match.LetterGrade, match.GradePoints, defaultScore);
                }
            }

            // Fallback points and scores
            return grade switch
            {
                "A" => ("A", 5.0m, 75m),
                "B" => ("B", 4.0m, 65m),
                "C" => ("C", 3.0m, 55m),
                "D" => ("D", 2.0m, 45m),
                "E" => ("E", 1.0m, 40m),
                _ => ("F", 0.0m, 0m)
            };
        }

        return ("A", 5.0m, 75m);
    }

    private static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        return code.Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());

        return result;
    }
}
