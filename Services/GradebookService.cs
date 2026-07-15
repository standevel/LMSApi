using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class GradebookService : IGradebookService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;

    public GradebookService(LmsDbContext dbContext, IAuditService auditService, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _notificationService = notificationService;
    }

    #region System Configuration

    public async Task<ErrorOr<SystemGradingConfigurationDto>> GetSystemConfigurationAsync(CancellationToken ct = default)
    {
        var config = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            var defaultConfig = new SystemGradingConfiguration();
            return MapToSystemConfigurationDto(defaultConfig);
        }

        return MapToSystemConfigurationDto(config);
    }

    public async Task<ErrorOr<SystemGradingConfigurationDto>> UpdateSystemConfigurationAsync(
        UpdateSystemGradingConfigurationRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var config = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            config = new SystemGradingConfiguration();
            _dbContext.SystemGradingConfigurations.Add(config);
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultGradingStyle) &&
            Enum.TryParse<GradingStyle>(request.DefaultGradingStyle, ignoreCase: true, out var parsedStyle))
            config.DefaultGradingStyle = parsedStyle;

        if (request.DefaultExamPercentage.HasValue)
            config.DefaultExamPercentage = request.DefaultExamPercentage.Value;

        if (request.ApprovalWorkflowEnabled.HasValue)
            config.ApprovalWorkflowEnabled = request.ApprovalWorkflowEnabled.Value;

        if (request.DefaultCA1Weight.HasValue)
            config.DefaultCA1Weight = request.DefaultCA1Weight.Value;

        if (request.DefaultCA2Weight.HasValue)
            config.DefaultCA2Weight = request.DefaultCA2Weight.Value;

        if (request.DefaultCA3Weight.HasValue)
            config.DefaultCA3Weight = request.DefaultCA3Weight.Value;

        if (request.DefaultExamWeight.HasValue)
            config.DefaultExamWeight = request.DefaultExamWeight.Value;

        if (request.GpaScale.HasValue)
            config.GpaScale = request.GpaScale.Value;
            
        if (request.LetterGradesMapping != null)
        {
            config.LetterGradesMappingJson = System.Text.Json.JsonSerializer.Serialize(request.LetterGradesMapping);
        }

        if (!string.IsNullOrWhiteSpace(request.RoundingStrategy) &&
            Enum.TryParse<RoundingStrategy>(request.RoundingStrategy, ignoreCase: true, out var parsedRounding))
            config.RoundingStrategy = parsedRounding;

        if (request.RoundingDecimalPlaces.HasValue)
            config.RoundingDecimalPlaces = request.RoundingDecimalPlaces.Value;

        if (request.GraceThreshold.HasValue)
            config.GraceThreshold = request.GraceThreshold.Value;

        // Validate that category weights sum to 100%
        var totalWeight = config.DefaultCA1Weight + config.DefaultCA2Weight + config.DefaultCA3Weight + config.DefaultExamWeight;
        if (totalWeight != 100m)
            return Error.Validation("Weight.SumInvalid", $"Category weights must sum to 100%. Current total: {totalWeight}%");

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedById = userId;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("UpdateSystemConfiguration", "SystemGradingConfiguration", config.Id.ToString(), "Updated grading configuration", ct);

        return MapToSystemConfigurationDto(config);
    }

    #endregion

    #region Assessment Categories

    public async Task<ErrorOr<List<AssessmentCategoryDto>>> GetAssessmentCategoriesAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        return categories.Select(MapToCategoryDto).ToList();
    }

    public async Task<ErrorOr<AssessmentCategoryDto>> CreateAssessmentCategoryAsync(
        Guid courseOfferingId,
        CreateAssessmentCategoryRequest request,
        CancellationToken ct = default)
    {
        var category = new AssessmentCategory
        {
            CourseOfferingId = courseOfferingId,
            CategoryType = request.CategoryType,
            CategoryName = request.CategoryName,
            Weight = request.Weight,
            MaxMarks = request.MaxMarks,
            IsExamCategory = request.IsExamCategory,
            DisplayOrder = request.DisplayOrder
        };

        _dbContext.AssessmentCategories.Add(category);
        await _dbContext.SaveChangesAsync(ct);

        return MapToCategoryDto(category);
    }

    public async Task<ErrorOr<Deleted>> DeleteAssessmentCategoryAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _dbContext.AssessmentCategories.FindAsync(categoryId);
        if (category == null)
            return Error.NotFound("Category.NotFound", "Assessment category not found");

        _dbContext.AssessmentCategories.Remove(category);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Deleted;
    }

    #endregion

    #region Assessments

    public async Task<ErrorOr<List<AssessmentDto>>> GetAssessmentsAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.AssessmentCategory)
            .OrderBy(x => x.AssessmentCategory.DisplayOrder)
            .ThenBy(x => x.AssessmentDate)
            .ToListAsync(ct);

        var result = new List<AssessmentDto>();
        foreach (var assessment in assessments)
        {
            var gradesCount = await _dbContext.Grades
                .CountAsync(x => x.AssessmentId == assessment.Id, ct);

            result.Add(MapToAssessmentDto(assessment, gradesCount));
        }

        return result;
    }

    public async Task<ErrorOr<AssessmentDto>> CreateAssessmentAsync(
        Guid courseOfferingId,
        CreateAssessmentRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var category = await _dbContext.AssessmentCategories.FindAsync(request.AssessmentCategoryId);
        if (category == null)
            return Error.NotFound("Category.NotFound", "Assessment category not found");

        var assessment = new Assessment
        {
            CourseOfferingId = courseOfferingId,
            AssessmentCategoryId = request.AssessmentCategoryId,
            Title = request.Title,
            Description = request.Description,
            MaxMarks = request.MaxMarks,
            AssessmentDate = request.AssessmentDate,
            DueDate = request.DueDate
        };

        _dbContext.Assessments.Add(assessment);
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("CreateAssessment", "Assessment",
            assessment.Id.ToString(), $"Created assessment '{request.Title}'", ct);

        return MapToAssessmentDto(assessment, 0);
    }

    public async Task<ErrorOr<AssessmentDto>> UpdateAssessmentAsync(
        Guid assessmentId,
        UpdateAssessmentRequest request,
        CancellationToken ct = default)
    {
        var assessment = await _dbContext.Assessments.FindAsync(assessmentId);
        if (assessment == null)
            return Error.NotFound("Assessment.NotFound", "Assessment not found");

        if (request.AssessmentCategoryId.HasValue)
        {
            var category = await _dbContext.AssessmentCategories.FindAsync(request.AssessmentCategoryId.Value);
            if (category == null)
                return Error.NotFound("Category.NotFound", "Assessment category not found");
            assessment.AssessmentCategoryId = request.AssessmentCategoryId.Value;
        }

        if (request.Title != null)
            assessment.Title = request.Title;
        if (request.Description != null)
            assessment.Description = request.Description;
        if (request.MaxMarks.HasValue)
            assessment.MaxMarks = request.MaxMarks.Value;
        if (request.AssessmentDate.HasValue)
            assessment.AssessmentDate = request.AssessmentDate;
        if (request.DueDate.HasValue)
            assessment.DueDate = request.DueDate;

        assessment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var gradesCount = await _dbContext.Grades
            .CountAsync(x => x.AssessmentId == assessment.Id);

        return MapToAssessmentDto(assessment, gradesCount);
    }

    public async Task<ErrorOr<Deleted>> DeleteAssessmentAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _dbContext.Assessments.FindAsync(assessmentId);
        if (assessment == null)
            return Error.NotFound("Assessment.NotFound", "Assessment not found");

        _dbContext.Assessments.Remove(assessment);
        await _dbContext.SaveChangesAsync(ct);

        return Result.Deleted;
    }

    #endregion

    #region Grades

    public async Task<ErrorOr<List<GradeDto>>> GetGradesByAssessmentAsync(Guid assessmentId, CancellationToken ct = default)
    {
        var assessment = await _dbContext.Assessments.FindAsync(assessmentId);
        if (assessment == null)
            return Error.NotFound("Assessment.NotFound", "Assessment not found");

        var grades = await _dbContext.Grades
            .Where(x => x.AssessmentId == assessmentId)
            .Include(x => x.Student)
            .ToListAsync(ct);

        return grades.Select(g => MapToGradeDto(g, assessment.MaxMarks)).ToList();
    }

    public async Task<ErrorOr<List<StudentGradeSummaryDto>>> GetStudentGradeSummariesAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        // Get system configuration for grading calculation
        var sysConfig = await GetSystemConfigurationAsync(ct);
        if (sysConfig.IsError)
            return sysConfig.FirstError;

        // Get all enrolled students
        var offering = await _dbContext.CourseOfferings
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var enrollments = await _dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered")
            .Select(e => new { e.StudentId, e.Student.DisplayName, e.Student.Email })
            .ToListAsync(ct);

        var studentIds = enrollments.Select(e => e.StudentId).ToList();

        var studentEntities = await _dbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.StudentNumber })
            .ToListAsync(ct);

        var students = enrollments.Select(e => new
        {
            e.StudentId,
            StudentName = e.DisplayName ?? "Unknown",
            StudentEmail = e.Email ?? "",
            MatricNumber = studentEntities.FirstOrDefault(s => s.Id == e.StudentId)?.StudentNumber ?? "N/A"
        }).ToList();

        // Get all assessment categories with assessments and grades
        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.CourseOffering)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.Grades)
            .ToListAsync(ct);

        var result = new List<StudentGradeSummaryDto>();

        foreach (var student in students)
        {
            var ca1Score = CalculateCategoryScore(assessments, categories, student.StudentId, AssessmentCategoryType.CA1);
            var ca2Score = CalculateCategoryScore(assessments, categories, student.StudentId, AssessmentCategoryType.CA2);
            var ca3Score = CalculateCategoryScore(assessments, categories, student.StudentId, AssessmentCategoryType.CA3);
            var examScore = CalculateCategoryScore(assessments, categories, student.StudentId, AssessmentCategoryType.Exam);

            var totalScore = sysConfig.Value.DefaultGradingStyle == nameof(GradingStyle.Weighted)
                ? (ca1Score * sysConfig.Value.DefaultCA1Weight / 100m) +
                  (ca2Score * sysConfig.Value.DefaultCA2Weight / 100m) +
                  (ca3Score * sysConfig.Value.DefaultCA3Weight / 100m) +
                  (examScore * sysConfig.Value.DefaultExamWeight / 100m)
                : CalculateUnweightedAverage(ca1Score, ca2Score, ca3Score, examScore);

            Enum.TryParse<RoundingStrategy>(sysConfig.Value.RoundingStrategy, ignoreCase: true, out var rStrategy);
            var gradeResult = GradeCalculator.CalculateGrade(
                totalScore,
                rStrategy,
                sysConfig.Value.RoundingDecimalPlaces,
                sysConfig.Value.GraceThreshold,
                sysConfig.Value.LetterGradesMapping);

            result.Add(new StudentGradeSummaryDto(
                student.StudentId,
                student.MatricNumber,
                student.StudentName,
                student.StudentEmail,
                Math.Round(ca1Score, 2),
                Math.Round(ca2Score, 2),
                Math.Round(ca3Score, 2),
                Math.Round(examScore, 2),
                gradeResult.Score,
                gradeResult.LetterGrade,
                null));
        }

        return result.OrderByDescending(x => x.TotalScore).ToList();
    }

    public async Task<ErrorOr<int>> UpdateStudentGradeSummariesAsync(
        Guid courseOfferingId,
        UpdateStudentGradeSummaryRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var isPublished = await _dbContext.GradePublications
            .AnyAsync(x => x.CourseOfferingId == courseOfferingId && x.IsVisibleToStudents, ct);

        if (isPublished)
            return Error.Forbidden("Grade.Published", "Cannot update grades after publication");

        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        int successCount = 0;

        foreach (var studentGrade in request.Grades)
        {
            await UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca1Score, AssessmentCategoryType.CA1);
            await UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca2Score, AssessmentCategoryType.CA2);
            await UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca3Score, AssessmentCategoryType.CA3);
            await UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.ExamScore, AssessmentCategoryType.Exam);
        }

        await _dbContext.SaveChangesAsync(ct);
        return successCount;

        async Task UpdateOrAddGradeForCategory(Guid studentId, decimal? score, AssessmentCategoryType categoryType)
        {
            if (!score.HasValue) return;

            var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
            if (category == null) return;

            var assessment = assessments.FirstOrDefault(a => a.AssessmentCategoryId == category.Id);

            if (assessment == null)
            {
                assessment = new Assessment
                {
                    CourseOfferingId = courseOfferingId,
                    AssessmentCategoryId = category.Id,
                    Title = $"{category.CategoryName} Assessment",
                    MaxMarks = category.MaxMarks
                };
                _dbContext.Assessments.Add(assessment);
                await _dbContext.SaveChangesAsync(ct);
                assessments.Add(assessment);
            }

            var grade = await _dbContext.Grades
                .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == studentId, ct);

            if (grade == null)
            {
                grade = new Grade
                {
                    AssessmentId = assessment.Id,
                    StudentId = studentId,
                    MarksObtained = score.Value,
                    CreatedById = userId,
                    UpdatedById = userId
                };
                _dbContext.Grades.Add(grade);
                successCount++;
            }
            else if (!grade.IsLocked)
            {
                grade.MarksObtained = score.Value;
                grade.UpdatedById = userId;
                grade.UpdatedAt = DateTime.UtcNow;
                successCount++;
            }
        }
    }

    public async Task<ErrorOr<GradeDto>> EnterGradeAsync(
        EnterGradeRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var assessment = await _dbContext.Assessments.FindAsync(request.AssessmentId);
        if (assessment == null)
            return Error.NotFound("Assessment.NotFound", "Assessment not found");

        // Check if grades are locked
        var existingGrade = await _dbContext.Grades
            .FirstOrDefaultAsync(x => x.AssessmentId == request.AssessmentId && x.StudentId == request.StudentId, ct);

        if (existingGrade?.IsLocked == true)
            return Error.Forbidden("Grade.Locked", "Cannot edit locked grades");

        // Check if grades are already published
        var isPublished = await _dbContext.GradePublications
            .AnyAsync(x => x.CourseOfferingId == assessment.CourseOfferingId && x.IsVisibleToStudents, ct);

        if (isPublished)
            return Error.Forbidden("Grade.Published", "Cannot edit grades after publication");

        if (existingGrade == null)
        {
            existingGrade = new Grade
            {
                AssessmentId = request.AssessmentId,
                StudentId = request.StudentId,
                MarksObtained = request.MarksObtained,
                Remarks = request.Remarks,
                CreatedById = userId,
                UpdatedById = userId
            };
            _dbContext.Grades.Add(existingGrade);
        }
        else
        {
            existingGrade.MarksObtained = request.MarksObtained;
            existingGrade.Remarks = request.Remarks;
            existingGrade.UpdatedById = userId;
            existingGrade.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("EnterGrade", "Grade",
            existingGrade.Id.ToString(), $"Entered grade {request.MarksObtained} for assessment {request.AssessmentId}", ct);

        return MapToGradeDto(existingGrade, assessment.MaxMarks);
    }

    #endregion

    #region Excel Operations

    public async Task<ErrorOr<GradebookExcelTemplateDto>> GenerateExcelTemplateAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        // Get categories for column headers
        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        // Get enrolled students
        var students = await _dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered")
            .Include(e => e.Student)
            .OrderBy(e => e.Student.DisplayName)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Gradebook");

        // Title
        worksheet.Cell(1, 1).Value = $"Gradebook: {offering.Course.Code} - {offering.Course.Title}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 5 + categories.Count).Merge();

        // Headers
        worksheet.Cell(3, 1).Value = "Student ID";
        worksheet.Cell(3, 2).Value = "Student Name";
        worksheet.Cell(3, 3).Value = "Email";

        int col = 4;
        foreach (var category in categories)
        {
            worksheet.Cell(3, col).Value = $"{category.CategoryName} ({category.Weight}%)";
            col++;
        }

        worksheet.Cell(3, col).Value = "Total";
        worksheet.Cell(3, col + 1).Value = "Remarks";

        // Style headers
        var headerRange = worksheet.Range(3, 1, 3, col + 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 75, 68);
        headerRange.Style.Font.FontColor = XLColor.White;

        // Student data
        int row = 4;
        foreach (var student in students)
        {
            worksheet.Cell(row, 1).Value = student.Student.Id.ToString();
            worksheet.Cell(row, 2).Value = student.Student.DisplayName ?? "Unknown";
            worksheet.Cell(row, 3).Value = student.Student.Email ?? "";

            // Empty cells for grades
            for (int i = 4; i <= col; i++)
            {
                worksheet.Cell(row, i).Value = "";
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add instructions sheet
        var instructionsSheet = workbook.Worksheets.Add("Instructions");
        instructionsSheet.Cell(1, 1).Value = "Grade Entry Instructions";
        instructionsSheet.Cell(1, 1).Style.Font.Bold = true;
        instructionsSheet.Cell(1, 1).Style.Font.FontSize = 14;

        instructionsSheet.Cell(3, 1).Value = "1. Enter marks for each assessment (0-100 or above for bonus marks)";
        instructionsSheet.Cell(4, 1).Value = "2. Do not modify the Student ID column";
        instructionsSheet.Cell(5, 1).Value = "3. The Total column will be calculated automatically";
        instructionsSheet.Cell(6, 1).Value = "4. Add any remarks in the Remarks column";
        instructionsSheet.Cell(7, 1).Value = "5. Save and upload this file";

        instructionsSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return new GradebookExcelTemplateDto(
            stream.ToArray(),
            $"Gradebook_{offering.Course.Code}_{offering.AcademicSession.Name}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<ErrorOr<GradebookExcelTemplateDto>> GenerateSenateResultTemplateAsync(Guid courseOfferingId, string? collegeName = null, CancellationToken ct = default)
    {
        // ── Load Target Offering and Cohort Context ───────────────────────
        var offering = await _dbContext.CourseOfferings
            .Include(x => x.Course)
                .ThenInclude(c => c.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(x => x.AcademicSession)
            .Include(x => x.Programs).ThenInclude(p => p.Program)
                .ThenInclude(p => p.Department).ThenInclude(d => d.Faculty)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var offeringProgram = offering.Programs.FirstOrDefault();
        var programName  = offeringProgram?.Program?.Name ?? offering.Course.Program?.Name ?? "N/A";
        var levelName    = offeringProgram?.Level?.Name    ?? "N/A";
        var sessionName  = offering.AcademicSession.Name;
        var semesterLabel = offering.Semester == Data.Enums.Semester.First ? "FIRST" : "SECOND";

        var resolvedFaculty = offeringProgram?.Program?.Department?.Faculty
                           ?? offering.Course.Program?.Department?.Faculty;
        var facultyLabel    = resolvedFaculty?.Label ?? "COLLEGE";
        var facultyName     = collegeName
                           ?? resolvedFaculty?.Name
                           ?? programName;
        var collegeHeader   = $"{facultyLabel.ToUpper()} OF {facultyName.ToUpper()}";

        // Query all peer course offerings in this cohort (same Session, Semester, Program, Level)
        var targetProgramIds = offering.Programs.Select(p => p.ProgramId).ToList();
        var targetLevelIds = offering.Programs.Select(p => p.LevelId).ToList();

        var peerOfferings = await _dbContext.CourseOfferings
            .Include(x => x.Course)
            .Where(x => x.AcademicSessionId == offering.AcademicSessionId &&
                        x.Semester == offering.Semester &&
                        x.Programs.Any(p => targetProgramIds.Contains(p.ProgramId) && targetLevelIds.Contains(p.LevelId)))
            .ToListAsync(ct);

        var uniquePeerOfferings = peerOfferings
            .GroupBy(x => x.Course.Code)
            .Select(g => g.First())
            .OrderBy(x => x.Course.Code)
            .ToList();

        // Get student summaries for each of these peer offerings
        var allSummaries = new Dictionary<Guid, List<StudentGradeSummaryDto>>();
        foreach (var peer in uniquePeerOfferings)
        {
            var sumRes = await GetStudentGradeSummariesAsync(peer.Id, ct);
            if (!sumRes.IsError)
            {
                allSummaries[peer.Id] = sumRes.Value;
            }
        }

        // Gather all enrolled students across all peer offerings
        var peerOfferingIds = uniquePeerOfferings.Select(x => x.Id).ToList();
        var enrollments = await _dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => peerOfferingIds.Contains(e.CourseOfferingId) && e.Status == "Registered")
            .Include(e => e.Student)
            .ToListAsync(ct);

        var cohortStudents = enrollments
            .GroupBy(e => e.StudentId)
            .Select(g => g.First().Student)
            .OrderBy(s => s.DisplayName)
            .ToList();

        var studentIds = cohortStudents.Select(x => x.Id).ToList();
        var studentNumberMap = await _dbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.StudentNumber, ct);

        // ── Load Template Workbook ───────────────────────────────────────
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "wigwe_result_template.xlsx");
        if (!File.Exists(templatePath))
        {
            // Fallback to project root if BaseDirectory assets aren't copied yet
            templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "wigwe_result_template.xlsx");
            if (!File.Exists(templatePath))
            {
                templatePath = "/Users/mac/Apps/LMS APP/wigwe_result_template.xlsx";
            }
        }

        using var workbook = new XLWorkbook(templatePath);
        var ws = workbook.Worksheet("CGPA (2)");
        ws.Name = "Senate Result";

        // ── Header Metadata Row 1 ─────────────────────────────────────────
        var deptName = offeringProgram?.Program?.Department?.Name ?? offering.Course.Program?.Department?.Name ?? "N/A";
        var headerText = $"{facultyLabel.ToUpper()} OF {facultyName.ToUpper()}\nDEPARTMENT OF {deptName.ToUpper()}\nAcademic Year: {sessionName}\nLevel: {levelName}";
        ws.Cell(1, 7).Value = headerText;

        // ── Populate Courses (columns 9 to 38) ─────────────────────────────
        int startCourseCol = 9;
        int maxCourseCols = 30; // Columns I (9) to AL (38)
        int numCourses = Math.Min(uniquePeerOfferings.Count, maxCourseCols);

        for (int i = 0; i < numCourses; i++)
        {
            var peer = uniquePeerOfferings[i];
            int col = startCourseCol + i;

            var parts = peer.Course.Code.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var prefix = parts.FirstOrDefault() ?? "";
            var suffix = parts.Length > 1 ? parts[1] : "";

            ws.Cell(2, col).Value = prefix;
            ws.Cell(3, col).Value = suffix;
            ws.Cell(4, col).Value = peer.Course.CreditUnits;
        }

        // Delete unused course columns (shifting summary columns left)
        int deleteStartCol = startCourseCol + numCourses;
        int deleteEndCol = 38;
        if (deleteEndCol >= deleteStartCol)
        {
            ws.Columns(deleteStartCol, deleteEndCol).Delete();
        }

        int deletedCount = deleteEndCol - deleteStartCol + 1;
        int regUnitsCol = 39 - deletedCount;
        int passedUnitsCol = 40 - deletedCount;
        int failedUnitsCol = 41 - deletedCount;
        int totalGpCol = 42 - deletedCount;
        int gpaCol = 43 - deletedCount;
        int remarksCol = 44 - deletedCount;

        // Clear existing template dummy values (rows 6 to 327)
        ws.Rows(6, 327).Clear(XLClearOptions.Contents);

        // Standard grading mapping
        Func<double, (string Grade, double Points)> getGradeAndPoints = (score) =>
        {
            if (score >= 70) return ("A", 5.0);
            if (score >= 60) return ("B", 4.0);
            if (score >= 50) return ("C", 3.0);
            if (score >= 45) return ("D", 2.0);
            if (score >= 40) return ("E", 1.0);
            return ("F", 0.0);
        };

        // ── Populate Student Data Rows ─────────────────────────────────────
        int currentRow = 6;
        for (int k = 0; k < cohortStudents.Count; k++)
        {
            var student = cohortStudents[k];
            int r1 = currentRow;
            int r2 = currentRow + 1;

            // S/N
            ws.Cell(r1, 1).Value = k + 1;
            ws.Range(r1, 1, r2, 1).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(r1, 1, r2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Matric No
            studentNumberMap.TryGetValue(student.Id, out var matricNum);
            ws.Cell(r1, 2).Value = matricNum ?? "N/A";
            ws.Range(r1, 2, r2, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(r1, 2, r2, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Name
            ws.Cell(r1, 3).Value = student.DisplayName ?? "Unknown";
            ws.Range(r1, 3, r2, 8).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Range(r1, 3, r2, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Course scores & grades
            double totalRegisteredUnits = 0;
            double totalPassedUnits = 0;
            double totalFailedUnits = 0;
            double totalGradePoints = 0;
            var outstandingList = new List<string>();

            for (int i = 0; i < numCourses; i++)
            {
                int col = startCourseCol + i;
                var peer = uniquePeerOfferings[i];

                double? score = null;
                if (allSummaries.TryGetValue(peer.Id, out var peerSummaries))
                {
                    var studSummary = peerSummaries.FirstOrDefault(s => s.StudentId == student.Id);
                    if (studSummary != null)
                    {
                        score = (double)studSummary.TotalScore;
                    }
                }

                if (score.HasValue)
                {
                    var gp = getGradeAndPoints(score.Value);
                    totalRegisteredUnits += peer.Course.CreditUnits;

                    if (gp.Grade != "F")
                    {
                        totalPassedUnits += peer.Course.CreditUnits;
                    }
                    else
                    {
                        totalFailedUnits += peer.Course.CreditUnits;
                        outstandingList.Add($"{peer.Course.Code} ({peer.Course.CreditUnits})");
                    }

                    totalGradePoints += gp.Points * peer.Course.CreditUnits;

                    // Row 1: Score
                    ws.Cell(r1, col).Value = Math.Round(score.Value, 1);
                    ws.Cell(r1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Row 2: Grade formula
                    var scoreCellRef = ws.Cell(r1, col).Address.ToString();
                    ws.Cell(r2, col).FormulaA1 = $"=IFS({scoreCellRef}>=70,\"A\",{scoreCellRef}>=60,\"B\",{scoreCellRef}>=50,\"C\",{scoreCellRef}>=45,\"D\",{scoreCellRef}>=40,\"E\",{scoreCellRef}<40,\"F\")";
                    ws.Cell(r2, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            // Summary metrics
            // Total Registered
            ws.Cell(r2, regUnitsCol).Value = totalRegisteredUnits;
            ws.Cell(r2, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Total Passed
            ws.Cell(r2, passedUnitsCol).Value = totalPassedUnits;
            ws.Cell(r2, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Total Failed
            ws.Cell(r2, failedUnitsCol).Value = totalFailedUnits;
            ws.Cell(r2, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Total Grade Point
            ws.Cell(r1, totalGpCol).Value = totalGradePoints;
            ws.Range(r1, totalGpCol, r2, totalGpCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(r1, totalGpCol, r2, totalGpCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // GPA
            var totalGpCell = ws.Cell(r1, totalGpCol).Address.ToString();
            var regUnitsCell = ws.Cell(r2, regUnitsCol).Address.ToString();
            ws.Cell(r1, gpaCol).FormulaA1 = $"=IF({regUnitsCell}>0, ROUND({totalGpCell}/{regUnitsCell}, 2), 0)";
            ws.Range(r1, gpaCol, r2, gpaCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(r1, gpaCol, r2, gpaCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Remarks
            string remarksVal = outstandingList.Count > 0 ? string.Join(", ", outstandingList) : "PASS";
            ws.Cell(r1, remarksCol).Value = remarksVal;
            ws.Range(r1, remarksCol, r2, remarksCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(r1, remarksCol, r2, remarksCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Borders and styles
            var studentRange = ws.Range(r1, 1, r2, remarksCol);
            studentRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            studentRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            studentRange.Style.Font.FontName = "Aptos Narrow";
            studentRange.Style.Font.FontSize = 10;

            currentRow += 2;
        }

        // Delete other sheets to return only the result worksheet
        var sheetsToDelete = workbook.Worksheets.Where(x => x.Name != "Senate Result").ToList();
        foreach (var sheet in sheetsToDelete)
        {
            workbook.Worksheets.Delete(sheet.Name);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"SenateResult_{offering.Course.Code}_{sessionName}_{semesterLabel}Sem.xlsx";
        return new GradebookExcelTemplateDto(
            stream.ToArray(),
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<ErrorOr<GradebookExcelTemplateDto>> GenerateCollegeSenateResultAsync(
        Guid academicSessionId,
        Data.Enums.Semester semester,
        Guid collegeId,
        Guid levelId,
        CancellationToken ct = default)
    {
        // ── Load Metadata ─────────────────────────────────────────────────
        var session = await _dbContext.AcademicSessions.FindAsync(new object[] { academicSessionId }, ct);
        var faculty = await _dbContext.Faculties.FindAsync(new object[] { collegeId }, ct);
        var level = await _dbContext.Levels.FindAsync(new object[] { levelId }, ct);

        if (session == null) return Error.NotFound("Session.NotFound", "Academic session not found");
        if (faculty == null) return Error.NotFound("Faculty.NotFound", "Faculty not found");
        if (level == null) return Error.NotFound("Level.NotFound", "Academic level not found");

        var semesterLabel = semester == Data.Enums.Semester.First ? "FIRST" : "SECOND";

        // ── Load Template Workbook ────────────────────────────────────────
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "wigwe_result_template.xlsx");
        if (!File.Exists(templatePath))
        {
            templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "wigwe_result_template.xlsx");
            if (!File.Exists(templatePath))
            {
                templatePath = "/Users/mac/Apps/LMS APP/wigwe_result_template.xlsx";
            }
        }

        using var workbook = new XLWorkbook(templatePath);
        var wsTemplate = workbook.Worksheet("CGPA (2)");

        // ── Load Active Programs in the College ────────────────────────────
        var programs = await _dbContext.Programs
            .Include(p => p.Department)
                .ThenInclude(d => d.Faculty)
            .Where(p => p.Department.FacultyId == collegeId && p.IsActive)
            .ToListAsync(ct);

        bool hasAnyWorksheet = false;

        Func<double, (string Grade, double Points)> getGradeAndPoints = (score) =>
        {
            if (score >= 70) return ("A", 5.0);
            if (score >= 60) return ("B", 4.0);
            if (score >= 50) return ("C", 3.0);
            if (score >= 45) return ("D", 2.0);
            if (score >= 40) return ("E", 1.0);
            return ("F", 0.0);
        };

        foreach (var program in programs)
        {
            // Find all offerings for this program and level in this semester/session
            var offerings = await _dbContext.CourseOfferings
                .Include(co => co.Course)
                .Where(co => co.AcademicSessionId == academicSessionId &&
                            co.Semester == semester &&
                            co.Programs.Any(p => p.ProgramId == program.Id && 
                                                (p.LevelId == levelId || 
                                                 p.Level.Order == level.Order || 
                                                 p.Level.Name.ToLower() == level.Name.ToLower())))
                .ToListAsync(ct);

            if (offerings.Count == 0)
                continue;

            var uniquePeerOfferings = offerings
                .GroupBy(x => x.Course.Code)
                .Select(g => g.First())
                .OrderBy(x => x.Course.Code)
                .ToList();

            var peerOfferingIds = uniquePeerOfferings.Select(x => x.Id).ToList();

            // Load registered students for this program cohort
            var enrollments = await _dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => peerOfferingIds.Contains(e.CourseOfferingId) && e.Status == "Registered")
                .Include(e => e.Student)
                .ToListAsync(ct);

            var cohortStudents = enrollments
                .GroupBy(e => e.StudentId)
                .Select(g => g.First().Student)
                .OrderBy(s => s.DisplayName)
                .ToList();

            if (cohortStudents.Count == 0)
                continue;

            var studentIds = cohortStudents.Select(x => x.Id).ToList();
            var studentNumberMap = await _dbContext.Students
                .AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.StudentNumber, ct);

            // Fetch student summaries for each offering
            var allSummaries = new Dictionary<Guid, List<StudentGradeSummaryDto>>();
            foreach (var peer in uniquePeerOfferings)
            {
                var sumRes = await GetStudentGradeSummariesAsync(peer.Id, ct);
                if (!sumRes.IsError)
                {
                    allSummaries[peer.Id] = sumRes.Value;
                }
            }

            // Define sheet name (limited to 30 chars, no special chars)
            var sheetName = program.Code;
            if (string.IsNullOrWhiteSpace(sheetName)) sheetName = program.Name;
            sheetName = sheetName.Length > 30 ? sheetName.Substring(0, 30) : sheetName;
            foreach (var ch in new[] { '\\', '/', '?', '*', ':', '[', ']' })
            {
                sheetName = sheetName.Replace(ch, '_');
            }

            var ws = wsTemplate.CopyTo(sheetName);
            hasAnyWorksheet = true;

            // ── Set Metadata Row 1 ─────────────────────────────────────────
            var facultyLabel = program.Department?.Faculty?.Label ?? faculty.Label;
            var facultyName = program.Department?.Faculty?.Name ?? faculty.Name;
            var deptName = program.Department?.Name ?? "N/A";
            var headerText = $"{facultyLabel.ToUpper()} OF {facultyName.ToUpper()}\nDEPARTMENT OF {deptName.ToUpper()}\nAcademic Year: {session.Name}\nLevel: {level.Name}";
            ws.Cell(1, 7).Value = headerText;

            // ── Populate Courses (columns 9 to 38) ─────────────────────────
            int startCourseCol = 9;
            int maxCourseCols = 30;
            int numCourses = Math.Min(uniquePeerOfferings.Count, maxCourseCols);

            for (int i = 0; i < numCourses; i++)
            {
                var peer = uniquePeerOfferings[i];
                int col = startCourseCol + i;

                var parts = peer.Course.Code.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var prefix = parts.FirstOrDefault() ?? "";
                var suffix = parts.Length > 1 ? parts[1] : "";

                ws.Cell(2, col).Value = prefix;
                ws.Cell(3, col).Value = suffix;
                ws.Cell(4, col).Value = peer.Course.CreditUnits;
            }

            // Delete unused course columns
            int deleteStartCol = startCourseCol + numCourses;
            int deleteEndCol = 38;
            if (deleteEndCol >= deleteStartCol)
            {
                ws.Columns(deleteStartCol, deleteEndCol).Delete();
            }

            int deletedCount = deleteEndCol - deleteStartCol + 1;
            int regUnitsCol = 39 - deletedCount;
            int passedUnitsCol = 40 - deletedCount;
            int failedUnitsCol = 41 - deletedCount;
            int totalGpCol = 42 - deletedCount;
            int gpaCol = 43 - deletedCount;
            int remarksCol = 44 - deletedCount;

            // Clear dummy rows
            ws.Rows(6, 327).Clear(XLClearOptions.Contents);

            // ── Populate Student Data Rows ─────────────────────────────────
            int currentRow = 6;
            for (int k = 0; k < cohortStudents.Count; k++)
            {
                var student = cohortStudents[k];
                int r1 = currentRow;
                int r2 = currentRow + 1;

                // S/N
                ws.Cell(r1, 1).Value = k + 1;
                ws.Range(r1, 1, r2, 1).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(r1, 1, r2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Matric No
                studentNumberMap.TryGetValue(student.Id, out var matricNum);
                ws.Cell(r1, 2).Value = matricNum ?? "N/A";
                ws.Range(r1, 2, r2, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(r1, 2, r2, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Name
                ws.Cell(r1, 3).Value = student.DisplayName ?? "Unknown";
                ws.Range(r1, 3, r2, 8).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Range(r1, 3, r2, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Course scores & grades
                double totalRegisteredUnits = 0;
                double totalPassedUnits = 0;
                double totalFailedUnits = 0;
                double totalGradePoints = 0;
                var outstandingList = new List<string>();

                for (int i = 0; i < numCourses; i++)
                {
                    int col = startCourseCol + i;
                    var peer = uniquePeerOfferings[i];

                    double? score = null;
                    if (allSummaries.TryGetValue(peer.Id, out var peerSummaries))
                    {
                        var studSummary = peerSummaries.FirstOrDefault(s => s.StudentId == student.Id);
                        if (studSummary != null)
                        {
                            score = (double)studSummary.TotalScore;
                        }
                    }

                    if (score.HasValue)
                    {
                        var gp = getGradeAndPoints(score.Value);
                        totalRegisteredUnits += peer.Course.CreditUnits;

                        if (gp.Grade != "F")
                        {
                            totalPassedUnits += peer.Course.CreditUnits;
                        }
                        else
                        {
                            totalFailedUnits += peer.Course.CreditUnits;
                            outstandingList.Add($"{peer.Course.Code} ({peer.Course.CreditUnits})");
                        }

                        totalGradePoints += gp.Points * peer.Course.CreditUnits;

                        // Row 1: Score
                        ws.Cell(r1, col).Value = Math.Round(score.Value, 1);
                        ws.Cell(r1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // Row 2: Grade formula
                        var scoreCellRef = ws.Cell(r1, col).Address.ToString();
                        ws.Cell(r2, col).FormulaA1 = $"=IFS({scoreCellRef}>=70,\"A\",{scoreCellRef}>=60,\"B\",{scoreCellRef}>=50,\"C\",{scoreCellRef}>=45,\"D\",{scoreCellRef}>=40,\"E\",{scoreCellRef}<40,\"F\")";
                        ws.Cell(r2, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }

                // Summary metrics
                // Total Registered
                ws.Cell(r2, regUnitsCol).Value = totalRegisteredUnits;
                ws.Cell(r2, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Total Passed
                ws.Cell(r2, passedUnitsCol).Value = totalPassedUnits;
                ws.Cell(r2, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Total Failed
                ws.Cell(r2, failedUnitsCol).Value = totalFailedUnits;
                ws.Cell(r2, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Total Grade Point
                ws.Cell(r1, totalGpCol).Value = totalGradePoints;
                ws.Range(r1, totalGpCol, r2, totalGpCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(r1, totalGpCol, r2, totalGpCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // GPA
                var totalGpCell = ws.Cell(r1, totalGpCol).Address.ToString();
                var regUnitsCell = ws.Cell(r2, regUnitsCol).Address.ToString();
                ws.Cell(r1, gpaCol).FormulaA1 = $"=IF({regUnitsCell}>0, ROUND({totalGpCell}/{regUnitsCell}, 2), 0)";
                ws.Range(r1, gpaCol, r2, gpaCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(r1, gpaCol, r2, gpaCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Remarks
                string remarksVal = outstandingList.Count > 0 ? string.Join(", ", outstandingList) : "PASS";
                ws.Cell(r1, remarksCol).Value = remarksVal;
                ws.Range(r1, remarksCol, r2, remarksCol).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(r1, remarksCol, r2, remarksCol).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Borders and styles
                var studentRange = ws.Range(r1, 1, r2, remarksCol);
                studentRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                studentRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                studentRange.Style.Font.FontName = "Aptos Narrow";
                studentRange.Style.Font.FontSize = 10;

                currentRow += 2;
            }
        }

        if (!hasAnyWorksheet)
        {
            var ws = workbook.Worksheets.Add("No Results");
            ws.Cell(1, 1).Value = "No active course offerings or registrations found for this college, level, and semester.";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 12;
            ws.Column(1).AdjustToContents();
        }

        // Delete other sheets to return only the newly generated results
        var sheetsToDelete = workbook.Worksheets.Where(x => x.Name != "No Results" && !programs.Any(p => x.Name == (p.Code.Length > 30 ? p.Code.Substring(0, 30) : p.Code) || x.Name == (p.Name.Length > 30 ? p.Name.Substring(0, 30) : p.Name))).ToList();
        foreach (var sheet in sheetsToDelete)
        {
            workbook.Worksheets.Delete(sheet.Name);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var cleanFacultyName = faculty.Name.Replace(" ", "_");
        var cleanSessionName = session.Name.Replace("/", "_");
        var fileName = $"SenateResult_{cleanFacultyName}_{cleanSessionName}_{semesterLabel}Sem.xlsx";

        return new GradebookExcelTemplateDto(
            stream.ToArray(),
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }


    public async Task<ErrorOr<GradeUploadResultDto>> BulkUploadGradesAsync(
        Guid courseOfferingId,
        IFormFile excelFile,
        Guid userId,
        CancellationToken ct = default)
    {
        if (excelFile == null || excelFile.Length == 0)
            return Error.Validation("File.Required", "Please provide an Excel file");

        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        // Check if grades are published
        var isPublished = await _dbContext.GradePublications
            .AnyAsync(x => x.CourseOfferingId == courseOfferingId && x.IsVisibleToStudents, ct);

        if (isPublished)
            return Error.Forbidden("Grade.Published", "Cannot upload grades after publication");

        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var errors = new List<string>();
        var successCount = 0;
        var totalRecords = 0;

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream, ct);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet("Gradebook");

            // Find data rows (skip header rows)
            var rows = worksheet.RowsUsed().Skip(3); // Skip title, blank, and header rows

            foreach (var row in rows)
            {
                totalRecords++;
                var studentIdCell = row.Cell(1).GetValue<string>();

                if (string.IsNullOrWhiteSpace(studentIdCell))
                    continue;

                if (!Guid.TryParse(studentIdCell, out var studentId))
                {
                    errors.Add($"Row {row.RowNumber()}: Invalid Student ID format");
                    continue;
                }

                // Process each category column
                int col = 4;
                foreach (var category in categories)
                {
                    var marksCell = row.Cell(col).GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(marksCell) && decimal.TryParse(marksCell, out var marks))
                    {
                        // Find or create an assessment for this category
                        var assessment = assessments.FirstOrDefault(a => a.AssessmentCategoryId == category.Id);

                        if (assessment == null)
                        {
                            // Create a default assessment if none exists
                            assessment = new Assessment
                            {
                                CourseOfferingId = courseOfferingId,
                                AssessmentCategoryId = category.Id,
                                Title = $"{category.CategoryName} Assessment",
                                MaxMarks = category.MaxMarks
                            };
                            _dbContext.Assessments.Add(assessment);
                            await _dbContext.SaveChangesAsync(ct);
                            assessments.Add(assessment);
                        }

                        // Enter the grade
                        var grade = await _dbContext.Grades
                            .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == studentId, ct);

                        if (grade == null)
                        {
                            grade = new Grade
                            {
                                AssessmentId = assessment.Id,
                                StudentId = studentId,
                                MarksObtained = marks,
                                CreatedById = userId,
                                UpdatedById = userId
                            };
                            _dbContext.Grades.Add(grade);
                        }
                        else if (!grade.IsLocked)
                        {
                            grade.MarksObtained = marks;
                            grade.UpdatedById = userId;
                            grade.UpdatedAt = DateTime.UtcNow;
                        }

                        successCount++;
                    }
                    col++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);

            await _auditService.LogAsync("BulkUploadGrades", "Gradebook",
                courseOfferingId.ToString(), $"Bulk uploaded {successCount} grades", ct);
        }
        catch (Exception ex)
        {
            errors.Add($"Error processing file: {ex.Message}");
        }

        return new GradeUploadResultDto(
            Guid.Empty,
            totalRecords,
            successCount,
            totalRecords > 0 ? totalRecords - successCount : 0,
            errors);
    }

    #endregion

    #region Gradebook Summary

    public async Task<ErrorOr<GradebookSummaryDto>> GetGradebookSummaryAsync(
        Guid courseOfferingId,
        Guid? userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .Include(x => x.Programs).ThenInclude(p => p.Program)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var categories = await EnsureAssessmentCategoriesAsync(courseOfferingId, ct);
        categories = categories.OrderBy(x => x.DisplayOrder).ToList();

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.AssessmentCategory)
            .OrderBy(x => x.AssessmentCategory.DisplayOrder)
            .ToListAsync(ct);

        var totalStudents = await _dbContext.CourseEnrollments
            .CountAsync(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered", ct);

        var gradesEntered = await _dbContext.Grades
            .Where(g => assessments.Select(a => a.Id).Contains(g.AssessmentId))
            .CountAsync(ct);

        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        var approvals = await _dbContext.GradeApprovals
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.ApprovalOrder)
            .ToListAsync(ct);

        // Check if user has access
        var userIdStr = userId?.ToString() ?? string.Empty;
        var isLecturer = userId.HasValue &&
                         await _dbContext.CourseOfferingLecturers.AnyAsync(col =>
                             col.CourseOfferingId == offering.Id && col.LecturerId == userId.Value, ct);

        if (userId.HasValue && !isLecturer)
        {
            // Check if user has admin/approval role
            var userRoles = await _dbContext.UserRoles
                .Where(ur => ur.UserId == userId.Value)
                .Select(ur => ur.Role.Name)
                .ToListAsync(ct);

            if (!userRoles.Any(r => r == "Admin" || r == "SuperAdmin" || r == "HOD" || r == "Dean"))
                return Error.Forbidden("Access.Denied", "You do not have access to this gradebook");
        }

        return new GradebookSummaryDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.Programs.FirstOrDefault()?.Program?.Name ?? "N/A",
            offering.Programs.FirstOrDefault()?.Level?.Name ?? "N/A",
            offering.AcademicSession.Name,
            (int)offering.Semester,
            categories.Select(MapToCategoryDto).ToList(),
            assessments.Select(a => MapToAssessmentDto(a, 0)).ToList(),
            totalStudents,
            gradesEntered,
            publication?.IsVisibleToStudents ?? false,
            publication?.ApprovalWorkflowCompleted ?? false,
            approvals.Select(MapToApprovalDto).ToList());
    }

    public async Task<ErrorOr<List<GradeDistributionDto>>> GetGradeDistributionAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var summariesResult = await GetStudentGradeSummariesAsync(courseOfferingId, ct);
        if (summariesResult.IsError)
            return summariesResult.Errors;

        var summaries = summariesResult.Value;

        var distribution = summaries
            .GroupBy(s => s.LetterGrade)
            .Select(g => new GradeDistributionDto(g.Key, g.Count()))
            .ToList();

        // Ensure standard grades are present even if count is 0
        var standardGrades = new[] { "A", "B", "C", "D", "E", "F" };
        foreach (var grade in standardGrades)
        {
            if (!distribution.Any(d => d.LetterGrade == grade))
            {
                distribution.Add(new GradeDistributionDto(grade, 0));
            }
        }

        return distribution.OrderBy(d => d.LetterGrade).ToList();
    }

    #endregion

    #region Approval Workflow

    public async Task<ErrorOr<List<GradeApprovalDto>>> GetGradeApprovalsAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var approvals = await _dbContext.GradeApprovals
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.ApprovalOrder)
            .Include(x => x.ApprovedBy)
            .ToListAsync(ct);

        return approvals.Select(MapToApprovalDto).ToList();
    }

    public async Task<ErrorOr<GradeApprovalDto>> SubmitForApprovalAsync(
        Guid courseOfferingId,
        SubmitForApprovalRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        // Verify user is the lecturer
        var userIdStr = userId.ToString();
        var isLecturer = await _dbContext.CourseOfferingLecturers.AnyAsync(col =>
                             col.CourseOfferingId == offering.Id && col.LecturerId == userId, ct);

        if (!isLecturer)
            return Error.Forbidden("Access.Denied", "Only the assigned lecturer can submit for approval");

        // Check if already published
        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication?.IsVisibleToStudents == true)
            return Error.Conflict("Already.Published", "Grades are already published");

        // Get system configuration
        var sysConfig = await GetSystemConfigurationAsync(ct);
        if (sysConfig.IsError)
            return sysConfig.FirstError;

        // Create approval records if workflow is enabled
        if (sysConfig.Value.ApprovalWorkflowEnabled)
        {
            // Check if approvals already exist
            var existingApprovals = await _dbContext.GradeApprovals
                .Where(x => x.CourseOfferingId == courseOfferingId)
                .ToListAsync(ct);

            if (!existingApprovals.Any())
            {
                // Create Department level approval
                var deptApproval = new GradeApproval
                {
                    CourseOfferingId = courseOfferingId,
                    Level = ApprovalLevel.Department,
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    ApprovalOrder = 1
                };
                _dbContext.GradeApprovals.Add(deptApproval);

                // Create College level approval
                var collegeApproval = new GradeApproval
                {
                    CourseOfferingId = courseOfferingId,
                    Level = ApprovalLevel.College,
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    ApprovalOrder = 2
                };
                _dbContext.GradeApprovals.Add(collegeApproval);

                // Create Senate level approval
                var senateApproval = new GradeApproval
                {
                    CourseOfferingId = courseOfferingId,
                    Level = ApprovalLevel.Senate,
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    ApprovalOrder = 3
                };
                _dbContext.GradeApprovals.Add(senateApproval);

                await _dbContext.SaveChangesAsync(ct);

                await _auditService.LogAsync("SubmitForApproval", "GradeApproval",
                    courseOfferingId.ToString(), request.Comments ?? "Submitted for approval", ct);
            }
        }

        return await GetNextPendingApprovalAsync(courseOfferingId, ct)
            ?? new GradeApprovalDto(Guid.Empty, ApprovalLevel.Department, ApprovalStatus.Pending, null, null, null, null, false, 1);
    }

    public async Task<ErrorOr<GradeApprovalDto>> ApproveGradesAsync(
        Guid courseOfferingId,
        ApproveGradesRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var authResult = await ValidateApprovalAuthorityAsync(offering, userId, request.Level, ct);
        if (authResult.IsError)
            return authResult.FirstError;

        var approval = await _dbContext.GradeApprovals
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId && x.Level == request.Level, ct);

        if (approval == null)
            return Error.NotFound("Approval.NotFound", "Approval record not found");

        if (approval.Status != ApprovalStatus.Pending)
            return Error.Conflict("Approval.AlreadyProcessed", "This approval has already been processed");

        // Check if previous levels are approved
        var previousApprovals = await _dbContext.GradeApprovals
            .Where(x => x.CourseOfferingId == courseOfferingId && x.ApprovalOrder < approval.ApprovalOrder)
            .ToListAsync(ct);

        if (previousApprovals.Any(x => x.Status != ApprovalStatus.Approved))
            return Error.Forbidden("Approval.PreviousPending", "Previous approval levels must be approved first");

        approval.Status = ApprovalStatus.Approved;
        approval.ApprovedById = userId;
        approval.ApprovedAt = DateTime.UtcNow;
        approval.Comments = request.Comments;
        approval.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("ApproveGrades", "GradeApproval",
            approval.Id.ToString(), $"Approved at {request.Level} level", ct);

        return MapToApprovalDto(approval);
    }

    public async Task<ErrorOr<GradeApprovalDto>> RejectGradesAsync(
        Guid courseOfferingId,
        RejectGradesRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Comments))
            return Error.Validation("Comments.Required", "Comments are required when rejecting grades");

        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var authResult = await ValidateApprovalAuthorityAsync(offering, userId, request.Level, ct);
        if (authResult.IsError)
            return authResult.FirstError;

        var approval = await _dbContext.GradeApprovals
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId && x.Level == request.Level, ct);

        if (approval == null)
            return Error.NotFound("Approval.NotFound", "Approval record not found");

        approval.Status = ApprovalStatus.Rejected;
        approval.ApprovedById = userId;
        approval.ApprovedAt = DateTime.UtcNow;
        approval.Comments = request.Comments;
        approval.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("RejectGrades", "GradeApproval",
            approval.Id.ToString(), $"Rejected at {request.Level} level: {request.Comments}", ct);

        return MapToApprovalDto(approval);
    }

    /// <summary>
    /// Validates that the user has authority to approve/reject grades for the given course offering.
    /// Allowed if user has an admin role OR is the assigned lecturer for the offering.
    /// </summary>
    private async Task<ErrorOr<Success>> ValidateApprovalAuthorityAsync(CourseOffering offering, Guid userId, ApprovalLevel? requestedLevel, CancellationToken ct)
    {
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin");
        if (isAdmin) return Result.Success;

        if (requestedLevel == ApprovalLevel.Department)
        {
            var firstProgramId = (await _dbContext.CourseOfferingPrograms
                .Where(p => p.CourseOfferingId == offering.Id)
                .Select(p => (Guid?)p.ProgramId)
                .FirstOrDefaultAsync(ct));
            var program = firstProgramId.HasValue ? await _dbContext.Programs
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == firstProgramId.Value, ct) : null;
            
            if (program?.Department?.HeadId != userId)
                return Error.Forbidden("Approval.AccessDenied", "You are not the Head of Department for this course.");
            
            return Result.Success;
        }
        else if (requestedLevel == ApprovalLevel.College)
        {
            var firstProgramId2 = (await _dbContext.CourseOfferingPrograms
                .Where(p => p.CourseOfferingId == offering.Id)
                .Select(p => (Guid?)p.ProgramId)
                .FirstOrDefaultAsync(ct));
            var program = firstProgramId2.HasValue ? await _dbContext.Programs
                .Include(p => p.Department)
                    .ThenInclude(d => d.Faculty)
                .FirstOrDefaultAsync(p => p.Id == firstProgramId2.Value, ct) : null;
                
            if (program?.Department?.Faculty?.DeanId != userId)
                return Error.Forbidden("Approval.AccessDenied", "You are not the Dean of the Faculty for this course.");
            
            return Result.Success;
        }

        var userIdStr = userId.ToString();
        var isLecturer = await _dbContext.CourseOfferingLecturers.AnyAsync(col =>
                             col.CourseOfferingId == offering.Id && col.LecturerId == userId, ct);

        if (!isLecturer)
            return Error.Forbidden("Approval.AccessDenied", "You are not authorized to approve or reject grades for this course");

        return Result.Success;
    }

    #endregion

    #region Publication

    public async Task<ErrorOr<GradePublicationDto>> GetPublicationStatusAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var publication = await _dbContext.GradePublications
            .Include(x => x.PublishedBy)
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication == null)
        {
            return new GradePublicationDto(
                Guid.Empty,
                DateTime.MinValue,
                Guid.Empty,
                "Not Published",
                false,
                false,
                "Grades not yet published");
        }

        return MapToPublicationDto(publication);
    }

    public async Task<ErrorOr<GradePublicationDto>> PublishGradesAsync(
        Guid courseOfferingId,
        PublishGradesRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        // Get system configuration
        var sysConfig = await GetSystemConfigurationAsync(ct);
        if (sysConfig.IsError)
            return sysConfig.FirstError;

        var approvalWorkflowCompleted = false;

        // Check approval workflow if enabled
        if (sysConfig.Value.ApprovalWorkflowEnabled)
        {
            var approvals = await _dbContext.GradeApprovals
                .Where(x => x.CourseOfferingId == courseOfferingId && x.IsRequired)
                .ToListAsync(ct);

            if (approvals.Any() && !approvals.All(x => x.Status == ApprovalStatus.Approved))
                return Error.Forbidden("Approval.Incomplete", "All approval levels must be approved before publishing");

            approvalWorkflowCompleted = approvals.Any() && approvals.All(x => x.Status == ApprovalStatus.Approved);
        }

        // Lock all grades
        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var grades = await _dbContext.Grades
            .Where(g => assessments.Select(a => a.Id).Contains(g.AssessmentId))
            .ToListAsync(ct);

        foreach (var grade in grades)
        {
            grade.IsLocked = true;
        }

        // Create or update publication
        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication == null)
        {
            publication = new GradePublication
            {
                CourseOfferingId = courseOfferingId,
                PublishedById = userId,
                IsVisibleToStudents = true,
                ApprovalWorkflowCompleted = approvalWorkflowCompleted,
                PublicationNotes = request.PublicationNotes,
                AcademicSessionId = offering.AcademicSessionId,
                Semester = (int)offering.Semester
            };
            _dbContext.GradePublications.Add(publication);
        }
        else
        {
            publication.IsVisibleToStudents = true;
            publication.ApprovalWorkflowCompleted = approvalWorkflowCompleted;
            publication.PublicationNotes = request.PublicationNotes;
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("PublishGrades", "GradePublication",
            publication.Id.ToString(), "Published grades", ct);

        // Notify students
        var enrolledStudents = await _dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered")
            .Select(e => e.StudentId)
            .ToListAsync(ct);

        var courseCode = offering?.Course?.Code ?? "your course";
        foreach (var studentId in enrolledStudents)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest(
                studentId,
                userId,
                "Grades Published",
                $"Grades for {courseCode} have been published.",
                "System",
                $"/courses/{courseOfferingId}/grades"
            ), ct);
        }

        return MapToPublicationDto(publication);
    }

    public async Task<ErrorOr<Deleted>> UnpublishGradesAsync(Guid courseOfferingId, Guid userId, CancellationToken ct = default)
    {
        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication == null)
            return Error.NotFound("Publication.NotFound", "Publication not found");

        publication.IsVisibleToStudents = false;
        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("UnpublishGrades", "GradePublication",
            publication.Id.ToString(), "Unpublished grades", ct);

        return Result.Deleted;
    }

    /// <summary>
    /// Checks if the user has authority to perform grade management actions for the given course offering.
    /// Allowed if user has an admin role OR is the assigned lecturer for the offering.
    /// </summary>
    private async Task<ErrorOr<Success>> ValidateGradeManagementAuthorityAsync(CourseOffering offering, Guid userId, CancellationToken ct)
    {
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin" || r == "HOD" || r == "Dean");
        var userIdStr = userId.ToString();
        var isLecturer = await _dbContext.CourseOfferingLecturers.AnyAsync(col =>
                             col.CourseOfferingId == offering.Id && col.LecturerId == userId, ct);

        if (!isAdmin && !isLecturer)
            return Error.Forbidden("GradeManagement.AccessDenied", "You are not authorized to manage grades for this course");

        return Result.Success;
    }

    /// <summary>
    /// Unlocks all grades for a course offering so they can be edited again.
    /// Intended for use after unpublishing grades that require corrections.
    /// </summary>
    public async Task<ErrorOr<int>> UnlockGradesAsync(Guid courseOfferingId, Guid userId, CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var authResult = await ValidateGradeManagementAuthorityAsync(offering, userId, ct);
        if (authResult.IsError)
            return authResult.FirstError;

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var grades = await _dbContext.Grades
            .Where(g => assessments.Select(a => a.Id).Contains(g.AssessmentId) && g.IsLocked)
            .ToListAsync(ct);

        var unlockedCount = 0;
        foreach (var grade in grades)
        {
            grade.IsLocked = false;
            unlockedCount++;
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("UnlockGrades", "Gradebook",
            courseOfferingId.ToString(), $"Unlocked {unlockedCount} grades", ct);

        return unlockedCount;
    }

    #endregion

    #region Course Listing

    /// <summary>
    /// Returns all course offerings visible to the requesting user for use as a course selector.
    /// Admins/HOD/Deans see all courses; regular lecturers see only their own.
    /// An optional searchTerm filters by course code or title.
    /// </summary>
    public async Task<ErrorOr<List<CourseOfferingSummaryDto>>> GetAllCoursesForGradebookAsync(Guid userId, string? searchTerm = null, CancellationToken ct = default)
    {
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin" || r == "HOD" || r == "Dean");

        var query = _dbContext.CourseOfferings
            .Include(x => x.Course)
            .Include(x => x.Programs).ThenInclude(p => p.Program)
            .Include(x => x.Programs).ThenInclude(p => p.Level)
            .Include(x => x.AcademicSession)
            .Include(x => x.Lecturers).ThenInclude(l => l.Lecturer)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(x => _dbContext.CourseOfferingLecturers
                .Any(col => col.CourseOfferingId == x.Id && col.LecturerId == userId));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(x =>
                x.Course.Code.ToLower().Contains(term) ||
                x.Course.Title.ToLower().Contains(term));
        }

        var offerings = await query
            .OrderByDescending(x => x.AcademicSession.StartDate)
            .ThenBy(x => x.Course.Code)
            .ToListAsync(ct);

        var result = new List<CourseOfferingSummaryDto>();
        foreach (var offering in offerings)
        {
            var isPublished = await _dbContext.GradePublications
                .AnyAsync(x => x.CourseOfferingId == offering.Id && x.IsVisibleToStudents, ct);

            result.Add(new CourseOfferingSummaryDto(
                offering.Id,
                offering.Course.Code,
                offering.Course.Title,
                string.Join(", ", offering.Programs.Select(p => p.Program?.Name).Distinct()),
                string.Join(", ", offering.Programs.Select(p => p.Level?.Name).Distinct()),
                offering.AcademicSession.Name,
                (int)offering.Semester,
                isPublished,
                offering.Lecturers.FirstOrDefault(l => l.Role == Data.Enums.CourseLecturerRole.Main)?.Lecturer?.DisplayName,
                offering.AcademicSession.IsActive));
        }

        return result;
    }

    #endregion

    #region Student View

    public async Task<ErrorOr<StudentGradeViewDto>> GetStudentGradesAsync(
        Guid courseOfferingId,
        Guid studentId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings
            .Include(x => x.Course)
            .Include(x => x.AcademicSession)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        // Check if grades are published and visible
        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication?.IsVisibleToStudents != true)
            return Error.Forbidden("Grades.NotPublished", "Grades are not yet published");

        // Get system configuration
        var sysConfig = await GetSystemConfigurationAsync(ct);
        if (sysConfig.IsError)
            return sysConfig.FirstError;

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.AssessmentCategory)
            .ToListAsync(ct);

        var grades = await _dbContext.Grades
            .Where(x => x.StudentId == studentId && assessments.Select(a => a.Id).Contains(x.AssessmentId))
            .ToListAsync(ct);

        var assessmentGrades = new List<StudentAssessmentGradeDto>();
        decimal totalScore = 0;

        foreach (var assessment in assessments)
        {
            var grade = grades.FirstOrDefault(g => g.AssessmentId == assessment.Id);
            var marks = grade?.MarksObtained ?? 0;
            var percentage = assessment.MaxMarks > 0 ? (marks / assessment.MaxMarks) * 100 : 0;
            var weightedScore = percentage * assessment.AssessmentCategory.Weight / 100;

            assessmentGrades.Add(new StudentAssessmentGradeDto(
                assessment.AssessmentCategory.CategoryName,
                assessment.Title,
                marks,
                assessment.MaxMarks,
                assessment.AssessmentCategory.Weight,
                Math.Round(weightedScore, 2)));

            if (sysConfig.Value.DefaultGradingStyle == nameof(GradingStyle.Weighted))
            {
                totalScore += weightedScore;
            }
        }

        if (sysConfig.Value.DefaultGradingStyle == nameof(GradingStyle.Unweighted) && assessmentGrades.Any())
        {
            totalScore = (decimal)assessmentGrades.Average(x => x.MarksObtained / x.MaxMarks * 100);
        }

        return new StudentGradeViewDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            assessmentGrades,
            Math.Round(totalScore, 2),
            CalculateLetterGrade(totalScore, sysConfig.Value.LetterGradesMapping),
            null,
            true);
    }

    public async Task<ErrorOr<List<StudentGradeViewDto>>> GetStudentAllGradesAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var publicationsQuery = _dbContext.GradePublications
            .Where(x => x.IsVisibleToStudents);

        if (academicSessionId.HasValue)
            publicationsQuery = publicationsQuery.Where(x => x.AcademicSessionId == academicSessionId.Value);

        var publications = await publicationsQuery
            .Select(x => x.CourseOfferingId)
            .ToListAsync(ct);

        var results = new List<StudentGradeViewDto>();

        foreach (var courseOfferingId in publications)
        {
            var result = await GetStudentGradesAsync(courseOfferingId, studentId, ct);
            if (!result.IsError)
            {
                results.Add(result.Value);
            }
        }

        return results;
    }

    #endregion

    #region Helper Methods

    private static SystemGradingConfigurationDto MapToSystemConfigurationDto(SystemGradingConfiguration config)
    {
        var mapping = string.IsNullOrEmpty(config.LetterGradesMappingJson) || config.LetterGradesMappingJson == "[]"
            ? new List<GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<GradeMappingDto>>(config.LetterGradesMappingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<GradeMappingDto>();

        return new SystemGradingConfigurationDto(
            config.Id,
            config.DefaultGradingStyle.ToString(),
            config.DefaultExamPercentage,
            config.ApprovalWorkflowEnabled,
            config.DefaultCA1Weight,
            config.DefaultCA2Weight,
            config.DefaultCA3Weight,
            config.DefaultExamWeight,
            config.GpaScale,
            mapping,
            config.RoundingStrategy.ToString(),
            config.RoundingDecimalPlaces,
            config.GraceThreshold,
            config.UpdatedAt);
    }

    private static AssessmentCategoryDto MapToCategoryDto(AssessmentCategory category)
    {
        return new AssessmentCategoryDto(
            category.Id,
            category.CategoryType,
            category.CategoryName,
            category.Weight,
            category.MaxMarks,
            category.IsExamCategory,
            category.DisplayOrder);
    }

    private static AssessmentDto MapToAssessmentDto(Assessment assessment, int gradesCount)
    {
        return new AssessmentDto(
            assessment.Id,
            assessment.AssessmentCategoryId,
            assessment.AssessmentCategory?.CategoryName ?? "",
            assessment.Title,
            assessment.Description,
            assessment.MaxMarks,
            assessment.AssessmentDate,
            assessment.DueDate,
            gradesCount);
    }

    private static GradeDto MapToGradeDto(Grade grade, decimal maxMarks)
    {
        return new GradeDto(
            grade.Id,
            grade.AssessmentId,
            grade.StudentId,
            grade.Student?.DisplayName ?? "Unknown",
            grade.Student?.Email ?? "",
            grade.MarksObtained,
            maxMarks,
            maxMarks > 0 ? Math.Round(grade.MarksObtained / maxMarks * 100, 2) : 0,
            grade.IsLocked,
            grade.Remarks,
            grade.UpdatedAt);
    }

    private static GradeApprovalDto MapToApprovalDto(GradeApproval approval)
    {
        return new GradeApprovalDto(
            approval.Id,
            approval.Level,
            approval.Status,
            approval.ApprovedById,
            approval.ApprovedBy?.DisplayName,
            approval.ApprovedAt,
            approval.Comments,
            approval.IsRequired,
            approval.ApprovalOrder);
    }

    private static GradePublicationDto MapToPublicationDto(GradePublication publication)
    {
        return new GradePublicationDto(
            publication.Id,
            publication.PublishedAt,
            publication.PublishedById,
            publication.PublishedBy?.DisplayName ?? "Unknown",
            publication.IsVisibleToStudents,
            publication.ApprovalWorkflowCompleted,
            publication.PublicationNotes);
    }

    private decimal CalculateCategoryScore(List<Assessment> assessments, List<AssessmentCategory> categories, Guid studentId, AssessmentCategoryType categoryType)
    {
        var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
        if (category == null) return 0;

        var categoryAssessments = assessments.Where(a => a.AssessmentCategoryId == category.Id).ToList();
        if (!categoryAssessments.Any()) return 0;

        var totalMarks = 0m;
        var totalMaxMarks = 0m;

        foreach (var assessment in categoryAssessments)
        {
            var grade = assessment.Grades.FirstOrDefault(g => g.StudentId == studentId);
            totalMarks += grade?.MarksObtained ?? 0;
            totalMaxMarks += assessment.MaxMarks;
        }

        if (totalMaxMarks == 0) return 0;
        return totalMarks / totalMaxMarks * 100; // Return percentage
    }

    private decimal CalculateUnweightedAverage(decimal ca1, decimal ca2, decimal ca3, decimal exam)
    {
        var scores = new[] { ca1, ca2, ca3, exam }.Where(s => s >= 0).ToList();
        return (decimal)(scores.Any() ? scores.Average() : 0);
    }

    private string CalculateLetterGrade(decimal percentage, List<GradeMappingDto>? mappings = null)
    {
        SystemGradingConfiguration? sysConfig = null;
        try
        {
            sysConfig = _dbContext.SystemGradingConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            if (mappings == null || !mappings.Any())
            {
                if (sysConfig != null && !string.IsNullOrEmpty(sysConfig.LetterGradesMappingJson) && sysConfig.LetterGradesMappingJson != "[]")
                {
                    mappings = JsonSerializer.Deserialize<List<GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
        }
        catch
        {
            // Fallback to default
        }

        var rStrategy = sysConfig?.RoundingStrategy ?? RoundingStrategy.Standard;
        var decimalPlaces = sysConfig?.RoundingDecimalPlaces ?? 0;
        var graceThreshold = sysConfig?.GraceThreshold ?? 0.0m;

        var result = GradeCalculator.CalculateGrade(percentage, rStrategy, decimalPlaces, graceThreshold, mappings ?? new List<GradeMappingDto>());
        return result.LetterGrade;
    }

    private async Task<GradeApprovalDto?> GetNextPendingApprovalAsync(Guid courseOfferingId, CancellationToken ct)
    {
        var nextApproval = await _dbContext.GradeApprovals
            .Where(x => x.CourseOfferingId == courseOfferingId && x.Status == ApprovalStatus.Pending)
            .OrderBy(x => x.ApprovalOrder)
            .Include(x => x.ApprovedBy)
            .FirstOrDefaultAsync(ct);

        return nextApproval == null ? null : MapToApprovalDto(nextApproval);
    }

    #endregion

    #region Classter Migration

    public async Task<ErrorOr<GradeUploadResultDto>> MigrateClassterGradesAsync(
        Guid academicSessionId,
        Guid courseId,
        IFormFile excelFile,
        Guid userId,
        Guid? uploadId = null,
        CancellationToken ct = default)
    {
        if (excelFile == null || excelFile.Length == 0)
            return Error.Validation("File.Required", "Please provide an Excel file");

        var course = await _dbContext.Courses.FindAsync(courseId);
        if (course == null)
            return Error.NotFound("Course.NotFound", "Course not found");

        var academicSession = await _dbContext.AcademicSessions.FindAsync(academicSessionId);
        if (academicSession == null)
            return Error.NotFound("Session.NotFound", "Academic session not found");

        var upload = await _dbContext.ClassterResultUploads
            .FirstOrDefaultAsync(u => (uploadId != null && u.UploadId == uploadId) || (u.CourseId == courseId && u.AcademicSessionId == academicSessionId), ct);

        if (upload != null)
        {
            upload.FileName = Path.GetFileName(excelFile.FileName) ?? excelFile.FileName;
            upload.Status = ClassterUploadStatus.Processing;
            upload.TotalRows = 0;
            upload.ProcessedRows = 0;
            upload.SuccessfulRows = 0;
            upload.FailedRows = 0;
            upload.UpdatedAt = DateTime.UtcNow;
            upload.CompletedAt = null;

            // Drop existing rows in ClassterResultUploadRows
            var existingRows = await _dbContext.ClassterResultUploadRows
                .Where(r => r.UploadId == upload.Id)
                .ToListAsync(ct);
            _dbContext.ClassterResultUploadRows.RemoveRange(existingRows);

            // Drop existing grades for this course offering in this session
            var offeringIds = await _dbContext.CourseOfferings
                .Where(co => co.CourseId == courseId && co.AcademicSessionId == academicSessionId)
                .Select(co => co.Id)
                .ToListAsync(ct);

            if (offeringIds.Any())
            {
                var assessmentIds = await _dbContext.Assessments
                    .Where(a => offeringIds.Contains(a.CourseOfferingId))
                    .Select(a => a.Id)
                    .ToListAsync(ct);

                if (assessmentIds.Any())
                {
                    var gradesToDelete = await _dbContext.Grades
                        .Where(g => assessmentIds.Contains(g.AssessmentId))
                        .ToListAsync(ct);
                    _dbContext.Grades.RemoveRange(gradesToDelete);
                }
            }

            await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            upload = new ClassterResultUpload
            {
                UploadId = uploadId ?? Guid.NewGuid(),
                FileName = Path.GetFileName(excelFile.FileName) ?? excelFile.FileName,
                AcademicSessionId = academicSessionId,
                CourseId = courseId,
                CreatedById = userId,
                Status = ClassterUploadStatus.Processing,
                TotalRows = 0,
                ProcessedRows = 0,
                SuccessfulRows = 0,
                FailedRows = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ClassterResultUploads.Add(upload);
        }

        var errors = new List<string>();
        var successfulRows = 0;
        var failedRows = 0;
        var uploadedGrades = 0;
        var totalRecords = 0;
        var provisionedUsers = 0;
        var provisionedEnrollments = 0;
        var provisionedCourseEnrollments = 0;

        var processedCourseOfferings = new Dictionary<string, CourseOffering>(StringComparer.OrdinalIgnoreCase);

        void AddRowError(int rowNumber, string message)
        {
            failedRows++;
            errors.Add($"Row {rowNumber}: {message}");
        }

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream, ct);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                errors.Add("No worksheet found in Excel file");
                upload.Status = ClassterUploadStatus.Failed;
                await _dbContext.SaveChangesAsync(ct);
                return new GradeUploadResultDto(upload.UploadId, totalRecords, successfulRows, failedRows, errors);
            }

            var headerRow = FindHeaderRow(worksheet);
            if (headerRow == null)
            {
                errors.Add("Could not find header row with required columns (identity number, first name, last name)");
                upload.Status = ClassterUploadStatus.Failed;
                await _dbContext.SaveChangesAsync(ct);
                return new GradeUploadResultDto(upload.UploadId, totalRecords, successfulRows, failedRows, errors);
            }

            var columnMap = BuildColumnMap(headerRow);
            var dataRows = worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber());

            foreach (var row in dataRows)
            {
                var rowNumber = row.RowNumber();
                try
                {
                    var identityNumber = GetCellValue(row, columnMap, "identity number").Trim();
                    var firstName = GetCellValue(row, columnMap, "first name").Trim();
                    var lastName = GetCellValue(row, columnMap, "last name").Trim();
                    if (IsRepeatedHeaderRow(identityNumber, firstName, lastName))
                        continue;

                    totalRecords++;
                    upload.TotalRows++;

                    var quizScore = TryGetDecimalCellValue(row, columnMap, new[] { "quiz" }, out var quiz) ? quiz : (decimal?)null;
                    var assignmentScore = TryGetDecimalCellValue(row, columnMap, new[] { "assignment" }, out var assignment) ? assignment : (decimal?)null;
                    var midsemesterScore = TryGetDecimalCellValue(row, columnMap, new[] { "midsemester test", "mid-semester test", "mid semester test" }, out var midsemester) ? midsemester : (decimal?)null;
                    var examScore = TryGetDecimalCellValue(row, columnMap, new[] { "exam", "examination" }, out var exam) ? exam : (decimal?)null;

                    var rowFingerprint = BuildFingerprint(upload.UploadId, identityNumber, firstName, lastName, quizScore, assignmentScore, midsemesterScore, examScore);
                    var rawPayload = JsonSerializer.Serialize(new
                    {
                        IdentityNumber = identityNumber,
                        FirstName = firstName,
                        LastName = lastName,
                        QuizScore = quizScore,
                        AssignmentScore = assignmentScore,
                        MidsemesterScore = midsemesterScore,
                        ExamScore = examScore,
                        RowValues = row.CellsUsed().Select(c => c.Value.ToString() ?? string.Empty).ToArray()
                    });

                    var rowEntity = await _dbContext.ClassterResultUploadRows
                        .FirstOrDefaultAsync(r => r.UploadId == upload.Id && r.RowNumber == rowNumber, ct);

                    if (rowEntity == null)
                    {
                        rowEntity = new ClassterResultUploadRow
                        {
                            UploadId = upload.Id,
                            RowNumber = rowNumber,
                            CreatedAtUtc = DateTime.UtcNow
                        };
                        _dbContext.ClassterResultUploadRows.Add(rowEntity);
                    }

                    rowEntity.ExternalStudentId = identityNumber;
                    rowEntity.StudentName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    rowEntity.AssessmentType = "Classter Import";
                    rowEntity.MarksObtained = examScore;
                    rowEntity.AttemptNumber = null;
                    rowEntity.Fingerprint = rowFingerprint;
                    rowEntity.MappingStatus = "Pending";
                    rowEntity.MappingReason = null;
                    rowEntity.RawPayload = rawPayload;
                    rowEntity.UpdatedAtUtc = DateTime.UtcNow;

                    if (string.IsNullOrWhiteSpace(identityNumber))
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = "Missing identity number";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    var existingRow = await _dbContext.ClassterResultUploadRows
                        .FirstOrDefaultAsync(r => r.UploadId == upload.Id && r.Fingerprint == rowFingerprint && r.Id != rowEntity.Id, ct);
                    if (existingRow != null)
                    {
                        rowEntity.MappingStatus = "Duplicate";
                        rowEntity.MappingReason = "Duplicate row detected in the same upload";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    var student = await FindStudentAsync(identityNumber, firstName, lastName, ct);
                    if (student == null)
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = $"Student not found (identity: {identityNumber})";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    var (appUser, userCreated) = await ProvisionAppUserAsync(student, ct);
                    if (appUser == null)
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = $"Could not provision user for student (identity: {identityNumber})";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    if (userCreated)
                        provisionedUsers++;

                    var courseOffering = await GetOrCreateCourseOfferingAsync(courseId, student, academicSessionId, ct, processedCourseOfferings);
                    if (courseOffering == null)
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = "Could not create course offering for student";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    rowEntity.CourseOfferingId = courseOffering.Id;

                    var (enrollment, enrollmentCreated) = await ProvisionEnrollmentAsync(student, courseOffering, ct);
                    if (enrollmentCreated)
                        provisionedEnrollments++;

                    var (courseEnrollment, courseEnrollmentCreated) = await ProvisionCourseEnrollmentAsync(student, courseOffering, userId, ct);
                    if (courseEnrollmentCreated)
                        provisionedCourseEnrollments++;

                    var categories = await EnsureAssessmentCategoriesAsync(courseOffering.Id, ct);
                    var assessments = await EnsureAssessmentsAsync(courseOffering.Id, categories, ct);

                    var gradeColumnAliases = new Dictionary<AssessmentCategoryType, string[]>
                    {
                        { AssessmentCategoryType.CA1, new[] { "quiz" } },
                        { AssessmentCategoryType.CA2, new[] { "assignment" } },
                        { AssessmentCategoryType.CA3, new[] { "midsemester test", "mid-semester test", "mid semester test" } },
                        { AssessmentCategoryType.Exam, new[] { "exam", "examination" } }
                    };

                    var rowGradeUploads = 0;
                    foreach (var kvp in gradeColumnAliases)
                    {
                        if (TryGetDecimalCellValue(row, columnMap, kvp.Value, out var marks))
                        {
                            var categoryType = kvp.Key;
                            var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
                            var assessment = category == null
                                ? null
                                : assessments.FirstOrDefault(a => a.AssessmentCategoryId == category.Id);

                            if (assessment != null)
                            {
                                var existingGrade = await _dbContext.Grades
                                    .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == appUser.Id, ct);

                                if (existingGrade == null)
                                {
                                    var grade = new Grade
                                    {
                                        AssessmentId = assessment.Id,
                                        StudentId = appUser.Id,
                                        MarksObtained = marks,
                                        CreatedById = userId,
                                        UpdatedById = userId
                                    };
                                    _dbContext.Grades.Add(grade);
                                    rowGradeUploads++;
                                }
                                else if (!existingGrade.IsLocked)
                                {
                                    existingGrade.MarksObtained = marks;
                                    existingGrade.UpdatedById = userId;
                                    existingGrade.UpdatedAt = DateTime.UtcNow;
                                    rowGradeUploads++;
                                }
                            }
                        }
                    }

                    if (rowGradeUploads == 0)
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = "No valid grade values found";
                        failedRows++;
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    rowEntity.StudentId = student.Id;
                    rowEntity.AssessmentId = null;
                    rowEntity.MappingStatus = "Success";
                    rowEntity.MappingReason = null;
                    rowEntity.ProcessedAtUtc = DateTime.UtcNow;
                    rowEntity.UpdatedAtUtc = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync(ct);

                    uploadedGrades += rowGradeUploads;
                    successfulRows++;
                    upload.SuccessfulRows++;
                    upload.ProcessedRows++;
                }
                catch (Exception ex)
                {
                    _dbContext.ChangeTracker.Clear();
                    AddRowError(rowNumber, ex.Message);
                    upload.FailedRows++;
                    upload.ProcessedRows++;
                }
            }

            upload.Status = upload.FailedRows == upload.TotalRows ? ClassterUploadStatus.Failed : ClassterUploadStatus.Completed;
            upload.UpdatedAt = DateTime.UtcNow;
            upload.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            var auditMessage = $"Classter migration: {uploadedGrades} grades uploaded";
            if (provisionedUsers > 0)
                auditMessage += $", {provisionedUsers} users provisioned";
            if (provisionedEnrollments > 0)
                auditMessage += $", {provisionedEnrollments} enrollments created";
            if (provisionedCourseEnrollments > 0)
                auditMessage += $", {provisionedCourseEnrollments} course enrollments created";

            await _auditService.LogAsync("MigrateClassterGrades", "Gradebook",
                $"{courseId}_{academicSessionId}", auditMessage, ct);
        }
        catch (Exception ex)
        {
            errors.Add($"Error processing file: {ex.Message}");
            upload.Status = ClassterUploadStatus.Failed;
            upload.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        return new GradeUploadResultDto(
            upload.UploadId,
            totalRecords,
            successfulRows,
            failedRows,
            errors);
    }

    private string BuildFingerprint(Guid uploadId, string identityNumber, string firstName, string lastName, decimal? quizScore, decimal? assignmentScore, decimal? midsemesterScore, decimal? examScore)
    {
        var payload = string.Join("|", uploadId.ToString(), NormalizeValue(identityNumber), NormalizeValue(firstName), NormalizeValue(lastName), quizScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, assignmentScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, midsemesterScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, examScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private string NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").ToLowerInvariant();
    }

    private IXLRow? FindHeaderRow(IXLWorksheet worksheet)
    {
        var lastRow = worksheet.LastRowUsed();
        if (lastRow == null)
        {
            return null;
        }

        for (int rowNum = 1; rowNum <= Math.Min(5, lastRow.RowNumber()); rowNum++)
        {
            var row = worksheet.Row(rowNum);
            var cells = row.CellsUsed().Select(c => c.Value.ToString().ToLowerInvariant().Trim()).ToList();

            if (cells.Any(c => c.Contains("identity number") || c.Contains("identity")) &&
                cells.Any(c => c.Contains("first name")) &&
                cells.Any(c => c.Contains("last name")))
            {
                return row;
            }
        }
        return null;
    }

    private Dictionary<string, int> BuildColumnMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var value = cell.Value.ToString().ToLowerInvariant().Trim();
            map[value] = cell.WorksheetColumn().ColumnNumber();
        }

        return map;
    }

    private string GetCellValue(IXLRow row, Dictionary<string, int> columnMap, string columnName)
    {
        var normalizedName = columnName.ToLowerInvariant().Trim();
        if (columnMap.TryGetValue(normalizedName, out var colNum))
        {
            var cellValue = row.Cell(colNum).Value.ToString();
            return cellValue?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }

    private bool TryGetDecimalCellValue(IXLRow row, Dictionary<string, int> columnMap, string[] columnAliases, out decimal value)
    {
        foreach (var alias in columnAliases)
        {
            var cellValue = GetCellValue(row, columnMap, alias);
            if (decimal.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
        }

        // Fallback: partial matching against all header cells.
        // Classter Excel exports often have headers like "CA1 Score", "CA2 Score", "CA3 Score", "Exam Result"
        // which won't match exact aliases like "ca1", "ca2", "ca3", "exam".
        foreach (var alias in columnAliases)
        {
            var normalizedAlias = alias.ToLowerInvariant().Trim();
            foreach (var kvp in columnMap)
            {
                if (kvp.Key.Contains(normalizedAlias))
                {
                    var colNum = kvp.Value;
                    var cellValue = row.Cell(colNum).Value.ToString();
                    if (decimal.TryParse(cellValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        return true;
                }
            }
        }

        value = 0;
        return false;
    }

    private static bool IsRepeatedHeaderRow(string identityNumber, string firstName, string lastName)
    {
        return identityNumber.Equals("identity number", StringComparison.OrdinalIgnoreCase)
            && firstName.Equals("first name", StringComparison.OrdinalIgnoreCase)
            && lastName.Equals("last name", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Student?> FindStudentAsync(string identityNumber, string firstName, string lastName, CancellationToken ct)
    {
        var student = await _dbContext.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == identityNumber, ct);

        if (student != null)
            return student;

        if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
        {
            student = await _dbContext.Students
                .FirstOrDefaultAsync(s => s.FirstName.ToLower() == firstName.ToLower() && s.LastName.ToLower() == lastName.ToLower(), ct);
        }

        return student;
    }

    private async Task<(AppUser? User, bool Created)> ProvisionAppUserAsync(Student student, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(student.EntraObjectId))
        {
            var existingByEntra = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntraObjectId == student.EntraObjectId, ct);
            if (existingByEntra != null)
                return (existingByEntra, false);
        }

        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == student.OfficialEmail, ct);
        if (existingUser != null)
        {
            if (!string.IsNullOrWhiteSpace(student.EntraObjectId))
                existingUser.EntraObjectId = student.EntraObjectId;
            existingUser.DisplayName = $"{student.FirstName} {student.LastName}";
            existingUser.UpdatedUtc = DateTime.UtcNow;
            return (existingUser, false);
        }

        var appUser = new AppUser
        {
            Id = student.Id,
            EntraObjectId = student.EntraObjectId ?? $"student:{student.Id}",
            Email = student.OfficialEmail,
            DisplayName = $"{student.FirstName} {student.LastName}",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.Users.Add(appUser);
        return (appUser, true);
    }

    private async Task<CourseOffering?> GetOrCreateCourseOfferingAsync(
        Guid courseId,
        Student student,
        Guid academicSessionId,
        CancellationToken ct,
        Dictionary<string, CourseOffering>? processedOfferings = null)
    {
        if (!student.AcademicProgramId.HasValue || !student.LevelId.HasValue)
            return null;

        // Build the cache key for duplicate detection: course Code + session + program + level
        var course = await _dbContext.Courses.FindAsync(courseId);
        var courseCode = course?.Code ?? string.Empty;
        var session = await _dbContext.AcademicSessions.FindAsync(academicSessionId);
        var sessionName = session?.Name ?? string.Empty;
        var cacheKey = $"{courseCode}:{sessionName}:{student.AcademicProgramId}:{student.LevelId}";

        // Check in-memory cache first to prevent duplicates within the same migration
        if (processedOfferings != null && processedOfferings.TryGetValue(cacheKey, out var cachedOffering))
        {
            return cachedOffering;
        }

        var curriculumCourse = await _dbContext.CurriculumCourses
            .Include(cc => cc.Curriculum)
            .FirstOrDefaultAsync(cc => cc.CourseId == courseId && cc.LevelId == student.LevelId, ct);
        Guid? curriculumId = curriculumCourse?.Curriculum == null
            ? null
            : curriculumCourse.CurriculumId;
        var semester = curriculumCourse?.Semester
            ?? await _dbContext.Courses
                .Where(c => c.Id == courseId)
                .Select(c => c.Semester)
                .FirstOrDefaultAsync(ct)
            ?? Data.Enums.Semester.First;

        // Find offering by course+session+semester (new normalized model)
        var offering = await _dbContext.CourseOfferings
            .FirstOrDefaultAsync(co => co.CourseId == courseId && co.AcademicSessionId == academicSessionId && co.Semester == semester, ct);

        if (offering == null)
        {
            offering = new CourseOffering
            {
                Id                = Guid.NewGuid(),
                CourseId          = courseId,
                AcademicSessionId = academicSessionId,
                Semester          = semester,
                CurriculumId      = curriculumId
            };
            _dbContext.CourseOfferings.Add(offering);

            try
            {
                await _dbContext.SaveChangesAsync(ct);

                // Attach the student's program/level to the new offering
                if (student.AcademicProgramId.HasValue && student.LevelId.HasValue)
                {
                    var alreadyAttached = await _dbContext.CourseOfferingPrograms.AnyAsync(
                        p => p.CourseOfferingId == offering.Id &&
                             p.ProgramId == student.AcademicProgramId.Value &&
                             p.LevelId == student.LevelId.Value, ct);
                    if (!alreadyAttached)
                    {
                        _dbContext.CourseOfferingPrograms.Add(new CourseOfferingProgram
                        {
                            CourseOfferingId = offering.Id,
                            ProgramId        = student.AcademicProgramId.Value,
                            LevelId          = student.LevelId.Value
                        });
                        await _dbContext.SaveChangesAsync(ct);
                    }
                }
            }
            catch
            {
                _dbContext.Entry(offering).State = EntityState.Detached;
                return null;
            }
        }

        // Track in the in-memory cache to prevent duplicates
        if (processedOfferings != null)
        {
            processedOfferings[cacheKey] = offering;
        }

        return offering;
    }

    private async Task<(ProgramEnrollment? Enrollment, bool Created)> ProvisionEnrollmentAsync(Student student, CourseOffering offering, CancellationToken ct)
    {
        if (!offering.CurriculumId.HasValue)
            return (null, false);

        var appUser = await _dbContext.Users.FirstOrDefaultAsync(u =>
            (!string.IsNullOrWhiteSpace(student.EntraObjectId) && u.EntraObjectId == student.EntraObjectId)
            || u.Email == student.OfficialEmail, ct);
        if (appUser == null)
            return (null, false);

        // Find program enrollment via offering programs
        var offeringProgramId = await _dbContext.CourseOfferingPrograms
            .Where(p => p.CourseOfferingId == offering.Id)
            .Select(p => (Guid?)p.ProgramId)
            .FirstOrDefaultAsync(ct);

        if (!offeringProgramId.HasValue)
            return (null, false);

        var enrollment = await _dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == appUser.Id && e.ProgramId == offeringProgramId.Value && e.AcademicSessionId == offering.AcademicSessionId, ct);

        if (enrollment == null)
        {
            // Find level from program attachment
            var offeringLevelId = await _dbContext.CourseOfferingPrograms
                .Where(p => p.CourseOfferingId == offering.Id && p.ProgramId == offeringProgramId.Value)
                .Select(p => (Guid?)p.LevelId)
                .FirstOrDefaultAsync(ct);

            enrollment = new ProgramEnrollment
            {
                Id                = Guid.NewGuid(),
                ProgramId         = offeringProgramId.Value,
                LevelId           = offeringLevelId ?? Guid.Empty,
                UserId            = appUser.Id,
                AcademicSessionId = offering.AcademicSessionId,
                CurriculumId      = offering.CurriculumId.Value,
                EnrolledAtUtc     = DateTime.UtcNow
            };
            _dbContext.Enrollments.Add(enrollment);
            await _dbContext.SaveChangesAsync(ct);
            return (enrollment, true);
        }

        return (enrollment, false);
    }

    private async Task<(CourseEnrollment? Enrollment, bool Created)> ProvisionCourseEnrollmentAsync(
        Student student,
        CourseOffering offering,
        Guid userId,
        CancellationToken ct)
    {
        var appUser = await _dbContext.Users.FirstOrDefaultAsync(u =>
            (!string.IsNullOrWhiteSpace(student.EntraObjectId) && u.EntraObjectId == student.EntraObjectId)
            || u.Email == student.OfficialEmail, ct);
        if (appUser == null)
            return (null, false);

        var enrollment = await _dbContext.CourseEnrollments
            .FirstOrDefaultAsync(ce => ce.StudentId == appUser.Id && ce.CourseOfferingId == offering.Id, ct);

        if (enrollment == null)
        {
            enrollment = new CourseEnrollment
            {
                Id = Guid.NewGuid(),
                StudentId = appUser.Id,
                CourseOfferingId = offering.Id,
                Status = "Registered",
                RegisteredAtUtc = DateTime.UtcNow,
                CreatedById = userId
            };
            _dbContext.CourseEnrollments.Add(enrollment);
            await _dbContext.SaveChangesAsync(ct);
            return (enrollment, true);
        }
        else if (enrollment.Status != "Registered")
        {
            enrollment.Status = "Registered";
            enrollment.RegisteredAtUtc = DateTime.UtcNow;
            enrollment.DroppedAtUtc = null;
            enrollment.UpdatedById = userId;
            await _dbContext.SaveChangesAsync(ct);
            return (enrollment, false);
        }

        return (enrollment, false);
    }

    private async Task<List<AssessmentCategory>> EnsureAssessmentCategoriesAsync(Guid courseOfferingId, CancellationToken ct)
    {
        var categories = await _dbContext.AssessmentCategories
            .Where(c => c.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var defaultCategories = new[]
        {
            AssessmentCategoryType.CA1,
            AssessmentCategoryType.CA2,
            AssessmentCategoryType.CA3,
            AssessmentCategoryType.Exam
        };

        var sysConfig = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var defaultCA1 = sysConfig?.DefaultCA1Weight ?? 15m;
        var defaultCA2 = sysConfig?.DefaultCA2Weight ?? 15m;
        var defaultCA3 = sysConfig?.DefaultCA3Weight ?? 15m;
        var defaultExam = sysConfig?.DefaultExamWeight ?? 55m;

        foreach (var categoryType in defaultCategories)
        {
            var weight = categoryType switch
            {
                AssessmentCategoryType.CA1 => defaultCA1,
                AssessmentCategoryType.CA2 => defaultCA2,
                AssessmentCategoryType.CA3 => defaultCA3,
                AssessmentCategoryType.Exam => defaultExam,
                _ => 20m
            };

            var existing = categories.FirstOrDefault(c => c.CategoryType == categoryType);
            if (existing == null)
            {
                var category = new AssessmentCategory
                {
                    CourseOfferingId = courseOfferingId,
                    CategoryType = categoryType,
                    CategoryName = categoryType.ToString(),
                    Weight = weight,
                    MaxMarks = 100m,
                    IsExamCategory = categoryType == AssessmentCategoryType.Exam,
                    DisplayOrder = (int)categoryType
                };
                _dbContext.AssessmentCategories.Add(category);
                categories.Add(category);
            }
            else if (existing.Weight != weight)
            {
                existing.Weight = weight;
                _dbContext.AssessmentCategories.Update(existing);
            }
        }

        if (categories.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        return categories;
    }

    private async Task<List<Assessment>> EnsureAssessmentsAsync(Guid courseOfferingId, List<AssessmentCategory> categories, CancellationToken ct)
    {
        var assessments = await _dbContext.Assessments
            .Where(a => a.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        foreach (var category in categories)
        {
            if (!assessments.Any(a => a.AssessmentCategoryId == category.Id))
            {
                var assessment = new Assessment
                {
                    CourseOfferingId = courseOfferingId,
                    AssessmentCategoryId = category.Id,
                    Title = $"{category.CategoryName} Assessment",
                    MaxMarks = category.MaxMarks
                };
                _dbContext.Assessments.Add(assessment);
                assessments.Add(assessment);
            }
        }

        if (categories.Any(c => !assessments.Any(a => a.AssessmentCategoryId == c.Id)))
            await _dbContext.SaveChangesAsync(ct);

        return assessments;
    }

    #endregion
}
