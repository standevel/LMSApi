using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Data.Repositories;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace LMS.Api.Services;

public record ParsedCourseRow(
    Guid Id,
    string ProgramName,
    int Level,
    Semester Semester,
    string Code,
    string Title,
    int CreditUnits,
    CourseCategory Category,
    int? LectureHours,
    int? PracticalHours,
    string? Error);

public sealed class CourseCatalogImportService(IServiceScopeFactory scopeFactory)
    : ICourseCatalogImportService
{
    // Singleton-safe: ConcurrentDictionary survives across HTTP requests
    private readonly ConcurrentDictionary<Guid, CourseCatalogImportPreview> _previews = new();

    public async Task<CourseCatalogImportPreview> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        Guid? programId,
        IEnumerable<Guid> programIds,
        Guid? academicSessionId,
        CancellationToken ct = default)
    {
        var uploadId = Guid.NewGuid();

        // Clone stream for parsing (original stream may not be re-readable)
        using var cloneStream = new MemoryStream();
        await fileStream.CopyToAsync(cloneStream, ct);
        cloneStream.Position = 0;

        // Resolve scoped repositories inside a short-lived scope
        using var scope = scopeFactory.CreateScope();
        var programRepo = scope.ServiceProvider.GetRequiredService<IAcademicProgramRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IAcademicSessionRepository>();

        var rows = await ParseDocumentAsync(cloneStream, programRepo, ct);

        // If specific programs were requested, filter rows to only those programs
        var programIdList = programIds.ToList();
        if (programIdList.Count > 0)
        {
            var programNames = new List<string>();
            foreach (var pid in programIdList)
            {
                var p = await programRepo.GetByIdAsync(pid, ct);
                if (p != null) programNames.Add(p.Name);
            }
            if (programNames.Count > 0)
            {
                rows = rows.Where(r => programNames.Any(n =>
                    n.Equals(r.ProgramName, StringComparison.OrdinalIgnoreCase))).ToList();
            }
        }

        var previewRows = rows.Select(r => new CourseCatalogPreviewRow(
            Guid.NewGuid(),
            r.ProgramName,
            r.Level,
            r.Semester,
            r.Code,
            r.Title,
            r.CreditUnits,
            r.Category,
            r.LectureHours,
            r.PracticalHours,
            r.Error)).ToList();

        // Resolve display names for preview header
        string? programName = null;
        if (programIdList.Count == 1)
            programName = (await programRepo.GetByIdAsync(programIdList[0], ct))?.Name;
        else if (programIdList.Count > 1)
            programName = $"{programIdList.Count} Programs";

        string? sessionName = null;
        if (academicSessionId.HasValue)
            sessionName = (await sessionRepo.GetByIdAsync(academicSessionId.Value, ct))?.Name;

        var preview = new CourseCatalogImportPreview(
            uploadId,
            fileName,
            programName,
            sessionName,
            previewRows,
            previewRows.Count);

        _previews[uploadId] = preview;
        return preview;
    }

    public CourseCatalogImportPreview GetPreview(Guid uploadId)
    {
        if (!_previews.TryGetValue(uploadId, out var preview))
            throw new KeyNotFoundException($"Upload {uploadId} not found.");
        return preview;
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
        if (!_previews.TryGetValue(uploadId, out var preview))
            throw new KeyNotFoundException($"Upload {uploadId} not found.");

        // Resolve scoped services for this operation
        using var scope = scopeFactory.CreateScope();
        var programRepository = scope.ServiceProvider.GetRequiredService<IAcademicProgramRepository>();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<IAcademicSessionRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<LmsDbContext>();

        // Separate counters for courses vs curriculum-course links
        int coursesCreated = 0;
        int coursesUpdated = 0;
        int coursesSkipped = 0;
        int curriculumCoursesAdded = 0;
        int curriculumCoursesUpdated = 0;
        var errors = new List<ImportErrorRow>();
        Guid? createdCurriculumId = null;

        // Determine the academic session
        AcademicSession? session = academicSessionId.HasValue
            ? await sessionRepository.GetByIdAsync(academicSessionId.Value, ct)
            : await sessionRepository.GetActiveAsync(ct);

        if (session == null)
            throw new Exception("No active academic session found.");

        // Determine the programs to import (lean query — no navigation properties needed)
        var programIdList = programIds.ToList();
        var programsToImport = new List<AcademicProgram>();
        if (programIdList.Count > 0)
        {
            programsToImport = await dbContext.Programs
                .Where(p => programIdList.Contains(p.Id))
                .ToListAsync(ct);
        }
        else
        {
            programsToImport = await dbContext.Programs.ToListAsync(ct);
        }

        // Cache all existing courses by (ProgramId, Code) for program-specific lookups
        var allCourses = (await dbContext.Courses.ToListAsync(ct))
            .GroupBy(c => (c.ProgramId, c.Code.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.First());

        // Cache all existing AcademicLevels by (ProgramId, Name)
        var levelsByKey = (await dbContext.Levels.ToListAsync(ct))
            .ToDictionary(l => (l.ProgramId, l.Name));

        // Cache all existing CurriculumCourses for fast duplicate detection
        // Key: (CurriculumId, CourseId, Semester)
        var existingCcKeys = (await dbContext.CurriculumCourses.ToListAsync(ct))
            .Select(cc => (cc.CurriculumId, cc.CourseId, cc.Semester))
            .ToHashSet();

        // Process each program
        foreach (var program in programsToImport)
        {
            var programRows = preview.Rows
                .Where(r => r.ProgramName.Equals(program.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (programRows.Count == 0) continue;

            // Find or create the target curriculum
            Curriculum targetCurriculum;
            if (curriculumId.HasValue)
            {
                var found = await dbContext.Curricula.FindAsync(new object[] { curriculumId.Value }, ct);
                if (found == null) continue;
                targetCurriculum = found;
            }
            else
            {
                var existing = await dbContext.Curricula
                    .FirstOrDefaultAsync(c => c.ProgramId == program.Id
                        && c.AdmissionSessionId == session.Id
                        && c.IsActive, ct);

                if (existing != null)
                {
                    targetCurriculum = existing;
                }
                else
                {
                    targetCurriculum = new Curriculum
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = program.Id,
                        AdmissionSessionId = session.Id,
                        Name = curriculumName ?? $"{program.Name} Curriculum",
                        Status = CurriculumStatus.Published,
                        IsActive = true
                    };
                    dbContext.Curricula.Add(targetCurriculum);
                    // Flush now so FK is valid for CurriculumCourse rows below
                    await dbContext.SaveChangesAsync(ct);
                    createdCurriculumId = targetCurriculum.Id;
                }
            }

            // --- Pre-flush: ensure all new courses are in DB before linking ---
            // Pass 1: upsert courses and levels
            var rowLevels = new Dictionary<int, AcademicLevel>();
            foreach (var row in programRows)
            {
                // Find or create AcademicLevel
                if (!rowLevels.ContainsKey(row.Level))
                {
                    var levelName = FormatLevelName(row.Level);
                    var levelKey = (program.Id, levelName);
                    if (!levelsByKey.TryGetValue(levelKey, out var academicLevel))
                    {
                        academicLevel = new AcademicLevel
                        {
                            Id = Guid.NewGuid(),
                            ProgramId = program.Id,
                            Name = levelName,
                            Order = ToLevelOrder(row.Level)
                        };
                        dbContext.Levels.Add(academicLevel);
                        levelsByKey[levelKey] = academicLevel;
                    }
                    rowLevels[row.Level] = academicLevel;
                }

                var rowLevel = rowLevels[row.Level];

                // Upsert Course - program-specific (same code can exist in different programs)
                var courseKey = (program.Id, row.CourseCode.ToUpperInvariant());
                if (!allCourses.TryGetValue(courseKey, out var course))
                {
                    course = new Course
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = program.Id,
                        Code = row.CourseCode,
                        Title = row.CourseTitle,
                        CreditUnits = row.CreditUnits,
                        LevelId = rowLevel.Id,
                        Semester = row.Semester,
                        LectureHours = row.LectureHours,
                        PracticalHours = row.PracticalHours,
                        IsActive = true
                    };
                    dbContext.Courses.Add(course);
                    allCourses[courseKey] = course;
                    coursesCreated++;
                }
                else
                {
                    course.Title = row.CourseTitle;
                    course.CreditUnits = row.CreditUnits;
                    course.LevelId = rowLevel.Id;
                    course.Semester = row.Semester;
                    course.LectureHours = row.LectureHours;
                    course.PracticalHours = row.PracticalHours;
                    coursesUpdated++;
                }
            }

            // Flush courses and levels to DB so their PKs are resolvable by EF FK tracking
            await dbContext.SaveChangesAsync(ct);

            // Pass 2: link courses to curriculum
            foreach (var row in programRows)
            {
                var courseKey = (program.Id, row.CourseCode.ToUpperInvariant());
                var course = allCourses[courseKey];
                var academicLevel = rowLevels[row.Level];

                var ccKey = (targetCurriculum.Id, course.Id, row.Semester);
                if (existingCcKeys.Contains(ccKey))
                {
                    // Update existing link
                    var existingCc = await dbContext.CurriculumCourses
                        .FirstOrDefaultAsync(cc =>
                            cc.CurriculumId == targetCurriculum.Id &&
                            cc.CourseId == course.Id &&
                            cc.Semester == row.Semester, ct);

                    if (existingCc != null)
                    {
                        existingCc.Category = row.Status;
                        existingCc.CreditUnits = row.CreditUnits;
                        existingCc.LevelId = academicLevel.Id;
                        curriculumCoursesUpdated++;
                    }
                }
                else
                {
                    dbContext.CurriculumCourses.Add(new CurriculumCourse
                    {
                        Id = Guid.NewGuid(),
                        CurriculumId = targetCurriculum.Id,
                        LevelId = academicLevel.Id,
                        CourseId = course.Id,
                        Semester = row.Semester,
                        Category = row.Status,
                        CreditUnits = row.CreditUnits
                    });
                    existingCcKeys.Add(ccKey);
                    curriculumCoursesAdded++;
                }
            }
        }

        // Persist all curriculum-course links
        await dbContext.SaveChangesAsync(ct);

        // Remove preview after successful import
        _previews.TryRemove(uploadId, out _);

        return new CourseCatalogImportResult(
            uploadId,
            true,
            coursesCreated,
            coursesUpdated,
            coursesSkipped,
            curriculumCoursesAdded,
            curriculumCoursesUpdated,
            createdCurriculumId?.ToString(),
            errors);
    }

    public void DeletePreview(Guid uploadId)
    {
        _previews.TryRemove(uploadId, out _);
    }

    private async Task<List<ParsedCourseRow>> ParseDocumentAsync(
        Stream stream,
        IAcademicProgramRepository programRepo,
        CancellationToken ct)
    {
        var rows = new List<ParsedCourseRow>();

        // Pre-fetch all programs once for header matching
        var dbPrograms = await programRepo.GetAllAsync(ct);

        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return rows;

        string currentProgram = "";
        int currentLevel = 0;
        Semester currentSemester = Semester.First;

        foreach (var element in body.Elements())
        {
            if (element is Paragraph p)
            {
                var text = p.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                var matchedProgram = ResolveProgram(text, dbPrograms);
                if (matchedProgram != null)
                {
                    currentProgram = matchedProgram.Name;
                    continue;
                }

                var level = DetectLevel(text);
                if (level.HasValue)
                {
                    currentLevel = level.Value;
                    continue;
                }

                if (text.Contains("FIRST SEMESTER", StringComparison.OrdinalIgnoreCase))
                {
                    currentSemester = Semester.First;
                    continue;
                }

                if (text.Contains("SECOND SEMESTER", StringComparison.OrdinalIgnoreCase))
                {
                    currentSemester = Semester.Second;
                    continue;
                }
            }

            if (element is Table table)
            {
                ParseTable(table, dbPrograms, ref currentProgram, ref currentLevel, ref currentSemester, rows);
            }
        }

        return rows;
    }

    private void ParseTable(
        Table table,
        List<AcademicProgram> programs,
        ref string currentProgram,
        ref int currentLevel,
        ref Semester currentSemester,
        List<ParsedCourseRow> rows)
    {
        // Detect column layout from the header row first
        int snCol = -1, codeCol = -1, titleCol = -1, unitsCol = -1, typeCol = -1, lhCol = -1, phCol = -1;

        foreach (var tableRow in table.Elements<TableRow>())
        {
            var cells = tableRow.Elements<TableCell>()
                                .Select(x => x.InnerText.Trim())
                                .ToList();

            var rowText = string.Join(" ", cells.Where(c => !string.IsNullOrWhiteSpace(c))).Trim();
            if (string.IsNullOrWhiteSpace(rowText)) continue;

            var matchedProgram = ResolveProgram(rowText, programs);
            if (matchedProgram != null)
            {
                currentProgram = matchedProgram.Name;
                continue;
            }

            var level = DetectLevel(rowText);
            if (level.HasValue)
            {
                currentLevel = level.Value;
            }

            if (rowText.Contains("FIRST SEMESTER", StringComparison.OrdinalIgnoreCase))
            {
                currentSemester = Semester.First;
                continue;
            }

            if (rowText.Contains("SECOND SEMESTER", StringComparison.OrdinalIgnoreCase))
            {
                currentSemester = Semester.Second;
                continue;
            }

            if (cells.Count < 3) continue;

            // Check if this is a header row
            bool isHeader = cells.Any(c =>
                c.Contains("Course Code", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("CourseCode", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Code", StringComparison.OrdinalIgnoreCase));

            if (isHeader)
            {
                for (int ci = 0; ci < cells.Count; ci++)
                {
                    var h = cells[ci].ToUpperInvariant();
                    if (h.Contains("S/N") || h.Contains("SN") || h == "NO" || h == "S.N")
                        snCol = ci;
                    else if (h.Contains("COURSE CODE") || h.Contains("CODE"))
                        codeCol = ci;
                    else if (h.Contains("COURSE TITLE") || h.Contains("TITLE"))
                        titleCol = ci;
                    else if (h.Contains("UNIT") || h.Contains("CREDIT"))
                        unitsCol = ci;
                    else if (h.Contains("STATUS") || h == "C/E" || h == "TYPE" || h == "E/C")
                        typeCol = ci;
                    else if (h.Contains("L.H") || h.Contains("LH") || h.Contains("LECTURE"))
                        lhCol = ci;
                    else if (h.Contains("P.H") || h.Contains("PH") || h.Contains("PRACTICAL"))
                        phCol = ci;
                }
                continue; // move to data rows
            }

            // Skip total/blank rows
            if (cells[0].Contains("TOTAL", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(cells[0]))
                continue;

            if (string.IsNullOrEmpty(currentProgram) || currentLevel <= 0)
                continue;

            // If we successfully detected columns, use them; otherwise fall back to positional
            string? code, title;
            int units;

            if (codeCol >= 0 && titleCol >= 0 && unitsCol >= 0)
            {
                // Header-detected layout
                code  = codeCol  < cells.Count ? cells[codeCol]  : null;
                title = titleCol < cells.Count ? cells[titleCol] : null;
                var unitsStr = unitsCol < cells.Count ? cells[unitsCol] : null;
                var parsedUnits = ParseInt(unitsStr);
                if (!parsedUnits.HasValue) continue;
                units = parsedUnits.Value;
            }
            else
            {
                // Positional fallback — try to figure out if there's an S/N column
                // by checking whether cells[0] is a small integer (serial number)
                if (cells.Count >= 5 && int.TryParse(cells[0], out _) && !int.TryParse(cells[1], out _))
                {
                    // Layout: S/N | Code | Title | Units | C/E [| LH | PH]
                    code  = cells[1];
                    title = cells[2];
                    var parsedUnits = ParseInt(cells[3]);
                    if (!parsedUnits.HasValue) continue;
                    units = parsedUnits.Value;
                    if (typeCol < 0) typeCol  = 4;
                    if (lhCol  < 0) lhCol    = 5;
                    if (phCol  < 0) phCol    = 6;
                }
                else
                {
                    // Layout: Code | Title | Units | C/E [| LH | PH]
                    code  = cells[0];
                    title = cells[1];
                    var parsedUnits = ParseInt(cells[2]);
                    if (!parsedUnits.HasValue) continue;
                    units = parsedUnits.Value;
                    if (typeCol < 0) typeCol = 3;
                    if (lhCol  < 0) lhCol   = 4;
                    if (phCol  < 0) phCol   = 5;
                }
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title)) continue;
            code = code.Trim();
            title = title.Trim();
            if (!IsLikelyCourseCode(code)) continue;

            var typeValue = typeCol >= 0 && typeCol < cells.Count ? cells[typeCol] : string.Empty;
            var category  = typeValue.StartsWith("E", StringComparison.OrdinalIgnoreCase)
                ? CourseCategory.Elective
                : CourseCategory.Compulsory;

            rows.Add(new ParsedCourseRow(
                Guid.NewGuid(),
                currentProgram,
                currentLevel,
                currentSemester,
                code,
                title,
                units,
                category,
                lhCol >= 0 && lhCol < cells.Count ? ParseInt(cells[lhCol]) : null,
                phCol >= 0 && phCol < cells.Count ? ParseInt(cells[phCol]) : null,
                null));
        }
    }

    private AcademicProgram? ResolveProgram(string text, List<AcademicProgram> programs)
    {
        var normalizedInput = Normalize(text);
        if (string.IsNullOrEmpty(normalizedInput)) return null;

        return programs.FirstOrDefault(p =>
            normalizedInput.Contains(Normalize(p.Name)) ||
            Normalize(p.Name).Contains(normalizedInput));
    }

    private static int? DetectLevel(string text)
    {
        var normalized = text.ToUpperInvariant();
        var numericMatch = Regex.Match(normalized, @"\bLEVEL\s*(\d{3})\b|\b(\d{3})\s*(?:LEVEL|L)\b|^\s*(\d{3})\s*$");
        if (numericMatch.Success)
        {
            var value = numericMatch.Groups
                .Cast<System.Text.RegularExpressions.Group>()
                .Skip(1)
                .First(g => g.Success)
                .Value;
            return int.Parse(value);
        }

        var yearMatch = Regex.Match(normalized, @"\b(?:YEAR|PART)\s+(ONE|TWO|THREE|FOUR|FIVE|1|2|3|4|5)\b");
        if (!yearMatch.Success)
            return null;

        return yearMatch.Groups[1].Value switch
        {
            "ONE" or "1" => 100,
            "TWO" or "2" => 200,
            "THREE" or "3" => 300,
            "FOUR" or "4" => 400,
            "FIVE" or "5" => 500,
            _ => null
        };
    }

    private static string FormatLevelName(int level)
    {
        var levelNumber = level < 10 ? level * 100 : level;
        return $"{levelNumber} Level";
    }

    private static int ToLevelOrder(int level)
    {
        var levelNumber = level < 10 ? level * 100 : level;
        return levelNumber >= 100 ? levelNumber / 100 : levelNumber;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = Regex.Match(value, @"\d+");
        return match.Success && int.TryParse(match.Value, out var x) ? x : null;
    }

    private static bool IsLikelyCourseCode(string value)
        => Regex.IsMatch(value.Trim(), @"^[A-Z]{2,}\s*\d{3}[A-Z]?$", RegexOptions.IgnoreCase);

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return new string(value.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
