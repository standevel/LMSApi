using System.Globalization;
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

    public GradebookService(LmsDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    #region System Configuration

    public async Task<ErrorOr<SystemGradingConfigurationDto>> GetSystemConfigurationAsync(CancellationToken ct = default)
    {
        var config = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            // Create default configuration
            config = new SystemGradingConfiguration();
            _dbContext.SystemGradingConfigurations.Add(config);
            await _dbContext.SaveChangesAsync(ct);
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

        if (request.DefaultGradingStyle.HasValue)
            config.DefaultGradingStyle = request.DefaultGradingStyle.Value;
        
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

        var students = await _dbContext.Enrollments
            .Where(e => e.ProgramId == offering.ProgramId
                && e.LevelId == offering.LevelId
                && e.AcademicSessionId == offering.AcademicSessionId)
            .Include(e => e.User)
            .ToListAsync(ct);

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
            var ca1Score = CalculateCategoryScore(assessments, categories, student.User.Id, AssessmentCategoryType.CA1);
            var ca2Score = CalculateCategoryScore(assessments, categories, student.User.Id, AssessmentCategoryType.CA2);
            var ca3Score = CalculateCategoryScore(assessments, categories, student.User.Id, AssessmentCategoryType.CA3);
            var examScore = CalculateCategoryScore(assessments, categories, student.User.Id, AssessmentCategoryType.Exam);

            var totalScore = sysConfig.Value.DefaultGradingStyle == GradingStyle.Weighted
                ? (ca1Score * sysConfig.Value.DefaultCA1Weight / 100m) +
                  (ca2Score * sysConfig.Value.DefaultCA2Weight / 100m) +
                  (ca3Score * sysConfig.Value.DefaultCA3Weight / 100m) +
                  (examScore * sysConfig.Value.DefaultExamWeight / 100m)
                : CalculateUnweightedAverage(ca1Score, ca2Score, ca3Score, examScore);

            result.Add(new StudentGradeSummaryDto(
                student.User.Id,
                student.User.DisplayName ?? "Unknown",
                student.User.Email ?? "",
                ca1Score,
                ca2Score,
                ca3Score,
                examScore,
                Math.Round(totalScore, 2),
                CalculateLetterGrade(totalScore),
                null));
        }

        return result.OrderByDescending(x => x.TotalScore).ToList();
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
            .Include(x => x.Program)
            .Include(x => x.Level)
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
        var students = await _dbContext.Enrollments
            .Where(e => e.ProgramId == offering.ProgramId
                && e.LevelId == offering.LevelId
                && e.AcademicSessionId == offering.AcademicSessionId)
            .Include(e => e.User)
            .OrderBy(e => e.User.DisplayName)
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
            worksheet.Cell(row, 1).Value = student.User.Id.ToString();
            worksheet.Cell(row, 2).Value = student.User.DisplayName ?? "Unknown";
            worksheet.Cell(row, 3).Value = student.User.Email ?? "";
            
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
            .Include(x => x.Program)
            .Include(x => x.Level)
            .Include(x => x.AcademicSession)
            .FirstOrDefaultAsync(x => x.Id == courseOfferingId, ct);

        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.AssessmentCategory)
            .OrderBy(x => x.AssessmentCategory.DisplayOrder)
            .ToListAsync(ct);

        var totalStudents = await _dbContext.Enrollments
            .CountAsync(e => e.ProgramId == offering.ProgramId
                && e.LevelId == offering.LevelId
                && e.AcademicSessionId == offering.AcademicSessionId, ct);

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
        if (userId.HasValue && offering.LecturerId != userId.Value)
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
            offering.Program.Name,
            offering.Level.Name,
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
        if (offering.LecturerId != userId)
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

        var authResult = await ValidateApprovalAuthorityAsync(offering, userId, ct);
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

        var authResult = await ValidateApprovalAuthorityAsync(offering, userId, ct);
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
    private async Task<ErrorOr<Success>> ValidateApprovalAuthorityAsync(CourseOffering offering, Guid userId, CancellationToken ct)
    {
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin" || r == "HOD" || r == "Dean");
        var isLecturer = offering.LecturerId == userId;

        if (!isAdmin && !isLecturer)
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
            return Error.NotFound("Publication.NotFound", "Grades not yet published");

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
        var isLecturer = offering.LecturerId == userId;

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
            .Include(x => x.Program)
            .Include(x => x.Level)
            .Include(x => x.AcademicSession)
            .Include(x => x.Lecturer)
            .AsQueryable();

        if (!isAdmin)
            query = query.Where(x => x.LecturerId == userId);

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
                offering.Program.Name,
                offering.Level.Name,
                offering.AcademicSession.Name,
                (int)offering.Semester,
                isPublished,
                offering.Lecturer?.DisplayName,
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

            if (sysConfig.Value.DefaultGradingStyle == GradingStyle.Weighted)
            {
                totalScore += weightedScore;
            }
        }

        if (sysConfig.Value.DefaultGradingStyle == GradingStyle.Unweighted && assessmentGrades.Any())
        {
            totalScore = assessmentGrades.Average(x => x.MarksObtained / x.MaxMarks * 100);
        }

        return new StudentGradeViewDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            assessmentGrades,
            Math.Round(totalScore, 2),
            CalculateLetterGrade(totalScore),
            null,
            true);
    }

    public async Task<ErrorOr<List<StudentGradeViewDto>>> GetStudentAllGradesAsync(Guid studentId, CancellationToken ct = default)
    {
        var publications = await _dbContext.GradePublications
            .Where(x => x.IsVisibleToStudents)
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
        return new SystemGradingConfigurationDto(
            config.Id,
            config.DefaultGradingStyle,
            config.DefaultExamPercentage,
            config.ApprovalWorkflowEnabled,
            config.DefaultCA1Weight,
            config.DefaultCA2Weight,
            config.DefaultCA3Weight,
            config.DefaultExamWeight,
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
        return scores.Any() ? scores.Average() : 0;
    }

    private string CalculateLetterGrade(decimal percentage)
    {
        return percentage switch
        {
            >= 70 => "A",
            >= 60 => "B",
            >= 50 => "C",
            >= 45 => "D",
            >= 40 => "E",
            _ => "F"
        };
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
}
