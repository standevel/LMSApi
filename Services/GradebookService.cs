using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using LMS.Api.Security;

namespace LMS.Api.Services;

public sealed class GradebookService : IGradebookService
{
    private readonly LmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly INotificationService _notificationService;
    private readonly IGradeCalculationEngine _gradeCalculationEngine;
    private readonly IPermissionService _permissionService;

    public GradebookService(
        LmsDbContext dbContext,
        IAuditService auditService,
        INotificationService notificationService,
        IGradeCalculationEngine gradeCalculationEngine,
        IPermissionService permissionService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _notificationService = notificationService;
        _gradeCalculationEngine = gradeCalculationEngine;
        _permissionService = permissionService;
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

        if (request.ApplyToExistingCourses == true)
        {
            var allCategories = await _dbContext.AssessmentCategories.ToListAsync(ct);
            foreach (var cat in allCategories)
            {
                switch (cat.CategoryType)
                {
                    case AssessmentCategoryType.CA1:
                        cat.Weight = config.DefaultCA1Weight;
                        break;
                    case AssessmentCategoryType.CA2:
                        cat.Weight = config.DefaultCA2Weight;
                        break;
                    case AssessmentCategoryType.CA3:
                        cat.Weight = config.DefaultCA3Weight;
                        break;
                    case AssessmentCategoryType.Exam:
                        cat.Weight = config.DefaultExamWeight;
                        break;
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("UpdateSystemConfiguration", "SystemGradingConfiguration", config.Id.ToString(), $"Updated grading configuration (ApplyToExisting: {request.ApplyToExistingCourses})", ct);

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

    public async Task<ErrorOr<List<AssessmentCategoryDto>>> UpdateCourseAssessmentCategoriesAsync(
        Guid courseOfferingId,
        UpdateCourseAssessmentCategoriesRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(courseOfferingId);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        if (request.Categories == null || !request.Categories.Any())
            return Error.Validation("Categories.Required", "At least one category is required");

        var totalWeight = request.Categories.Sum(c => c.Weight);
        if (totalWeight != 100m)
            return Error.Validation("Weight.SumInvalid", $"Category weights must sum to 100%. Current total: {totalWeight}%");

        var existingCategories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingAssessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingQuizzes = await _dbContext.Quizzes
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingAssignments = await _dbContext.Assignments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        // Update or create categories
        var updatedCategories = new List<AssessmentCategory>();
        int order = 0;
        foreach (var item in request.Categories)
        {
            var matching = existingCategories.Where(c => c.CategoryType == item.CategoryType || c.CategoryName.Equals(item.CategoryName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matching.Any())
            {
                var existing = matching.First();
                existing.CategoryType = item.CategoryType;
                existing.CategoryName = item.CategoryName;
                existing.Weight = item.Weight;
                existing.MaxMarks = item.MaxMarks;
                existing.IsExamCategory = item.IsExamCategory || item.CategoryType == AssessmentCategoryType.Exam;
                existing.DisplayOrder = item.DisplayOrder > 0 ? item.DisplayOrder : order++;
                updatedCategories.Add(existing);

                // Reassign assessments, quizzes, and assignments on any duplicate matching categories to primary and delete duplicate
                for (int i = 1; i < matching.Count; i++)
                {
                    var dup = matching[i];
                    var orphanAssessments = existingAssessments.Where(a => a.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var ass in orphanAssessments)
                    {
                        ass.AssessmentCategoryId = existing.Id;
                    }

                    var orphanQuizzes = existingQuizzes.Where(q => q.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var quiz in orphanQuizzes)
                    {
                        quiz.AssessmentCategoryId = existing.Id;
                    }

                    var orphanAssignments = existingAssignments.Where(a => a.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var assignment in orphanAssignments)
                    {
                        assignment.AssessmentCategoryId = existing.Id;
                    }

                    _dbContext.AssessmentCategories.Remove(dup);
                    existingCategories.Remove(dup);
                }
            }
            else
            {
                var newCat = new AssessmentCategory
                {
                    CourseOfferingId = courseOfferingId,
                    CategoryType = item.CategoryType,
                    CategoryName = item.CategoryName,
                    Weight = item.Weight,
                    MaxMarks = item.MaxMarks,
                    IsExamCategory = item.IsExamCategory || item.CategoryType == AssessmentCategoryType.Exam,
                    DisplayOrder = item.DisplayOrder > 0 ? item.DisplayOrder : order++
                };
                _dbContext.AssessmentCategories.Add(newCat);
                updatedCategories.Add(newCat);
            }
        }

        // Remove any obsolete categories that were deleted
        var itemsToKeepTypes = request.Categories.Select(c => c.CategoryType).ToHashSet();
        var itemsToKeepNames = request.Categories.Select(c => c.CategoryName.ToLower()).ToHashSet();
        var toRemove = existingCategories.Where(c => !itemsToKeepTypes.Contains(c.CategoryType) && !itemsToKeepNames.Contains(c.CategoryName.ToLower())).ToList();
        if (toRemove.Any())
        {
            foreach (var rem in toRemove)
            {
                var hasAssessments = existingAssessments.Any(a => a.AssessmentCategoryId == rem.Id);
                var hasQuizzes = existingQuizzes.Any(q => q.AssessmentCategoryId == rem.Id);
                var hasAssignments = existingAssignments.Any(a => a.AssessmentCategoryId == rem.Id);
                if (!hasAssessments && !hasQuizzes && !hasAssignments)
                {
                    _dbContext.AssessmentCategories.Remove(rem);
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("UpdateCourseCategories", "CourseOffering", courseOfferingId.ToString(), $"Updated assessment categories ({string.Join(", ", request.Categories.Select(c => $"{c.CategoryName}: {c.Weight}%"))})", ct);

        return updatedCategories.OrderBy(c => c.DisplayOrder).Select(MapToCategoryDto).ToList();
    }

    public async Task<ErrorOr<List<AssessmentCategoryDto>>> ResetCourseAssessmentCategoriesToDefaultAsync(
        Guid courseOfferingId,
        Guid userId,
        CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings.FindAsync(new object?[] { courseOfferingId }, cancellationToken: ct);
        if (offering == null)
            return Error.NotFound("Course.NotFound", "Course offering not found");

        var config = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

        var existingCategories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingAssessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingQuizzes = await _dbContext.Quizzes
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var existingAssignments = await _dbContext.Assignments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var defaultDefinitions = new[]
        {
            (Type: AssessmentCategoryType.CA1, Name: "CA1", Weight: config.DefaultCA1Weight, IsExam: false, Order: 0),
            (Type: AssessmentCategoryType.CA2, Name: "CA2", Weight: config.DefaultCA2Weight, IsExam: false, Order: 1),
            (Type: AssessmentCategoryType.CA3, Name: "CA3", Weight: config.DefaultCA3Weight, IsExam: false, Order: 2),
            (Type: AssessmentCategoryType.Exam, Name: "Exam", Weight: config.DefaultExamWeight, IsExam: true, Order: 3)
        };

        var resultCategories = new List<AssessmentCategory>();

        foreach (var def in defaultDefinitions)
        {
            var matching = existingCategories.Where(c => c.CategoryType == def.Type).ToList();
            AssessmentCategory primary;

            if (!matching.Any())
            {
                primary = new AssessmentCategory
                {
                    CourseOfferingId = courseOfferingId,
                    CategoryType = def.Type,
                    CategoryName = def.Name,
                    Weight = def.Weight,
                    MaxMarks = def.Weight,
                    IsExamCategory = def.IsExam,
                    DisplayOrder = def.Order
                };
                _dbContext.AssessmentCategories.Add(primary);
            }
            else
            {
                primary = matching.First();
                primary.CategoryName = def.Name;
                primary.Weight = def.Weight;
                primary.MaxMarks = def.Weight;
                primary.IsExamCategory = def.IsExam;
                primary.DisplayOrder = def.Order;

                // If duplicate categories exist for this type, reassign any assessments, quizzes, and assignments to primary and remove the duplicates
                for (int i = 1; i < matching.Count; i++)
                {
                    var dup = matching[i];
                    var orphanAssessments = existingAssessments.Where(a => a.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var ass in orphanAssessments)
                    {
                        ass.AssessmentCategoryId = primary.Id;
                    }

                    var orphanQuizzes = existingQuizzes.Where(q => q.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var quiz in orphanQuizzes)
                    {
                        quiz.AssessmentCategoryId = primary.Id;
                    }

                    var orphanAssignments = existingAssignments.Where(a => a.AssessmentCategoryId == dup.Id).ToList();
                    foreach (var assignment in orphanAssignments)
                    {
                        assignment.AssessmentCategoryId = primary.Id;
                    }

                    _dbContext.AssessmentCategories.Remove(dup);
                }
            }

            resultCategories.Add(primary);
        }

        // Remove any custom categories that have no assessments, quizzes, or assignments
        var standardTypes = defaultDefinitions.Select(d => d.Type).ToHashSet();
        var extraCategories = existingCategories.Where(c => !standardTypes.Contains(c.CategoryType)).ToList();
        foreach (var extra in extraCategories)
        {
            var hasAssessments = existingAssessments.Any(a => a.AssessmentCategoryId == extra.Id);
            var hasQuizzes = existingQuizzes.Any(q => q.AssessmentCategoryId == extra.Id);
            var hasAssignments = existingAssignments.Any(a => a.AssessmentCategoryId == extra.Id);

            if (!hasAssessments && !hasQuizzes && !hasAssignments)
            {
                _dbContext.AssessmentCategories.Remove(extra);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditService.LogAsync("ResetCourseCategories", "CourseOffering", courseOfferingId.ToString(), "Reset assessment categories to system default", ct);

        return resultCategories.OrderBy(c => c.DisplayOrder).Select(MapToCategoryDto).ToList();
    }

    public async Task<ErrorOr<Deleted>> DeleteAssessmentCategoryAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _dbContext.AssessmentCategories.FindAsync(new object?[] { categoryId }, cancellationToken: ct);
        if (category == null)
            return Error.NotFound("Category.NotFound", "Assessment category not found");

        var hasAssessments = await _dbContext.Assessments.AnyAsync(a => a.AssessmentCategoryId == categoryId, ct);
        var hasQuizzes = await _dbContext.Quizzes.AnyAsync(q => q.AssessmentCategoryId == categoryId, ct);
        var hasAssignments = await _dbContext.Assignments.AnyAsync(a => a.AssessmentCategoryId == categoryId, ct);

        if (hasAssessments || hasQuizzes || hasAssignments)
        {
            return Error.Conflict("Category.HasLinkedItems", "Cannot delete assessment category because it has linked assessments, quizzes, or assignments.");
        }

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

        // Check if publication is visible and published results exist
        var publication = await _dbContext.GradePublications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication?.IsVisibleToStudents == true)
        {
            var publishedResults = await _dbContext.StudentCourseResults
                .AsNoTracking()
                .Where(r => r.CourseOfferingId == courseOfferingId && r.IsPublished)
                .ToListAsync(ct);

            if (publishedResults.Any())
            {
                var summaryList = new List<StudentGradeSummaryDto>();
                foreach (var student in students)
                {
                    var pub = publishedResults.FirstOrDefault(r => r.StudentId == student.StudentId);
                    if (pub != null)
                    {
                        summaryList.Add(new StudentGradeSummaryDto(
                            student.StudentId,
                            student.MatricNumber,
                            student.StudentName,
                            student.StudentEmail,
                            pub.Ca1Score ?? 0m,
                            pub.Ca2Score ?? 0m,
                            pub.Ca3Score ?? 0m,
                            pub.ExamScore ?? 0m,
                            pub.TotalScore,
                            pub.LetterGrade,
                            null));
                    }
                }

                if (summaryList.Count > 0)
                {
                    return summaryList.OrderByDescending(x => x.TotalScore).ToList();
                }
            }
        }

        // Get system configuration entity
        var sysConfigEntity = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

        // Get all assessment categories with assessments and grades
        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.Grades)
            .ToListAsync(ct);

        var allGrades = assessments.SelectMany(a => a.Grades).ToList();

        var calculatedGrades = _gradeCalculationEngine.CalculateCourseGrades(
            studentIds,
            assessments,
            categories,
            allGrades,
            sysConfigEntity);

        var result = new List<StudentGradeSummaryDto>();
        foreach (var student in students)
        {
            if (calculatedGrades.TryGetValue(student.StudentId, out var calc))
            {
                result.Add(new StudentGradeSummaryDto(
                    student.StudentId,
                    student.MatricNumber,
                    student.StudentName,
                    student.StudentEmail,
                    calc.Ca1Score ?? 0m,
                    calc.Ca2Score ?? 0m,
                    calc.Ca3Score ?? 0m,
                    calc.ExamScore ?? 0m,
                    calc.TotalScore,
                    calc.LetterGrade,
                    null));
            }
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

        if (!categories.Any())
        {
            var config = await _dbContext.SystemGradingConfigurations
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

            categories = new List<AssessmentCategory>
            {
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA1, CategoryName = "CA1", Weight = config.DefaultCA1Weight, MaxMarks = config.DefaultCA1Weight, DisplayOrder = 0 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA2, CategoryName = "CA2", Weight = config.DefaultCA2Weight, MaxMarks = config.DefaultCA2Weight, DisplayOrder = 1 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA3, CategoryName = "CA3", Weight = config.DefaultCA3Weight, MaxMarks = config.DefaultCA3Weight, DisplayOrder = 2 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.Exam, CategoryName = "Exam", Weight = config.DefaultExamWeight, MaxMarks = config.DefaultExamWeight, IsExamCategory = true, DisplayOrder = 3 }
            };
            _dbContext.AssessmentCategories.AddRange(categories);
            await _dbContext.SaveChangesAsync(ct);
        }

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        // Ensure default assessment exists for each category
        foreach (var category in categories)
        {
            if (!assessments.Any(a => a.AssessmentCategoryId == category.Id))
            {
                var newAssessment = new Assessment
                {
                    CourseOfferingId = courseOfferingId,
                    AssessmentCategoryId = category.Id,
                    Title = $"{category.CategoryName} Assessment",
                    MaxMarks = category.MaxMarks
                };
                _dbContext.Assessments.Add(newAssessment);
                await _dbContext.SaveChangesAsync(ct);
                assessments.Add(newAssessment);
            }
        }

        var assessmentIds = assessments.Select(a => a.Id).ToList();
        var existingGrades = await _dbContext.Grades
            .Where(g => assessmentIds.Contains(g.AssessmentId))
            .ToListAsync(ct);

        var distinctCategories = categories
            .GroupBy(c => c.CategoryType)
            .Select(g => g.OrderByDescending(c => assessments.Any(a => a.AssessmentCategoryId == c.Id))
                          .ThenBy(c => c.DisplayOrder)
                          .First())
            .ToList();

        int successCount = 0;

        foreach (var studentGrade in request.Grades)
        {
            UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca1Score, AssessmentCategoryType.CA1);
            UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca2Score, AssessmentCategoryType.CA2);
            UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.Ca3Score, AssessmentCategoryType.CA3);
            UpdateOrAddGradeForCategory(studentGrade.StudentId, studentGrade.ExamScore, AssessmentCategoryType.Exam);
        }

        await _dbContext.SaveChangesAsync(ct);
        return successCount;

        void UpdateOrAddGradeForCategory(Guid studentId, decimal? score, AssessmentCategoryType categoryType)
        {
            var category = distinctCategories.FirstOrDefault(c => c.CategoryType == categoryType);
            if (category == null) return;

            var assessment = assessments
                .Where(a => a.AssessmentCategoryId == category.Id)
                .OrderByDescending(a => existingGrades.Any(g => g.AssessmentId == a.Id))
                .FirstOrDefault();
            if (assessment == null) return;

            var grade = existingGrades.FirstOrDefault(g => g.AssessmentId == assessment.Id && g.StudentId == studentId);

            if (!score.HasValue)
            {
                if (grade != null)
                {
                    _dbContext.Grades.Remove(grade);
                    existingGrades.Remove(grade);
                    successCount++;
                }
                return;
            }

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
                existingGrades.Add(grade);
                successCount++;
            }
            else
            {
                grade.MarksObtained = score.Value;
                grade.IsLocked = false;
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

    public async Task<ErrorOr<GradebookExcelTemplateDto>> GenerateExcelTemplateAsync(Guid courseOfferingId, Guid? collegeId = null, CancellationToken ct = default)
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
                .ThenInclude(u => u.Faculty)
            .OrderBy(e => e.Student.DisplayName)
            .ToListAsync(ct);

        // Pre-fetch student records to match Matric Numbers and Colleges
        var studentEmails = students
            .Select(e => e.Student.Email?.ToLower().Trim())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();

        var studentRecords = await _dbContext.Students
            .Include(s => s.Faculty)
            .Include(s => s.AcademicProgram)
                .ThenInclude(p => p.Department)
                    .ThenInclude(d => d.Faculty)
            .Where(s => studentEmails.Contains(s.OfficialEmail.ToLower().Trim()) || studentEmails.Contains(s.PersonalEmail.ToLower().Trim()))
            .ToListAsync(ct);

        // Build rows with resolved matric + college, optionally filter by college,
        // and sort by matric number (nulls last) then display name.
        var templateRows = students
            .Select(student =>
            {
                var studentUser = student.Student;
                var matchedStudent = studentRecords.FirstOrDefault(s =>
                    string.Equals(s.OfficialEmail, studentUser.Email, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.PersonalEmail, studentUser.Email, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(s.EntraObjectId) && string.Equals(s.EntraObjectId, studentUser.EntraObjectId, StringComparison.OrdinalIgnoreCase)));

                var collegeName = matchedStudent?.Faculty?.Name
                    ?? matchedStudent?.AcademicProgram?.Department?.Faculty?.Name
                    ?? studentUser.Faculty?.Name
                    ?? "";

                var facultyId = matchedStudent?.FacultyId
                    ?? matchedStudent?.AcademicProgram?.Department?.FacultyId;

                return new { studentUser, matchedStudent, collegeName, facultyId };
            })
            .Where(r => !collegeId.HasValue || r.facultyId == collegeId.Value)
            .OrderBy(r => r.matchedStudent?.StudentNumber == null ? 1 : 0)
            .ThenBy(r => r.matchedStudent?.StudentNumber ?? string.Empty)
            .ThenBy(r => r.studentUser.DisplayName ?? string.Empty)
            .ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Gradebook");

        // Title
        worksheet.Cell(1, 1).Value = $"Gradebook: {offering.Course.Code} - {offering.Course.Title}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 5 + categories.Count).Merge();

        // Resolve grading style so column headers show "%" (weighted) or "/max" (simple sum)
        var isWeightedStyle = true;
        var templateSysConfig = await GetSystemConfigurationAsync(ct);
        if (!templateSysConfig.IsError)
            isWeightedStyle = templateSysConfig.Value.DefaultGradingStyle == nameof(GradingStyle.Weighted);

        // Headers
        worksheet.Cell(3, 1).Value = "Matric Number";
        worksheet.Cell(3, 2).Value = "Student Name";
        worksheet.Cell(3, 3).Value = "College";

        int col = 4;
        foreach (var category in categories)
        {
            var weightLabel = isWeightedStyle
                ? $"{category.Weight:0.##}%"
                : $"{category.Weight:0.##}";
            worksheet.Cell(3, col).Value = $"{category.CategoryName} ({weightLabel})";
            col++;
        }

        worksheet.Cell(3, col).Value = "Total";
        worksheet.Cell(3, col + 1).Value = "Remarks";

        // Style headers
        var headerRange = worksheet.Range(3, 1, 3, col + 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 75, 68);
        headerRange.Style.Font.FontColor = XLColor.White;

        // Student data (already filtered by college and sorted by matric number)
        int row = 4;
        foreach (var r in templateRows)
        {
            var studentUser = r.studentUser;
            var matchedStudent = r.matchedStudent;

            worksheet.Cell(row, 1).Value = matchedStudent?.StudentNumber ?? "";
            worksheet.Cell(row, 2).Value = studentUser.DisplayName ?? "Unknown";
            worksheet.Cell(row, 3).Value = r.collegeName;

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

        instructionsSheet.Cell(3, 1).Value = "1. Enter marks for each assessment category (0-100 or above for bonus marks)";
        instructionsSheet.Cell(4, 1).Value = "2. You can sort and filter by College, Matric Number, or Student Name";
        instructionsSheet.Cell(5, 1).Value = "3. The Total column will be calculated automatically upon upload";
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

        var levelOrder = offeringProgram?.Level?.Order ?? 1;
        var levelNameStr = offeringProgram?.Level?.Name ?? "";
        bool isYear1 = levelOrder <= 1 || levelNameStr.Contains("100") || levelNameStr.ToLower().Contains("year 1");
        bool includeCgpa = !isYear1 || (isYear1 && offering.Semester == Data.Enums.Semester.Second);

        // Load all historical registered course enrollments for cohort students up to current session & semester
        var historicalEnrollments = await _dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId) && e.Status == "Registered")
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Include(e => e.CourseOffering)
                .ThenInclude(co => co.Course)
            .Where(e => e.CourseOffering.AcademicSession.StartDate < offering.AcademicSession.StartDate ||
                       (e.CourseOffering.AcademicSessionId == offering.AcademicSessionId && (int)e.CourseOffering.Semester <= (int)offering.Semester))
            .ToListAsync(ct);

        var histOfferingIds = historicalEnrollments.Select(e => e.CourseOfferingId).Distinct().ToList();
        foreach (var offId in histOfferingIds)
        {
            if (!allSummaries.ContainsKey(offId))
            {
                var sumRes = await GetStudentGradeSummariesAsync(offId, ct);
                if (!sumRes.IsError)
                {
                    allSummaries[offId] = sumRes.Value;
                }
            }
        }

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

        // Set header label in row 5 for GPA / CGPA column
        ws.Cell(5, gpaCol).Value = includeCgpa ? "GPA / CGPA" : "GPA";

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

                    // Row 1: Score (rounded up to whole number)
                    ws.Cell(r1, col).Value = Math.Ceiling(score.Value);
                    ws.Cell(r1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Row 2: Grade formula
                    var scoreCellRef = ws.Cell(r1, col).Address.ToString();
                    ws.Cell(r2, col).FormulaA1 = $"=IFS({scoreCellRef}>=70,\"A\",{scoreCellRef}>=60,\"B\",{scoreCellRef}>=50,\"C\",{scoreCellRef}>=45,\"D\",{scoreCellRef}>=40,\"E\",{scoreCellRef}<40,\"F\")";
                    ws.Cell(r2, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            // Cumulative summary calculation across all historical semesters up to current
            double cumRegisteredUnits = 0;
            double cumPassedUnits = 0;
            double cumFailedUnits = 0;
            double cumGradePoints = 0;
            var cumOutstandingList = new List<string>();

            if (includeCgpa)
            {
                var studHistEnrollments = historicalEnrollments.Where(e => e.StudentId == student.Id).ToList();
                foreach (var e in studHistEnrollments)
                {
                    var peerOffering = e.CourseOffering;
                    if (allSummaries.TryGetValue(peerOffering.Id, out var summaries))
                    {
                        var studSum = summaries.FirstOrDefault(s => s.StudentId == student.Id);
                        if (studSum != null)
                        {
                            double scoreVal = (double)studSum.TotalScore;
                            var gp = getGradeAndPoints(scoreVal);
                            double creditUnits = peerOffering.Course.CreditUnits;

                            cumRegisteredUnits += creditUnits;
                            if (gp.Grade != "F")
                            {
                                cumPassedUnits += creditUnits;
                            }
                            else
                            {
                                cumFailedUnits += creditUnits;
                            }
                            cumGradePoints += gp.Points * creditUnits;
                        }
                    }
                }

                var studentCourseGroups = studHistEnrollments.GroupBy(e => e.CourseOffering.Course.Code);
                foreach (var group in studentCourseGroups)
                {
                    var latestEnrollment = group
                        .OrderByDescending(e => e.CourseOffering.AcademicSession.StartDate)
                        .ThenByDescending(e => (int)e.CourseOffering.Semester)
                        .First();

                    if (allSummaries.TryGetValue(latestEnrollment.CourseOfferingId, out var summaries))
                    {
                        var studSum = summaries.FirstOrDefault(s => s.StudentId == student.Id);
                        if (studSum != null)
                        {
                            var gp = getGradeAndPoints((double)studSum.TotalScore);
                            if (gp.Grade == "F")
                            {
                                cumOutstandingList.Add($"{latestEnrollment.CourseOffering.Course.Code} ({latestEnrollment.CourseOffering.Course.CreditUnits})");
                            }
                        }
                    }
                }
            }
            else
            {
                cumRegisteredUnits = totalRegisteredUnits;
                cumPassedUnits = totalPassedUnits;
                cumFailedUnits = totalFailedUnits;
                cumGradePoints = totalGradePoints;
                cumOutstandingList = outstandingList;
            }

            // Current Semester Summary Metrics (Row 1)
            ws.Cell(r1, regUnitsCol).Value = totalRegisteredUnits;
            ws.Cell(r1, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r1, passedUnitsCol).Value = totalPassedUnits;
            ws.Cell(r1, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r1, failedUnitsCol).Value = totalFailedUnits;
            ws.Cell(r1, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r1, totalGpCol).Value = totalGradePoints;
            ws.Cell(r1, totalGpCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var r1RegCell = ws.Cell(r1, regUnitsCol).Address.ToString();
            var r1GpCell = ws.Cell(r1, totalGpCol).Address.ToString();
            ws.Cell(r1, gpaCol).FormulaA1 = $"=IF({r1RegCell}>0, ROUND({r1GpCell}/{r1RegCell}, 2), 0)";
            ws.Cell(r1, gpaCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Cumulative / CGPA Summary Metrics (Row 2)
            ws.Cell(r2, regUnitsCol).Value = cumRegisteredUnits;
            ws.Cell(r2, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r2, passedUnitsCol).Value = cumPassedUnits;
            ws.Cell(r2, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r2, failedUnitsCol).Value = cumFailedUnits;
            ws.Cell(r2, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r2, totalGpCol).Value = cumGradePoints;
            ws.Cell(r2, totalGpCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var r2RegCell = ws.Cell(r2, regUnitsCol).Address.ToString();
            var r2GpCell = ws.Cell(r2, totalGpCol).Address.ToString();
            ws.Cell(r2, gpaCol).FormulaA1 = $"=IF({r2RegCell}>0, ROUND({r2GpCell}/{r2RegCell}, 2), 0)";
            ws.Cell(r2, gpaCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Remarks (Merged r1:r2)
            string remarksVal = cumOutstandingList.Count > 0 ? string.Join(", ", cumOutstandingList) : "PASS";
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

            bool isYear1 = level.Order <= 1 || level.Name.Contains("100") || level.Name.ToLower().Contains("year 1");
            bool includeCgpa = !isYear1 || (isYear1 && semester == Data.Enums.Semester.Second);

            // Load all historical registered course enrollments for cohort students up to current session & semester
            var historicalEnrollments = await _dbContext.CourseEnrollments
                .AsNoTracking()
                .Where(e => studentIds.Contains(e.StudentId) && e.Status == "Registered")
                .Include(e => e.CourseOffering)
                    .ThenInclude(co => co.AcademicSession)
                .Include(e => e.CourseOffering)
                    .ThenInclude(co => co.Course)
                .Where(e => e.CourseOffering.AcademicSession.StartDate < session.StartDate ||
                           (e.CourseOffering.AcademicSessionId == session.Id && (int)e.CourseOffering.Semester <= (int)semester))
                .ToListAsync(ct);

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

            var histOfferingIds = historicalEnrollments.Select(e => e.CourseOfferingId).Distinct().ToList();
            foreach (var offId in histOfferingIds)
            {
                if (!allSummaries.ContainsKey(offId))
                {
                    var sumRes = await GetStudentGradeSummariesAsync(offId, ct);
                    if (!sumRes.IsError)
                    {
                        allSummaries[offId] = sumRes.Value;
                    }
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

            // Set header label in row 5 for GPA / CGPA column
            ws.Cell(5, gpaCol).Value = includeCgpa ? "GPA / CGPA" : "GPA";

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

                        // Row 1: Score (rounded up to whole number)
                        ws.Cell(r1, col).Value = Math.Ceiling(score.Value);
                        ws.Cell(r1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // Row 2: Grade formula
                        var scoreCellRef = ws.Cell(r1, col).Address.ToString();
                        ws.Cell(r2, col).FormulaA1 = $"=IFS({scoreCellRef}>=70,\"A\",{scoreCellRef}>=60,\"B\",{scoreCellRef}>=50,\"C\",{scoreCellRef}>=45,\"D\",{scoreCellRef}>=40,\"E\",{scoreCellRef}<40,\"F\")";
                        ws.Cell(r2, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                }

                // Cumulative summary calculation across all historical semesters up to current
                double cumRegisteredUnits = 0;
                double cumPassedUnits = 0;
                double cumFailedUnits = 0;
                double cumGradePoints = 0;
                var cumOutstandingList = new List<string>();

                if (includeCgpa)
                {
                    var studHistEnrollments = historicalEnrollments.Where(e => e.StudentId == student.Id).ToList();
                    foreach (var e in studHistEnrollments)
                    {
                        var peerOffering = e.CourseOffering;
                        if (allSummaries.TryGetValue(peerOffering.Id, out var summaries))
                        {
                            var studSum = summaries.FirstOrDefault(s => s.StudentId == student.Id);
                            if (studSum != null)
                            {
                                double scoreVal = (double)studSum.TotalScore;
                                var gp = getGradeAndPoints(scoreVal);
                                double creditUnits = peerOffering.Course.CreditUnits;

                                cumRegisteredUnits += creditUnits;
                                if (gp.Grade != "F")
                                {
                                    cumPassedUnits += creditUnits;
                                }
                                else
                                {
                                    cumFailedUnits += creditUnits;
                                }
                                cumGradePoints += gp.Points * creditUnits;
                            }
                        }
                    }

                    var studentCourseGroups = studHistEnrollments.GroupBy(e => e.CourseOffering.Course.Code);
                    foreach (var group in studentCourseGroups)
                    {
                        var latestEnrollment = group
                            .OrderByDescending(e => e.CourseOffering.AcademicSession.StartDate)
                            .ThenByDescending(e => (int)e.CourseOffering.Semester)
                            .First();

                        if (allSummaries.TryGetValue(latestEnrollment.CourseOfferingId, out var summaries))
                        {
                            var studSum = summaries.FirstOrDefault(s => s.StudentId == student.Id);
                            if (studSum != null)
                            {
                                var gp = getGradeAndPoints((double)studSum.TotalScore);
                                if (gp.Grade == "F")
                                {
                                    cumOutstandingList.Add($"{latestEnrollment.CourseOffering.Course.Code} ({latestEnrollment.CourseOffering.Course.CreditUnits})");
                                }
                            }
                        }
                    }
                }
                else
                {
                    cumRegisteredUnits = totalRegisteredUnits;
                    cumPassedUnits = totalPassedUnits;
                    cumFailedUnits = totalFailedUnits;
                    cumGradePoints = totalGradePoints;
                    cumOutstandingList = outstandingList;
                }

                // Current Semester Summary Metrics (Row 1)
                ws.Cell(r1, regUnitsCol).Value = totalRegisteredUnits;
                ws.Cell(r1, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r1, passedUnitsCol).Value = totalPassedUnits;
                ws.Cell(r1, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r1, failedUnitsCol).Value = totalFailedUnits;
                ws.Cell(r1, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r1, totalGpCol).Value = totalGradePoints;
                ws.Cell(r1, totalGpCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var r1RegCell = ws.Cell(r1, regUnitsCol).Address.ToString();
                var r1GpCell = ws.Cell(r1, totalGpCol).Address.ToString();
                ws.Cell(r1, gpaCol).FormulaA1 = $"=IF({r1RegCell}>0, ROUND({r1GpCell}/{r1RegCell}, 2), 0)";
                ws.Cell(r1, gpaCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Cumulative / CGPA Summary Metrics (Row 2)
                ws.Cell(r2, regUnitsCol).Value = cumRegisteredUnits;
                ws.Cell(r2, regUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r2, passedUnitsCol).Value = cumPassedUnits;
                ws.Cell(r2, passedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r2, failedUnitsCol).Value = cumFailedUnits;
                ws.Cell(r2, failedUnitsCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(r2, totalGpCol).Value = cumGradePoints;
                ws.Cell(r2, totalGpCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var r2RegCell = ws.Cell(r2, regUnitsCol).Address.ToString();
                var r2GpCell = ws.Cell(r2, totalGpCol).Address.ToString();
                ws.Cell(r2, gpaCol).FormulaA1 = $"=IF({r2RegCell}>0, ROUND({r2GpCell}/{r2RegCell}, 2), 0)";
                ws.Cell(r2, gpaCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Remarks (Merged r1:r2)
                string remarksVal = cumOutstandingList.Count > 0 ? string.Join(", ", cumOutstandingList) : "PASS";
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

        var offering = await _dbContext.CourseOfferings
            .Include(co => co.Course)
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId, ct);
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

        if (!categories.Any())
        {
            // Auto-create standard assessment categories if none configured
            var defaultCategories = new List<AssessmentCategory>
            {
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA1, CategoryName = "CA1", Weight = 10m, MaxMarks = 10m, DisplayOrder = 0 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA2, CategoryName = "CA2", Weight = 10m, MaxMarks = 10m, DisplayOrder = 1 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.CA3, CategoryName = "CA3", Weight = 10m, MaxMarks = 10m, DisplayOrder = 2 },
                new() { CourseOfferingId = courseOfferingId, CategoryType = AssessmentCategoryType.Exam, CategoryName = "Exam", Weight = 70m, MaxMarks = 70m, IsExamCategory = true, DisplayOrder = 3 }
            };
            _dbContext.AssessmentCategories.AddRange(defaultCategories);
            await _dbContext.SaveChangesAsync(ct);
            categories = defaultCategories;
        }

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        // Preload enrolled students and student records for rapid multi-identifier lookup
        var enrollments = await _dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered")
            .Include(e => e.Student)
            .ToListAsync(ct);

        var enrolledUsersByEmail = enrollments
            .Where(e => !string.IsNullOrWhiteSpace(e.Student.Email))
            .ToDictionary(e => e.Student.Email!.Trim().ToLower(), e => e.Student);

        var enrolledUsersByName = enrollments
            .Where(e => !string.IsNullOrWhiteSpace(e.Student.DisplayName))
            .GroupBy(e => e.Student.DisplayName!.Trim().ToLower())
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Student);

        var studentEmails = enrolledUsersByEmail.Keys.ToList();
        var studentRecords = await _dbContext.Students
            .Where(s => studentEmails.Contains(s.OfficialEmail.ToLower().Trim()) || studentEmails.Contains(s.PersonalEmail.ToLower().Trim()))
            .ToListAsync(ct);

        var matricToUserMap = new Dictionary<string, AppUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var sr in studentRecords.Where(s => !string.IsNullOrWhiteSpace(s.StudentNumber)))
        {
            var user = enrollments.FirstOrDefault(e =>
                string.Equals(e.Student.Email, sr.OfficialEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Student.Email, sr.PersonalEmail, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(sr.EntraObjectId) && string.Equals(e.Student.EntraObjectId, sr.EntraObjectId, StringComparison.OrdinalIgnoreCase))
            )?.Student;

            if (user != null)
            {
                var raw = sr.StudentNumber!.Trim();
                matricToUserMap.TryAdd(raw, user);

                var norm = NormalizeMatricNumber(raw);
                if (!string.IsNullOrEmpty(norm))
                {
                    matricToUserMap.TryAdd(norm, user);
                }

                var parsed = ParseMatricNumber(raw);
                if (parsed.HasValue)
                {
                    var pKey = $"{parsed.Value.Program}_{parsed.Value.Year}_{parsed.Value.Sequence}";
                    matricToUserMap.TryAdd(pKey, user);
                }
            }
        }

        var errors = new List<string>();
        int totalStudentsProcessed = 0;
        int successfulStudents = 0;
        int failedStudents = 0;
        int totalGradeEntries = 0;

        try
        {
            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream, ct);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "Gradebook") ?? workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return Error.Validation("Worksheet.NotFound", "No worksheet found in the uploaded workbook");

            // 1. Locate header row dynamically (search first 10 rows)
            IXLRow? headerRow = null;
            int headerRowNum = 1;
            for (int r = 1; r <= Math.Min(10, worksheet.RowsUsed().Count()); r++)
            {
                var row = worksheet.Row(r);
                var rowTexts = row.CellsUsed().Select(c => c.GetValue<string>().Trim().ToLower()).ToList();
                if (rowTexts.Any(t => t.Contains("student id") || t.Contains("matric") || t.Contains("ca1") || t.Contains("ca 1") || t.Contains("exam") || t.Contains("identity number")))
                {
                    headerRow = row;
                    headerRowNum = r;
                    break;
                }
            }

            if (headerRow == null)
                headerRow = worksheet.Row(1);

            // 2. Identify column indexes from header row
            int studentIdCol = -1;
            int matricCol = -1;
            int emailCol = -1;
            int nameCol = -1;
            var categoryColMap = new Dictionary<int, AssessmentCategory>();

            var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 15;
            for (int col = 1; col <= lastCol; col++)
            {
                var headerText = headerRow.Cell(col).GetValue<string>().Trim();
                var headerLower = headerText.ToLower();

                if (headerLower.Contains("student id") || headerLower.Equals("id") || headerLower.Contains("guid") || headerLower.Contains("user id"))
                {
                    studentIdCol = col;
                }
                else if (headerLower.Contains("matric") || headerLower.Contains("identity number") || headerLower.Contains("reg no") || headerLower.Contains("student number"))
                {
                    matricCol = col;
                }
                else if (headerLower.Contains("email"))
                {
                    emailCol = col;
                }
                else if (headerLower.Contains("name"))
                {
                    nameCol = col;
                }
                else if (headerLower.Contains("college") || headerLower.Contains("faculty") || headerLower.Contains("school") || headerLower.Contains("department") || headerLower.Contains("dept") || headerLower.Contains("total") || headerLower.Contains("remark"))
                {
                    // Informational columns to skip
                    continue;
                }
                else
                {
                    // Check if matches any category
                    var matchedCategory = categories.FirstOrDefault(cat => 
                        headerLower.StartsWith(cat.CategoryName.ToLower()) ||
                        headerLower.Contains(cat.CategoryName.ToLower()) ||
                        (cat.CategoryType == AssessmentCategoryType.Exam && (headerLower.Contains("exam") || headerLower.Contains("examination"))) ||
                        (cat.CategoryType == AssessmentCategoryType.CA1 && (headerLower.Contains("ca1") || headerLower.Contains("ca 1"))) ||
                        (cat.CategoryType == AssessmentCategoryType.CA2 && (headerLower.Contains("ca2") || headerLower.Contains("ca 2"))) ||
                        (cat.CategoryType == AssessmentCategoryType.CA3 && (headerLower.Contains("ca3") || headerLower.Contains("ca 3"))));

                    if (matchedCategory != null && !categoryColMap.ContainsValue(matchedCategory))
                    {
                        categoryColMap[col] = matchedCategory;
                    }
                }
            }

            // Fallback: If no category columns mapped, map columns after identifiers sequentially
            if (categoryColMap.Count == 0)
            {
                int startCol = Math.Max(studentIdCol, Math.Max(matricCol, Math.Max(emailCol, nameCol))) + 1;
                if (startCol <= 0) startCol = 5;
                for (int i = 0; i < categories.Count; i++)
                {
                    categoryColMap[startCol + i] = categories[i];
                }
            }

            // 3. Process data rows starting immediately after header row
            var lastRowIndex = worksheet.LastRowUsed()?.RowNumber() ?? (headerRowNum + 1);
            var dataRows = worksheet.Rows(headerRowNum + 1, lastRowIndex);

            foreach (var row in dataRows)
            {
                if (row.IsEmpty() || !row.CellsUsed().Any())
                    continue;

                totalStudentsProcessed++;

                // Resolve student
                AppUser? matchedUser = null;

                // A. Check Student ID GUID
                if (studentIdCol > 0)
                {
                    var idStr = row.Cell(studentIdCol).GetValue<string>().Trim();
                    if (Guid.TryParse(idStr, out var sGuid))
                    {
                        matchedUser = enrollments.FirstOrDefault(e => e.Student.Id == sGuid)?.Student;
                    }
                }

                // B. Check Matric Number
                if (matchedUser == null && matricCol > 0)
                {
                    var matricStr = row.Cell(matricCol).GetValue<string>().Trim();
                    if (!string.IsNullOrEmpty(matricStr))
                    {
                        if (!matricToUserMap.TryGetValue(matricStr, out matchedUser))
                        {
                            var normMatric = NormalizeMatricNumber(matricStr);
                            if (!string.IsNullOrEmpty(normMatric) && !matricToUserMap.TryGetValue(normMatric, out matchedUser))
                            {
                                var parsed = ParseMatricNumber(matricStr);
                                if (parsed.HasValue)
                                {
                                    var pKey = $"{parsed.Value.Program}_{parsed.Value.Year}_{parsed.Value.Sequence}";
                                    matricToUserMap.TryGetValue(pKey, out matchedUser);
                                }
                            }
                        }
                    }
                }

                // C. Check Email
                if (matchedUser == null && emailCol > 0)
                {
                    var emailStr = row.Cell(emailCol).GetValue<string>().Trim().ToLower();
                    if (!string.IsNullOrEmpty(emailStr) && enrolledUsersByEmail.TryGetValue(emailStr, out var u))
                    {
                        matchedUser = u;
                    }
                }

                // D. Check Column 1 directly if not yet matched (e.g. if user put GUID, Matric, or Email in Col 1)
                if (matchedUser == null)
                {
                    var col1Str = row.Cell(1).GetValue<string>().Trim();
                    if (Guid.TryParse(col1Str, out var g1))
                    {
                        matchedUser = enrollments.FirstOrDefault(e => e.Student.Id == g1)?.Student;
                    }
                    else if (!string.IsNullOrEmpty(col1Str) && matricToUserMap.TryGetValue(col1Str, out var u1))
                    {
                        matchedUser = u1;
                    }
                    else if (col1Str.Contains("@") && enrolledUsersByEmail.TryGetValue(col1Str.ToLower(), out var uEmail))
                    {
                        matchedUser = uEmail;
                    }
                }

                // E. Check Name (if unique)
                if (matchedUser == null && nameCol > 0)
                {
                    var nameStr = row.Cell(nameCol).GetValue<string>().Trim().ToLower();
                    if (!string.IsNullOrEmpty(nameStr) && enrolledUsersByName.TryGetValue(nameStr, out var uName))
                    {
                        matchedUser = uName;
                    }
                }

                if (matchedUser == null)
                {
                    var identifier = studentIdCol > 0 ? row.Cell(studentIdCol).GetValue<string>().Trim() :
                                    (matricCol > 0 ? row.Cell(matricCol).GetValue<string>().Trim() :
                                    (nameCol > 0 ? row.Cell(nameCol).GetValue<string>().Trim() : $"Row {row.RowNumber()}"));

                    errors.Add($"Row {row.RowNumber()}: Could not resolve student identifier '{identifier}' to an enrolled student in this course.");
                    failedStudents++;
                    continue;
                }

                bool hasAnyMark = false;

                // Process marks for each mapped category column
                foreach (var kvp in categoryColMap)
                {
                    int cCol = kvp.Key;
                    var category = kvp.Value;

                    var cellVal = row.Cell(cCol).GetValue<string>().Trim();
                    if (!string.IsNullOrEmpty(cellVal) && decimal.TryParse(cellVal, out var marks))
                    {
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
                            .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == matchedUser.Id, ct);

                        if (grade == null)
                        {
                            grade = new Grade
                            {
                                AssessmentId = assessment.Id,
                                StudentId = matchedUser.Id,
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

                        hasAnyMark = true;
                        totalGradeEntries++;
                    }
                }

                successfulStudents++;
            }

            await _dbContext.SaveChangesAsync(ct);

            await _auditService.LogAsync("BulkUploadGrades", "Gradebook",
                courseOfferingId.ToString(), $"Bulk uploaded {totalGradeEntries} grades for {successfulStudents} students", ct);
        }
        catch (Exception ex)
        {
            errors.Add($"Error processing file: {ex.Message}");
        }

        return new GradeUploadResultDto(
            Guid.Empty,
            totalStudentsProcessed,
            successfulStudents,
            failedStudents,
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

    public async Task<ErrorOr<BulkApproveResultDto>> BulkApproveGradesAsync(
        BulkApproveGradesRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var query = _dbContext.CourseOfferings
            .Include(co => co.Course)
                .ThenInclude(c => c.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(co => co.Programs)
                .ThenInclude(cop => cop.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .AsQueryable();

        if (request.AcademicSessionId.HasValue)
            query = query.Where(co => co.AcademicSessionId == request.AcademicSessionId.Value);

        if (request.Semester.HasValue)
            query = query.Where(co => co.Semester == (LMS.Api.Data.Enums.Semester)request.Semester.Value);

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(co =>
                co.Course.Program.DepartmentId == request.DepartmentId.Value ||
                co.Programs.Any(cop => cop.Program.DepartmentId == request.DepartmentId.Value));
        }

        if (request.FacultyId.HasValue)
        {
            query = query.Where(co =>
                co.Course.Program.Department.FacultyId == request.FacultyId.Value ||
                co.Programs.Any(cop => cop.Program.Department.FacultyId == request.FacultyId.Value));
        }

        if (request.CourseOfferingIds != null && request.CourseOfferingIds.Any())
        {
            query = query.Where(co => request.CourseOfferingIds.Contains(co.Id));
        }

        var offerings = await query.ToListAsync(ct);
        var details = new List<BulkApproveDetailDto>();
        var approvedCount = 0;
        var skippedCount = 0;

        foreach (var offering in offerings)
        {
            var authResult = await ValidateApprovalAuthorityAsync(offering, userId, request.Level, ct);
            if (authResult.IsError)
            {
                details.Add(new BulkApproveDetailDto(
                    offering.Id,
                    offering.Course.Code,
                    offering.Course.Title,
                    false,
                    authResult.FirstError.Description));
                skippedCount++;
                continue;
            }

            var approval = await _dbContext.GradeApprovals
                .FirstOrDefaultAsync(x => x.CourseOfferingId == offering.Id && x.Level == request.Level, ct);

            if (approval == null)
            {
                approval = new GradeApproval
                {
                    CourseOfferingId = offering.Id,
                    Level = request.Level,
                    Status = ApprovalStatus.Pending,
                    IsRequired = true,
                    ApprovalOrder = request.Level == ApprovalLevel.Department ? 1 : (request.Level == ApprovalLevel.College ? 2 : 3)
                };
                _dbContext.GradeApprovals.Add(approval);
            }

            if (approval.Status == ApprovalStatus.Approved)
            {
                details.Add(new BulkApproveDetailDto(
                    offering.Id,
                    offering.Course.Code,
                    offering.Course.Title,
                    false,
                    "Already approved at this level"));
                skippedCount++;
                continue;
            }

            // Check if previous levels are approved
            var previousApprovals = await _dbContext.GradeApprovals
                .Where(x => x.CourseOfferingId == offering.Id && x.ApprovalOrder < approval.ApprovalOrder)
                .ToListAsync(ct);

            if (previousApprovals.Any(x => x.Status != ApprovalStatus.Approved))
            {
                details.Add(new BulkApproveDetailDto(
                    offering.Id,
                    offering.Course.Code,
                    offering.Course.Title,
                    false,
                    "Previous approval levels must be approved first"));
                skippedCount++;
                continue;
            }

            approval.Status = ApprovalStatus.Approved;
            approval.ApprovedById = userId;
            approval.ApprovedAt = DateTime.UtcNow;
            approval.Comments = request.Comments ?? $"Bulk approved at {request.Level} level";
            approval.UpdatedAt = DateTime.UtcNow;

            approvedCount++;
            details.Add(new BulkApproveDetailDto(
                offering.Id,
                offering.Course.Code,
                offering.Course.Title,
                true,
                $"Successfully approved at {request.Level} level"));
        }

        await _dbContext.SaveChangesAsync(ct);
        await _auditService.LogAsync("BulkApproveGrades", "GradeApproval",
            request.Level.ToString(), $"Bulk approved {approvedCount} courses at {request.Level} level", ct);

        return new BulkApproveResultDto(
            offerings.Count,
            approvedCount,
            skippedCount,
            details);
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

        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin");
        var hasPublishPermission = isAdmin || await _permissionService.HasPermissionAsync(userId, LmsPermissions.ResultsPublish, ct);

        if (!hasPublishPermission)
        {
            return Error.Forbidden("Results.PublishNotAllowed", "You do not have permission to publish course results. Please contact an administrator.");
        }

        var approvalWorkflowCompleted = false;

        var approvals = await _dbContext.GradeApprovals
            .Where(x => x.CourseOfferingId == courseOfferingId && x.IsRequired)
            .ToListAsync(ct);

        var requiredLevels = new[] { ApprovalLevel.Department, ApprovalLevel.College, ApprovalLevel.Senate };
        var approvedLevels = approvals.Where(x => x.Status == ApprovalStatus.Approved).Select(x => x.Level).ToHashSet();

        if (!isAdmin && requiredLevels.Any(l => !approvedLevels.Contains(l)))
        {
            return Error.Forbidden("Approval.Incomplete", "Only Administrators can auto-approve and publish results directly. Non-admin users require all approval levels (Department, College, and Senate) to be approved first.");
        }

        if (isAdmin)
        {
            // Auto-approve missing or pending levels for Admin
            foreach (var level in requiredLevels)
            {
                var app = approvals.FirstOrDefault(x => x.Level == level);
                if (app == null)
                {
                    app = new GradeApproval
                    {
                        CourseOfferingId = courseOfferingId,
                        Level = level,
                        Status = ApprovalStatus.Approved,
                        IsRequired = true,
                        ApprovalOrder = level == ApprovalLevel.Department ? 1 : (level == ApprovalLevel.College ? 2 : 3),
                        ApprovedById = userId,
                        ApprovedAt = DateTime.UtcNow,
                        Comments = "Auto-approved by Administrator"
                    };
                    _dbContext.GradeApprovals.Add(app);
                }
                else if (app.Status != ApprovalStatus.Approved)
                {
                    app.Status = ApprovalStatus.Approved;
                    app.ApprovedById = userId;
                    app.ApprovedAt = DateTime.UtcNow;
                    app.Comments = request.PublicationNotes ?? "Auto-approved by Administrator";
                }
            }
            approvalWorkflowCompleted = true;
        }
        else
        {
            approvalWorkflowCompleted = true;
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
        await MaterializeCourseResultsAsync(courseOfferingId, userId, ct);

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
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin");
        var hasPublishPermission = isAdmin || await _permissionService.HasPermissionAsync(userId, LmsPermissions.ResultsPublish, ct);

        if (!hasPublishPermission)
        {
            return Error.Forbidden("Results.UnpublishNotAllowed", "You do not have permission to unpublish course results. Please contact an administrator.");
        }

        var publication = await _dbContext.GradePublications
            .FirstOrDefaultAsync(x => x.CourseOfferingId == courseOfferingId, ct);

        if (publication == null)
            return Error.NotFound("Publication.NotFound", "Publication not found");

        publication.IsVisibleToStudents = false;

        var existingResults = await _dbContext.StudentCourseResults
            .Where(r => r.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);
        foreach (var r in existingResults)
        {
            r.IsPublished = false;
            r.UpdatedAt = DateTime.UtcNow;
        }

        var assessmentIds = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var grades = await _dbContext.Grades
            .Where(g => assessmentIds.Contains(g.AssessmentId) && g.IsLocked)
            .ToListAsync(ct);

        foreach (var grade in grades)
        {
            grade.IsLocked = false;
        }

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

     public async Task<ErrorOr<BulkPublishResultDto>> BulkPublishGradesAsync(
         BulkPublishGradesRequest request,
         Guid userId,
         CancellationToken ct = default)
     {
         var sysConfig = await GetSystemConfigurationAsync(ct);
         if (sysConfig.IsError)
             return sysConfig.FirstError;

         var userRoles = await _dbContext.UserRoles
             .Where(ur => ur.UserId == userId)
             .Select(ur => ur.Role.Name)
             .ToListAsync(ct);

         var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin");

         if (!isAdmin && request.ForcePublish)
         {
             return Error.Forbidden("Access.Denied", "Only Administrators are authorized to auto-approve or force-publish results.");
         }

         var query = _dbContext.CourseOfferings
             .Include(co => co.Course)
                 .ThenInclude(c => c.Program)
                     .ThenInclude(p => p.Department)
             .Include(co => co.Programs)
                 .ThenInclude(cop => cop.Program)
                     .ThenInclude(p => p.Department)
             .AsQueryable();

         if (request.AcademicSessionId.HasValue)
         {
             query = query.Where(co => co.AcademicSessionId == request.AcademicSessionId.Value);
         }

         if (request.Semester.HasValue)
         {
             query = query.Where(co => co.Semester == (LMS.Api.Data.Enums.Semester)request.Semester.Value);
         }

         if (request.FacultyId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.Program.Department.FacultyId == request.FacultyId.Value ||
                 co.Programs.Any(cop => cop.Program.Department.FacultyId == request.FacultyId.Value));
         }

         if (request.DepartmentId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.Program.DepartmentId == request.DepartmentId.Value ||
                 co.Programs.Any(cop => cop.Program.DepartmentId == request.DepartmentId.Value));
         }

         if (request.ProgramId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.ProgramId == request.ProgramId.Value ||
                 co.Programs.Any(cop => cop.ProgramId == request.ProgramId.Value));
         }

         var offerings = await query.ToListAsync(ct);

         var details = new List<BulkPublishDetailDto>();
         var publishedCount = 0;
         var skippedCount = 0;

         foreach (var offering in offerings)
         {
             try
             {
                 var approvalWorkflowCompleted = false;
                 var approvals = await _dbContext.GradeApprovals
                     .Where(x => x.CourseOfferingId == offering.Id && x.IsRequired)
                     .ToListAsync(ct);

                 var requiredLevels = new[] { ApprovalLevel.Department, ApprovalLevel.College, ApprovalLevel.Senate };
                 var approvedLevels = approvals.Where(x => x.Status == ApprovalStatus.Approved).Select(x => x.Level).ToHashSet();

                 if (isAdmin && (request.ForcePublish || requiredLevels.Any(l => !approvedLevels.Contains(l))))
                 {
                     foreach (var level in requiredLevels)
                     {
                         var app = approvals.FirstOrDefault(x => x.Level == level);
                         if (app == null)
                         {
                             app = new GradeApproval
                             {
                                 CourseOfferingId = offering.Id,
                                 Level = level,
                                 Status = ApprovalStatus.Approved,
                                 IsRequired = true,
                                 ApprovalOrder = level == ApprovalLevel.Department ? 1 : (level == ApprovalLevel.College ? 2 : 3),
                                 ApprovedById = userId,
                                 ApprovedAt = DateTime.UtcNow,
                                 Comments = "Auto-approved by Administrator"
                             };
                             _dbContext.GradeApprovals.Add(app);
                         }
                         else if (app.Status != ApprovalStatus.Approved)
                         {
                             app.Status = ApprovalStatus.Approved;
                             app.ApprovedById = userId;
                             app.ApprovedAt = DateTime.UtcNow;
                             app.Comments = request.PublicationNotes ?? "Auto-approved by Administrator";
                         }
                     }
                     approvalWorkflowCompleted = true;
                 }
                 else if (!isAdmin && requiredLevels.Any(l => !approvedLevels.Contains(l)))
                 {
                     details.Add(new BulkPublishDetailDto(
                         offering.Id,
                         offering.Course.Code,
                         offering.Course.Title,
                         false,
                         "Grades cannot be published until all approval levels (Department, College, and Senate) are explicitly approved"));
                     skippedCount++;
                     continue;
                 }
                 else
                 {
                     approvalWorkflowCompleted = true;
                 }

                 // Lock all grades
                 var assessments = await _dbContext.Assessments
                     .Where(x => x.CourseOfferingId == offering.Id)
                     .ToListAsync(ct);

                 var grades = await _dbContext.Grades
                     .Where(g => assessments.Select(a => a.Id).Contains(g.AssessmentId))
                     .ToListAsync(ct);

                 foreach (var grade in grades)
                 {
                     grade.IsLocked = true;
                 }

                 // Create or update publication — delete duplicates first to avoid unique constraint issues
                 var existingPublications = await _dbContext.GradePublications
                     .Where(x => x.CourseOfferingId == offering.Id)
                     .OrderBy(x => x.CreatedAt)
                     .ToListAsync(ct);

                 GradePublication? publication;

                 if (existingPublications.Count == 0)
                 {
                     publication = new GradePublication
                     {
                         CourseOfferingId = offering.Id,
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
                     // Keep only the oldest record; remove any duplicates
                     publication = existingPublications.First();
                     if (existingPublications.Count > 1)
                     {
                         _dbContext.GradePublications.RemoveRange(existingPublications.Skip(1));
                     }
                     publication.IsVisibleToStudents = true;
                     publication.ApprovalWorkflowCompleted = approvalWorkflowCompleted;
                     publication.PublicationNotes = request.PublicationNotes;
                 }

                 await _dbContext.SaveChangesAsync(ct);
                 await MaterializeCourseResultsAsync(offering.Id, userId, ct);


                 // Notify students
                 var enrolledStudents = await _dbContext.CourseEnrollments
                     .Where(e => e.CourseOfferingId == offering.Id && e.Status == "Registered")
                     .Select(e => e.StudentId)
                     .ToListAsync(ct);

                 var courseCode = offering.Course?.Code ?? "your course";
                 foreach (var studentId in enrolledStudents)
                 {
                     await _notificationService.CreateAsync(new CreateNotificationRequest(
                         studentId,
                         userId,
                         "Grades Published",
                         $"Grades for {courseCode} have been published.",
                         "System",
                         $"/courses/{offering.Id}/grades"
                     ), ct);
                 }

                 await _auditService.LogAsync("PublishGrades", "GradePublication",
                     publication.Id.ToString(), $"Bulk published grades for course offering {offering.Id}", ct);

                 details.Add(new BulkPublishDetailDto(
                     offering.Id,
                     offering.Course.Code,
                     offering.Course.Title,
                     true,
                     "Published successfully"));
                 publishedCount++;
             }
             catch (Exception ex)
             {
                 details.Add(new BulkPublishDetailDto(
                     offering.Id,
                     offering.Course.Code,
                     offering.Course.Title,
                     false,
                     $"Error: {ex.Message}"));
                 skippedCount++;
             }
         }

         return new BulkPublishResultDto(
             offerings.Count,
             publishedCount,
             skippedCount,
             details);
     }

     public async Task<ErrorOr<BulkUnpublishResultDto>> BulkUnpublishGradesAsync(
         BulkUnpublishGradesRequest request,
         Guid userId,
         CancellationToken ct = default)
     {
         var userRoles = await _dbContext.UserRoles
             .Where(ur => ur.UserId == userId)
             .Select(ur => ur.Role.Name)
             .ToListAsync(ct);

         var isAdmin = userRoles.Any(r => r == "Admin" || r == "SuperAdmin");
         var hasPublishPermission = isAdmin || await _permissionService.HasPermissionAsync(userId, LmsPermissions.ResultsPublish, ct);

         if (!hasPublishPermission)
         {
             return Error.Forbidden("Results.UnpublishNotAllowed", "You do not have permission to unpublish course results. Please contact an administrator.");
         }

         var query = _dbContext.CourseOfferings
             .Include(co => co.Course)
                 .ThenInclude(c => c.Program)
                     .ThenInclude(p => p.Department)
             .Include(co => co.Programs)
                 .ThenInclude(cop => cop.Program)
                     .ThenInclude(p => p.Department)
             .AsQueryable();

         if (request.AcademicSessionId.HasValue)
         {
             query = query.Where(co => co.AcademicSessionId == request.AcademicSessionId.Value);
         }

         if (request.Semester.HasValue)
         {
             query = query.Where(co => co.Semester == (LMS.Api.Data.Enums.Semester)request.Semester.Value);
         }

         if (request.FacultyId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.Program.Department.FacultyId == request.FacultyId.Value ||
                 co.Programs.Any(cop => cop.Program.Department.FacultyId == request.FacultyId.Value));
         }

         if (request.DepartmentId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.Program.DepartmentId == request.DepartmentId.Value ||
                 co.Programs.Any(cop => cop.Program.DepartmentId == request.DepartmentId.Value));
         }

         if (request.ProgramId.HasValue)
         {
             query = query.Where(co =>
                 co.Course.ProgramId == request.ProgramId.Value ||
                 co.Programs.Any(cop => cop.ProgramId == request.ProgramId.Value));
         }

         var offerings = await query.ToListAsync(ct);

         var details = new List<BulkUnpublishDetailDto>();
         var unpublishedCount = 0;
         var skippedCount = 0;

         foreach (var offering in offerings)
         {
             try
             {
                 var existingPublications = await _dbContext.GradePublications
                     .Where(x => x.CourseOfferingId == offering.Id)
                     .ToListAsync(ct);

                 var publishedRecords = existingPublications.Where(x => x.IsVisibleToStudents).ToList();

                 if (publishedRecords.Count == 0)
                 {
                     details.Add(new BulkUnpublishDetailDto(
                         offering.Id,
                         offering.Course.Code,
                         offering.Course.Title,
                         false,
                         "Course is not currently published"));
                     skippedCount++;
                     continue;
                 }

                 foreach (var pub in publishedRecords)
                 {
                     pub.IsVisibleToStudents = false;
                     if (!string.IsNullOrWhiteSpace(request.UnpublicationNotes))
                     {
                         pub.PublicationNotes = request.UnpublicationNotes;
                     }
                 }

                 var existingResults = await _dbContext.StudentCourseResults
                     .Where(r => r.CourseOfferingId == offering.Id)
                     .ToListAsync(ct);
                 foreach (var r in existingResults)
                 {
                     r.IsPublished = false;
                     r.UpdatedAt = DateTime.UtcNow;
                 }

                 var assessmentIds = await _dbContext.Assessments
                     .Where(x => x.CourseOfferingId == offering.Id)
                     .Select(x => x.Id)
                     .ToListAsync(ct);

                 var grades = await _dbContext.Grades
                     .Where(g => assessmentIds.Contains(g.AssessmentId) && g.IsLocked)
                     .ToListAsync(ct);

                 foreach (var grade in grades)
                 {
                     grade.IsLocked = false;
                 }

                 await _dbContext.SaveChangesAsync(ct);

                 await _auditService.LogAsync("UnpublishGrades", "GradePublication",
                     offering.Id.ToString(), $"Bulk unpublished grades for course offering {offering.Id}", ct);

                 details.Add(new BulkUnpublishDetailDto(
                     offering.Id,
                     offering.Course.Code,
                     offering.Course.Title,
                     true,
                     "Unpublished successfully"));
                 unpublishedCount++;
             }
             catch (Exception ex)
             {
                 details.Add(new BulkUnpublishDetailDto(
                     offering.Id,
                     offering.Course.Code,
                     offering.Course.Title,
                     false,
                     $"Error: {ex.Message}"));
                 skippedCount++;
             }
         }

         return new BulkUnpublishResultDto(
             offerings.Count,
             unpublishedCount,
             skippedCount,
             details);
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

    public async Task MaterializeCourseResultsAsync(Guid courseOfferingId, Guid publishedById, CancellationToken ct = default)
    {
        var offering = await _dbContext.CourseOfferings
            .Include(co => co.Course)
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId, ct);

        if (offering == null) return;

        var sysConfigEntity = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

        var enrolledStudentIds = await _dbContext.CourseEnrollments
            .Where(e => e.CourseOfferingId == courseOfferingId && e.Status == "Registered")
            .Select(e => e.StudentId)
            .ToListAsync(ct);

        var gradedStudentIds = await _dbContext.Grades
            .Where(g => g.Assessment.CourseOfferingId == courseOfferingId)
            .Select(g => g.StudentId)
            .Distinct()
            .ToListAsync(ct);

        var allStudentIds = enrolledStudentIds.Union(gradedStudentIds).Distinct().ToList();

        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.Grades)
            .ToListAsync(ct);

        var allGrades = assessments.SelectMany(a => a.Grades).ToList();

        var calculatedGrades = _gradeCalculationEngine.CalculateCourseGrades(
            allStudentIds,
            assessments,
            categories,
            allGrades,
            sysConfigEntity);

        var existingResults = await _dbContext.StudentCourseResults
            .Where(r => r.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var snapshotObj = new
        {
            sysConfigEntity.DefaultGradingStyle,
            sysConfigEntity.DefaultCA1Weight,
            sysConfigEntity.DefaultCA2Weight,
            sysConfigEntity.DefaultCA3Weight,
            sysConfigEntity.DefaultExamWeight,
            sysConfigEntity.RoundingStrategy,
            sysConfigEntity.RoundingDecimalPlaces,
            sysConfigEntity.GraceThreshold,
            CourseCategoryWeights = categories.Select(c => new { c.CategoryName, c.CategoryType, c.Weight })
        };
        var snapshotJson = JsonSerializer.Serialize(snapshotObj);

        foreach (var studentId in allStudentIds)
        {
            if (!calculatedGrades.TryGetValue(studentId, out var calc)) continue;

            var resultRecord = existingResults.FirstOrDefault(r => r.StudentId == studentId);
            if (resultRecord == null)
            {
                resultRecord = new StudentCourseResult
                {
                    CourseOfferingId = courseOfferingId,
                    StudentId = studentId,
                    AcademicSessionId = offering.AcademicSessionId,
                    Semester = (int)offering.Semester,
                    CreditUnits = offering.Course?.CreditUnits ?? 0,
                    Ca1Score = calc.Ca1Score,
                    Ca2Score = calc.Ca2Score,
                    Ca3Score = calc.Ca3Score,
                    ExamScore = calc.ExamScore,
                    TotalScore = calc.TotalScore,
                    LetterGrade = calc.LetterGrade,
                    GradePoints = calc.GradePoints,
                    IsPublished = true,
                    PublishedAt = now,
                    PublishedById = publishedById,
                    CalculationSnapshotJson = snapshotJson,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.StudentCourseResults.Add(resultRecord);
            }
            else
            {
                resultRecord.AcademicSessionId = offering.AcademicSessionId;
                resultRecord.Semester = (int)offering.Semester;
                resultRecord.CreditUnits = offering.Course?.CreditUnits ?? 0;
                resultRecord.Ca1Score = calc.Ca1Score;
                resultRecord.Ca2Score = calc.Ca2Score;
                resultRecord.Ca3Score = calc.Ca3Score;
                resultRecord.ExamScore = calc.ExamScore;
                resultRecord.TotalScore = calc.TotalScore;
                resultRecord.LetterGrade = calc.LetterGrade;
                resultRecord.GradePoints = calc.GradePoints;
                resultRecord.IsPublished = true;
                resultRecord.PublishedAt = now;
                resultRecord.PublishedById = publishedById;
                resultRecord.CalculationSnapshotJson = snapshotJson;
                resultRecord.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }

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

        var isEnrolled = await _dbContext.CourseEnrollments
            .AnyAsync(x => x.CourseOfferingId == courseOfferingId && x.StudentId == studentId && x.Status != "Dropped", ct);

        var hasGrades = await _dbContext.Grades
            .AnyAsync(g => g.StudentId == studentId && g.Assessment.CourseOfferingId == courseOfferingId, ct);

        if (!isEnrolled && !hasGrades)
            return Error.Forbidden("Grades.NotEnrolled", "You are not enrolled in this course and have no grades.");

        // Check for materialized result
        var savedResult = await _dbContext.StudentCourseResults
            .FirstOrDefaultAsync(r => r.CourseOfferingId == courseOfferingId && r.StudentId == studentId && r.IsPublished, ct);

        if (savedResult == null)
        {
            // Auto-materialize for legacy published offerings
            await MaterializeCourseResultsAsync(courseOfferingId, publication.PublishedById, ct);
            savedResult = await _dbContext.StudentCourseResults
                .FirstOrDefaultAsync(r => r.CourseOfferingId == courseOfferingId && r.StudentId == studentId && r.IsPublished, ct);
        }

        var categories = await _dbContext.AssessmentCategories
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);

        var assessments = await _dbContext.Assessments
            .Where(x => x.CourseOfferingId == courseOfferingId)
            .Include(x => x.AssessmentCategory)
            .ToListAsync(ct);

        var grades = await _dbContext.Grades
            .Where(x => x.StudentId == studentId && assessments.Select(a => a.Id).Contains(x.AssessmentId))
            .ToListAsync(ct);

        var sysConfigEntity = await _dbContext.SystemGradingConfigurations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();

        var calculated = _gradeCalculationEngine.CalculateStudentGrade(
            studentId,
            assessments,
            categories,
            grades,
            sysConfigEntity);

        var assessmentGrades = calculated.AssessmentItems.Select(item => new StudentAssessmentGradeDto(
            item.CategoryName,
            item.Title,
            item.MarksObtained,
            item.MaxMarks,
            item.CategoryWeight,
            item.WeightedScore)).ToList();

        decimal finalScore = savedResult?.TotalScore ?? calculated.TotalScore;
        string finalGrade = savedResult?.LetterGrade ?? calculated.LetterGrade;

        return new StudentGradeViewDto(
            offering.Id,
            offering.Course.Code,
            offering.Course.Title,
            offering.AcademicSession.Name,
            (int)offering.Semester,
            assessmentGrades,
            finalScore,
            finalGrade,
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

        // Filter publications to only those where the student is enrolled OR has grades
        var enrolledOfferingIds = await _dbContext.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status != "Dropped")
            .Select(e => e.CourseOfferingId)
            .ToListAsync(ct);

        var gradedOfferingIds = await _dbContext.Grades
            .Where(g => g.StudentId == studentId)
            .Select(g => g.Assessment.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct);

        var relevantOfferingIds = enrolledOfferingIds.Union(gradedOfferingIds).ToHashSet();
        var relevantPublications = publications.Where(p => relevantOfferingIds.Contains(p)).ToList();

        var results = new List<StudentGradeViewDto>();

        foreach (var courseOfferingId in relevantPublications)
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
            var categoryMapping = BuildCategoryMapping(columnMap);
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

                    (decimal TotalRaw, decimal TotalMax, bool HasVal) GetRawMarks(AssessmentCategoryType type)
                    {
                        if (!categoryMapping.TryGetValue(type, out var cols)) return (0, 0, false);
                        decimal totalRaw = 0;
                        decimal totalMax = 0;
                        bool hasVal = false;
                        foreach (var colInfo in cols)
                        {
                            var cellVal = row.Cell(colInfo.ColumnNumber).Value.ToString()?.Trim();
                            if (decimal.TryParse(cellVal, NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
                            {
                                if (colInfo.MaxScore.HasValue && colInfo.MaxScore.Value > 0)
                                {
                                    totalRaw += m;
                                    totalMax += colInfo.MaxScore.Value;
                                }
                                else
                                {
                                    totalRaw += m;
                                    totalMax += 100m; // Default implicit max
                                }
                                hasVal = true;
                            }
                        }
                        return (totalRaw, totalMax, hasVal);
                    }
                    
                    decimal ScaleMarks(decimal totalRaw, decimal totalMax, decimal targetMaxMarks)
                    {
                        if (totalMax > 0)
                        {
                            return Math.Clamp(Math.Ceiling((totalRaw / totalMax) * targetMaxMarks), 0m, targetMaxMarks);
                        }
                        return Math.Clamp(Math.Ceiling(totalRaw), 0m, targetMaxMarks);
                    }
                    
                    decimal? GetDefaultScaledMarks(AssessmentCategoryType type)
                    {
                        var raw = GetRawMarks(type);
                        if (!raw.HasVal) return null;
                        var targetMax = type switch
                        {
                            AssessmentCategoryType.CA1 => 10m,
                            AssessmentCategoryType.CA2 => 10m,
                            AssessmentCategoryType.CA3 => 10m,
                            AssessmentCategoryType.Exam => 70m,
                            _ => 10m
                        };
                        return ScaleMarks(raw.TotalRaw, raw.TotalMax, targetMax); 
                    }

                    var quizScore = GetDefaultScaledMarks(AssessmentCategoryType.CA1);
                    var assignmentScore = GetDefaultScaledMarks(AssessmentCategoryType.CA2);
                    var midsemesterScore = GetDefaultScaledMarks(AssessmentCategoryType.CA3);
                    var examScore = GetDefaultScaledMarks(AssessmentCategoryType.Exam);

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
                        AddRowError(rowNumber, rowEntity.MappingReason);
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
                        AddRowError(rowNumber, rowEntity.MappingReason);
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    var student = await FindStudentAsync(identityNumber, firstName, lastName, ct, academicSessionId);
                    if (student == null)
                    {
                        rowEntity.MappingStatus = "Failed";
                        rowEntity.MappingReason = $"Student not found (identity: {identityNumber})";
                        AddRowError(rowNumber, rowEntity.MappingReason);
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
                        AddRowError(rowNumber, rowEntity.MappingReason);
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
                        AddRowError(rowNumber, rowEntity.MappingReason);
                        upload.FailedRows++;
                        upload.ProcessedRows++;
                        await _dbContext.SaveChangesAsync(ct);
                        continue;
                    }

                    rowEntity.CourseOfferingId = courseOffering.Id;

                    var (enrollment, enrollmentCreated) = await ProvisionEnrollmentAsync(student, appUser, courseOffering, ct);
                    if (enrollmentCreated)
                        provisionedEnrollments++;

                    var (courseEnrollment, courseEnrollmentCreated) = await ProvisionCourseEnrollmentAsync(appUser, courseOffering, userId, ct);
                    if (courseEnrollmentCreated)
                        provisionedCourseEnrollments++;

                    var categories = await EnsureAssessmentCategoriesAsync(courseOffering.Id, ct);
                    var assessments = await EnsureAssessmentsAsync(courseOffering.Id, categories, ct);

                    var rowGradeUploads = 0;
                    foreach (var kvp in categoryMapping)
                    {
                        var categoryType = kvp.Key;
                        var raw = GetRawMarks(categoryType);
                        
                        if (raw.HasVal)
                        {
                            var category = categories.FirstOrDefault(c => c.CategoryType == categoryType);
                            var assessment = category == null
                                ? null
                                : assessments.FirstOrDefault(a => a.AssessmentCategoryId == category.Id);

                            if (assessment != null)
                            {
                                var scaledMarks = ScaleMarks(raw.TotalRaw, raw.TotalMax, assessment.MaxMarks);
                                
                                var existingGrade = await _dbContext.Grades
                                    .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == appUser.Id, ct);

                                if (existingGrade == null)
                                {
                                    var grade = new Grade
                                    {
                                        AssessmentId = assessment.Id,
                                        StudentId = appUser.Id,
                                        MarksObtained = scaledMarks,
                                        CreatedById = userId,
                                        UpdatedById = userId
                                    };
                                    _dbContext.Grades.Add(grade);
                                    rowGradeUploads++;
                                }
                                else if (!existingGrade.IsLocked)
                                {
                                    existingGrade.MarksObtained = scaledMarks;
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

    private decimal? ExtractMaxScore(string header)
    {
        var parts = header.Split(new[] { '/', '-', '_', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var lastPart = parts.Last().Trim();
            if (decimal.TryParse(lastPart, out var max) && max > 0)
                return max;
        }
        return null;
    }

    private Dictionary<AssessmentCategoryType, List<(int ColumnNumber, decimal? MaxScore)>> BuildCategoryMapping(Dictionary<string, int> columnMap)
    {
        var mapping = new Dictionary<AssessmentCategoryType, List<(int ColumnNumber, decimal? MaxScore)>>();
        
        var exactCa1 = FindColumnByPrefix(columnMap, new[] { "quiz", "ca1" });
        var exactCa2 = FindColumnByPrefix(columnMap, new[] { "assignment", "ca2" });
        var exactCa3 = FindColumnByPrefix(columnMap, new[] { "midsemester test", "mid-semester test", "mid semester test", "ca3" });
        
        var examCol = FindColumnByPrefix(columnMap, new[] { "exam", "examination" });
        if (examCol == null)
        {
            var pCol = FindPartialColumn(columnMap, new[] { "exam", "examination" });
            if (pCol != null)
            {
                var pKey = columnMap.FirstOrDefault(x => x.Value == pCol.Value).Key;
                examCol = (pCol.Value, ExtractMaxScore(pKey));
            }
        }
        
        if (examCol != null) mapping[AssessmentCategoryType.Exam] = new List<(int, decimal?)> { examCol.Value };

        bool hasExactCa1 = exactCa1.HasValue;
        bool hasExactCa2 = exactCa2.HasValue;
        bool hasExactCa3 = exactCa3.HasValue;

        if (hasExactCa1) mapping[AssessmentCategoryType.CA1] = new List<(int, decimal?)> { exactCa1!.Value };
        if (hasExactCa2) mapping[AssessmentCategoryType.CA2] = new List<(int, decimal?)> { exactCa2!.Value };
        if (hasExactCa3) mapping[AssessmentCategoryType.CA3] = new List<(int, decimal?)> { exactCa3!.Value };

        var allCaKeywords = new[] { "quiz", "assignment", "test", "practical", "ca1", "ca2", "ca3", "assessment" };
        var genericCaCols = new List<(int ColumnNumber, decimal? MaxScore)>();
        
        foreach (var kvp in columnMap)
        {
            if (exactCa1?.ColumnNumber == kvp.Value || exactCa2?.ColumnNumber == kvp.Value || exactCa3?.ColumnNumber == kvp.Value || examCol?.ColumnNumber == kvp.Value)
                continue;
                
            if (allCaKeywords.Any(k => kvp.Key.Contains(k)))
            {
                genericCaCols.Add((kvp.Value, ExtractMaxScore(kvp.Key)));
            }
        }
        
        if (genericCaCols.Any())
        {
            if (!hasExactCa1) 
            {
                if (hasExactCa2 || hasExactCa3)
                {
                    mapping[AssessmentCategoryType.CA1] = new List<(int, decimal?)>(genericCaCols);
                }
                else
                {
                    var availableTypes = new[] { AssessmentCategoryType.CA1, AssessmentCategoryType.CA2, AssessmentCategoryType.CA3 };
                    int typeIdx = 0;
                    
                    foreach (var colInfo in genericCaCols)
                    {
                        if (typeIdx < availableTypes.Length)
                        {
                            mapping[availableTypes[typeIdx]] = new List<(int, decimal?)> { colInfo };
                            typeIdx++;
                        }
                        else
                        {
                            mapping[AssessmentCategoryType.CA3].Add(colInfo);
                        }
                    }
                }
            }
        }
        
        return mapping;
    }

    private (int ColumnNumber, decimal? MaxScore)? FindColumnByPrefix(Dictionary<string, int> columnMap, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var normalized = alias.ToLowerInvariant().Trim();
            
            if (columnMap.TryGetValue(normalized, out var colNum))
                return (colNum, ExtractMaxScore(normalized));
                
            foreach (var kvp in columnMap)
            {
                var key = kvp.Key;
                if (key.StartsWith(normalized))
                {
                    if (key.Length == normalized.Length)
                        return (kvp.Value, ExtractMaxScore(key));
                        
                    char nextChar = key[normalized.Length];
                    if (!char.IsLetterOrDigit(nextChar))
                    {
                        return (kvp.Value, ExtractMaxScore(key));
                    }
                }
            }
        }
        return null;
    }

    private int? FindPartialColumn(Dictionary<string, int> columnMap, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var normalized = alias.ToLowerInvariant().Trim();
            foreach (var kvp in columnMap)
            {
                if (kvp.Key.Contains(normalized))
                    return kvp.Value;
            }
        }
        return null;
    }

    private static bool IsRepeatedHeaderRow(string identityNumber, string firstName, string lastName)
    {
        return identityNumber.Equals("identity number", StringComparison.OrdinalIgnoreCase)
            && firstName.Equals("first name", StringComparison.OrdinalIgnoreCase)
            && lastName.Equals("last name", StringComparison.OrdinalIgnoreCase);
    }

    public record struct ParsedMatricInfo(string Program, int Year, int Sequence);

    public static ParsedMatricInfo? ParseMatricNumber(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim().ToUpperInvariant();

        // 1. Slashed or separated forms: WU/...
        var parts = trimmed.Split(new[] { '/', '-', ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0] == "WU")
        {
            string? prog = null;
            int? year = null;
            int? seq = null;

            // Handle 5 parts: WU/{PROGRAM}/{YEAR1}/{YEAR2}/{SEQ} e.g. WU/ENG/2025/2026/026
            if (parts.Length == 5)
            {
                prog = parts[1];
                if (int.TryParse(parts[2], out var y1))
                {
                    year = y1 < 100 ? 2000 + y1 : y1;
                }
                if (int.TryParse(parts[4], out var sVal))
                {
                    seq = sVal;
                }
            }
            // Handle 4 parts:
            // Format A: WU/{PROGRAM}/{YEAR}/{SEQ} e.g. WU/ENG/2024/025 or WU/ENG/24/025
            // Format B: WU/{YEAR}/{PROGRAM}/{SEQ} e.g. WU/24/ENG/025 or WU/2024/ENG/025
            else if (parts.Length == 4)
            {
                if (int.TryParse(parts[1], out var yValB))
                {
                    // Format B: parts[1] is Year, parts[2] is Program
                    year = yValB < 100 ? 2000 + yValB : yValB;
                    prog = parts[2];
                }
                else
                {
                    // Format A: parts[1] is Program, parts[2] is Year
                    prog = parts[1];
                    if (int.TryParse(parts[2], out var yValA))
                    {
                        year = yValA < 100 ? 2000 + yValA : yValA;
                    }
                }

                if (int.TryParse(parts[3], out var sVal))
                {
                    seq = sVal;
                }
            }
            // Handle 3 parts: e.g. WU/{PROGRAM}/{SEQ}
            else if (parts.Length == 3)
            {
                prog = parts[1];
                if (int.TryParse(parts[2], out var sVal))
                {
                    seq = sVal;
                }
            }

            if (!string.IsNullOrWhiteSpace(prog) && year.HasValue && seq.HasValue)
            {
                if (prog == "ARTS") prog = "ART";
                if (prog == "MISS") prog = "MSS";
                return new ParsedMatricInfo(prog, year.Value, seq.Value);
            }
        }

        // 2. Unslashed: WU{PROG}{YEAR}{SEQ} e.g. WUENG2025022 or WUENG25022
        var matchNoSlash1 = Regex.Match(trimmed, @"^WU(?<prog>[A-Z]{2,6})(?<year>20\d{2}|\d{2})(?<seq>\d{2,5})$");
        if (matchNoSlash1.Success)
        {
            var prog = matchNoSlash1.Groups["prog"].Value;
            if (prog == "ARTS") prog = "ART";
            if (prog == "MISS") prog = "MSS";
            var yVal = int.Parse(matchNoSlash1.Groups["year"].Value);
            var year = yVal < 100 ? 2000 + yVal : yVal;
            var seq = int.Parse(matchNoSlash1.Groups["seq"].Value);
            return new ParsedMatricInfo(prog, year, seq);
        }

        // 3. Unslashed: WU{YEAR}{PROG}{SEQ} e.g. WU25ENG022 or WU2025ENG022
        var matchNoSlash2 = Regex.Match(trimmed, @"^WU(?<year>20\d{2}|\d{2})(?<prog>[A-Z]{2,6})(?<seq>\d{2,5})$");
        if (matchNoSlash2.Success)
        {
            var prog = matchNoSlash2.Groups["prog"].Value;
            if (prog == "ARTS") prog = "ART";
            if (prog == "MISS") prog = "MSS";
            var yVal = int.Parse(matchNoSlash2.Groups["year"].Value);
            var year = yVal < 100 ? 2000 + yVal : yVal;
            var seq = int.Parse(matchNoSlash2.Groups["seq"].Value);
            return new ParsedMatricInfo(prog, year, seq);
        }

        return null;
    }

    public static string NormalizeMatricNumber(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var parsed = ParseMatricNumber(input);
        if (parsed.HasValue)
        {
            var p = parsed.Value;
            // Canonical format: WU/{PROGRAM}/{YYYY}/{SEQ}
            return $"WU/{p.Program}/{p.Year}/{p.Sequence:D4}";
        }
        return input.Trim().ToUpperInvariant();
    }

    public static bool AreMatricNumbersEquivalent(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        var cleanA = a.Trim();
        var cleanB = b.Trim();
        if (string.Equals(cleanA, cleanB, StringComparison.OrdinalIgnoreCase)) return true;

        var parsedA = ParseMatricNumber(cleanA);
        var parsedB = ParseMatricNumber(cleanB);
        if (parsedA.HasValue && parsedB.HasValue)
        {
            return string.Equals(parsedA.Value.Program, parsedB.Value.Program, StringComparison.OrdinalIgnoreCase) &&
                   parsedA.Value.Year == parsedB.Value.Year &&
                   parsedA.Value.Sequence == parsedB.Value.Sequence;
        }

        var normA = NormalizeMatricNumber(cleanA);
        var normB = NormalizeMatricNumber(cleanB);
        return !string.IsNullOrEmpty(normA) && string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Student?> FindStudentAsync(string identityNumber, string firstName, string lastName, CancellationToken ct, Guid? academicSessionId = null)
    {
        var cleanId = identityNumber?.Trim() ?? string.Empty;
        var cleanFirst = firstName?.Trim() ?? string.Empty;
        var cleanLast = lastName?.Trim() ?? string.Empty;
        var normMatric = NormalizeMatricNumber(cleanId);

        if (!string.IsNullOrWhiteSpace(cleanId))
        {
            // 1. Direct match by StudentNumber (exact, normalized, or case-insensitive)
            var student = await _dbContext.Students
                .FirstOrDefaultAsync(s => s.StudentNumber == cleanId || 
                                          (!string.IsNullOrEmpty(normMatric) && s.StudentNumber == normMatric) ||
                                          (s.StudentNumber != null && s.StudentNumber.ToLower() == cleanId.ToLower()), ct);

            if (student != null)
            {
                if (!string.IsNullOrEmpty(normMatric) && student.StudentNumber != normMatric)
                {
                    student.StudentNumber = normMatric;
                    student.UpdatedAt = DateTime.UtcNow;
                }
                return student;
            }

            // 2. Match by JAMB Registration Number
            student = await _dbContext.Students
                .FirstOrDefaultAsync(s => s.JambRegistrationNumber == cleanId || (s.JambRegistrationNumber != null && s.JambRegistrationNumber.ToLower() == cleanId.ToLower()), ct);

            if (student != null)
            {
                if (string.IsNullOrWhiteSpace(student.StudentNumber) && !string.IsNullOrEmpty(normMatric))
                {
                    student.StudentNumber = normMatric;
                    student.UpdatedAt = DateTime.UtcNow;
                }
                return student;
            }

            // 3. Match normalized StudentNumber (comparing normalized forms in memory)
            if (!string.IsNullOrEmpty(normMatric))
            {
                var allStudents = await _dbContext.Students.ToListAsync(ct);
                student = allStudents.FirstOrDefault(s => 
                    !string.IsNullOrWhiteSpace(s.StudentNumber) && 
                    AreMatricNumbersEquivalent(s.StudentNumber, cleanId));

                if (student != null)
                {
                    if (student.StudentNumber != normMatric)
                    {
                        student.StudentNumber = normMatric;
                        student.UpdatedAt = DateTime.UtcNow;
                    }
                    return student;
                }
            }
        }

        // 4. Match by First and Last Name
        if (!string.IsNullOrWhiteSpace(cleanFirst) && !string.IsNullOrWhiteSpace(cleanLast))
        {
            var matchedStudents = await _dbContext.Students
                .Where(s => s.FirstName.ToLower() == cleanFirst.ToLower() && s.LastName.ToLower() == cleanLast.ToLower())
                .ToListAsync(ct);

            if (matchedStudents.Count == 1)
            {
                var student = matchedStudents.First();

                if (string.IsNullOrWhiteSpace(student.StudentNumber) && !string.IsNullOrWhiteSpace(normMatric))
                {
                    student.StudentNumber = normMatric;
                    student.UpdatedAt = DateTime.UtcNow;

                    var auditLog = new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = null,
                        Action = "AutoAssignMatricNumber",
                        EntityName = "Student",
                        EntityId = student.Id.ToString(),
                        Changes = $"System auto-assigned Matric Number '{normMatric}' to student '{student.FirstName} {student.LastName}' during result upload.",
                        Timestamp = DateTime.UtcNow
                    };
                    _dbContext.AuditLogs.Add(auditLog);
                }
                return student;
            }
        }

        // 5. Check if user already exists in Users
        AppUser? existingUser = null;
        if (!string.IsNullOrWhiteSpace(normMatric))
        {
            existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == normMatric || u.Username == cleanId, ct);
        }

        if (existingUser == null && !string.IsNullOrWhiteSpace(cleanFirst) && !string.IsNullOrWhiteSpace(cleanLast))
        {
            var fullName = $"{cleanFirst} {cleanLast}".ToLower();
            existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => 
                (u.DisplayName != null && u.DisplayName.ToLower() == fullName) ||
                (u.Email != null && u.Email.ToLower().StartsWith($"{cleanFirst.ToLower()}.{cleanLast.ToLower()}")), ct);
        }

        if (existingUser != null)
        {
            var studentFromUser = await _dbContext.Students.FirstOrDefaultAsync(s => s.Id == existingUser.Id, ct);
            if (studentFromUser != null)
            {
                if (string.IsNullOrWhiteSpace(studentFromUser.StudentNumber) && !string.IsNullOrWhiteSpace(normMatric))
                {
                    studentFromUser.StudentNumber = normMatric;
                    studentFromUser.UpdatedAt = DateTime.UtcNow;
                }
                return studentFromUser;
            }

            // User exists, but Student record missing; auto-create Student entity for this user
            var targetSessionId = academicSessionId ?? await GetSessionIdForMatricAsync(normMatric, ct);
            if (targetSessionId != Guid.Empty)
            {
                var newStudent = new Student
                {
                    Id = existingUser.Id,
                    EntraObjectId = existingUser.EntraObjectId,
                    AcademicSessionId = targetSessionId,
                    FirstName = cleanFirst,
                    LastName = cleanLast,
                    StudentNumber = !string.IsNullOrWhiteSpace(normMatric) ? normMatric : cleanId,
                    OfficialEmail = existingUser.Email ?? $"{cleanFirst.ToLower().Replace(" ", "")}.{cleanLast.ToLower().Replace(" ", "")}24@wigweuniversity.edu.ng",
                    PersonalEmail = existingUser.Email ?? $"{cleanFirst.ToLower().Replace(" ", "")}.{cleanLast.ToLower().Replace(" ", "")}24@wigweuniversity.edu.ng",
                    Phone = string.Empty,
                    Status = StudentStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await AssignProgramFromMatricAsync(newStudent, normMatric, ct);
                _dbContext.Students.Add(newStudent);
                await _dbContext.SaveChangesAsync(ct);
                return newStudent;
            }
        }

        // 6. Auto-provision Student if valid matric number and names exist
        if (!string.IsNullOrWhiteSpace(normMatric) && !string.IsNullOrWhiteSpace(cleanFirst) && !string.IsNullOrWhiteSpace(cleanLast))
        {
            var targetSessionId = academicSessionId ?? await GetSessionIdForMatricAsync(normMatric, ct);
            if (targetSessionId != Guid.Empty)
            {
                var newId = Guid.NewGuid();
                var officialEmail = $"{cleanFirst.ToLower().Replace(" ", "")}.{cleanLast.ToLower().Replace(" ", "")}24@wigweuniversity.edu.ng";
                
                var newStudent = new Student
                {
                    Id = newId,
                    EntraObjectId = $"student:{newId}",
                    AcademicSessionId = targetSessionId,
                    FirstName = cleanFirst,
                    LastName = cleanLast,
                    StudentNumber = normMatric,
                    OfficialEmail = officialEmail,
                    PersonalEmail = officialEmail,
                    Phone = string.Empty,
                    Status = StudentStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await AssignProgramFromMatricAsync(newStudent, normMatric, ct);
                _dbContext.Students.Add(newStudent);
                await _dbContext.SaveChangesAsync(ct);
                return newStudent;
            }
        }

        return null;
    }

    private async Task<Guid> GetSessionIdForMatricAsync(string? matric, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(matric))
        {
            var parsed = ParseMatricNumber(matric);
            if (parsed.HasValue)
            {
                var year = parsed.Value.Year;
                var sessionPattern = $"{year}/{year + 1}";
                var session = await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.Name.Contains(sessionPattern), ct);
                if (session != null) return session.Id;
            }
            else
            {
                var parts = matric.Split('/');
                if (parts.Length >= 2 && parts[1] == "24")
                {
                    var session24 = await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.Name.Contains("2024/2025"), ct);
                    if (session24 != null) return session24.Id;
                }
                if (parts.Length >= 2 && parts[1] == "25")
                {
                    var session25 = await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.Name.Contains("2025/2026"), ct);
                    if (session25 != null) return session25.Id;
                }
            }
        }

        var activeSession = await _dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct);
        return activeSession?.Id ?? Guid.Empty;
    }

    private async Task AssignProgramFromMatricAsync(Student student, string? matric, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(matric)) return;
        var parsed = ParseMatricNumber(matric);
        var progCode = parsed?.Program;
        if (string.IsNullOrWhiteSpace(progCode))
        {
            var parts = matric.Split(new[] { '/', '-', ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                progCode = parts.Length >= 4 && int.TryParse(parts[1], out _) ? parts[2] : parts[1];
            }
            else if (parts.Length == 2)
            {
                progCode = parts[1];
            }
        }
        if (string.IsNullOrWhiteSpace(progCode)) return;

        var prog = await _dbContext.Programs.FirstOrDefaultAsync(p => 
            p.Code.Contains(progCode) || 
            (progCode == "ART" && (p.Code == "BCDM" || p.Code == "BFA" || p.Code == "BTA")) ||
            (progCode == "CSC" && (p.Code == "BCS" || p.Code == "BSE" || p.Code == "BCY")) ||
            (progCode == "ENG" && (p.Code == "BME" || p.Code == "BEE" || p.Code == "BCEN")) ||
            (progCode == "MSS" && (p.Code == "BBAM" || p.Code == "BADA" || p.Code == "BECO")), ct);

        if (prog != null)
        {
            student.AcademicProgramId = prog.Id;
            var dept = await _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == prog.DepartmentId, ct);
            if (dept != null)
            {
                student.FacultyId = dept.FacultyId;
            }

            var level = await _dbContext.Levels.FirstOrDefaultAsync(l => l.ProgramId == prog.Id && l.Order == 1, ct);
            if (level != null)
            {
                student.LevelId = level.Id;
            }
        }
    }

    private async Task<(AppUser? User, bool Created)> ProvisionAppUserAsync(Student student, CancellationToken ct)
    {
        var existingUser = await _dbContext.Users.FindAsync(new object[] { student.Id }, ct);
        if (existingUser != null)
            return (existingUser, false);

        if (!string.IsNullOrWhiteSpace(student.StudentNumber))
        {
            existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == student.StudentNumber, ct);
            if (existingUser != null)
            {
                // Ensure IDs match if possible, or just return existing
                if (student.Id == Guid.Empty) student.Id = existingUser.Id;
                return (existingUser, false);
            }
        }

        if (!string.IsNullOrWhiteSpace(student.EntraObjectId))
        {
            existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntraObjectId == student.EntraObjectId, ct);
            if (existingUser != null)
            {
                if (student.Id == Guid.Empty) student.Id = existingUser.Id;
                return (existingUser, false);
            }
        }

        if (!string.IsNullOrWhiteSpace(student.OfficialEmail))
        {
            existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == student.OfficialEmail, ct);
            if (existingUser != null)
            {
                if (!string.IsNullOrWhiteSpace(student.EntraObjectId))
                    existingUser.EntraObjectId = student.EntraObjectId;
                existingUser.DisplayName = $"{student.FirstName} {student.LastName}";
                existingUser.UpdatedUtc = DateTime.UtcNow;

                if (student.Id == Guid.Empty) student.Id = existingUser.Id;
                return (existingUser, false);
            }
        }

        var appUser = new AppUser
        {
            Id = student.Id != Guid.Empty ? student.Id : Guid.NewGuid(),
            EntraObjectId = student.EntraObjectId ?? $"student:{student.Id}",
            Username = student.StudentNumber,
            Email = student.OfficialEmail,
            DisplayName = $"{student.FirstName} {student.LastName}",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        student.Id = appUser.Id;

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
        if (!student.AcademicProgramId.HasValue)
        {
            await AssignProgramFromMatricAsync(student, student.StudentNumber, ct);
        }

        if (student.AcademicProgramId.HasValue && !student.LevelId.HasValue)
        {
            var defaultLevel = await _dbContext.Levels
                .Where(l => l.ProgramId == student.AcademicProgramId.Value && l.Order == 1)
                .FirstOrDefaultAsync(ct);
            if (defaultLevel != null)
            {
                student.LevelId = defaultLevel.Id;
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        if (!student.AcademicProgramId.HasValue || !student.LevelId.HasValue)
        {
            var fallbackProg = await _dbContext.Programs.FirstOrDefaultAsync(p => p.IsActive, ct);
            if (fallbackProg != null)
            {
                student.AcademicProgramId = fallbackProg.Id;
                var fallbackLevel = await _dbContext.Levels.FirstOrDefaultAsync(l => l.ProgramId == fallbackProg.Id && l.Order == 1, ct);
                if (fallbackLevel != null)
                {
                    student.LevelId = fallbackLevel.Id;
                }
                await _dbContext.SaveChangesAsync(ct);
            }
        }

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

    private async Task<(ProgramEnrollment? Enrollment, bool Created)> ProvisionEnrollmentAsync(Student student, AppUser appUser, CourseOffering offering, CancellationToken ct)
    {
        if (!offering.CurriculumId.HasValue)
            return (null, false);

        // Find program enrollment via offering programs
        var offeringProgramId = await _dbContext.CourseOfferingPrograms
            .Where(p => p.CourseOfferingId == offering.Id)
            .Select(p => (Guid?)p.ProgramId)
            .FirstOrDefaultAsync(ct);

        if (!offeringProgramId.HasValue)
            return (null, false);

        var enrollment = await _dbContext.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == appUser.Id && e.AcademicSessionId == offering.AcademicSessionId, ct);

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
        AppUser appUser,
        CourseOffering offering,
        Guid userId,
        CancellationToken ct)
    {

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

        var defaultCA1 = sysConfig?.DefaultCA1Weight ?? 10m;
        var defaultCA2 = sysConfig?.DefaultCA2Weight ?? 10m;
        var defaultCA3 = sysConfig?.DefaultCA3Weight ?? 10m;
        var defaultExam = sysConfig?.DefaultExamWeight ?? 70m;

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
                    MaxMarks = weight,
                    IsExamCategory = categoryType == AssessmentCategoryType.Exam,
                    DisplayOrder = (int)categoryType
                };
                _dbContext.AssessmentCategories.Add(category);
                categories.Add(category);
            }
            else
            {
                bool modified = false;
                if (existing.Weight != weight)
                {
                    existing.Weight = weight;
                    modified = true;
                }
                if (existing.MaxMarks != weight)
                {
                    existing.MaxMarks = weight;
                    modified = true;
                }
                if (modified)
                {
                    _dbContext.AssessmentCategories.Update(existing);
                }
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

        bool assessmentsChanged = false;
        foreach (var category in categories)
        {
            var existingAssessment = assessments.FirstOrDefault(a => a.AssessmentCategoryId == category.Id);
            if (existingAssessment == null)
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
                assessmentsChanged = true;
            }
            else if (existingAssessment.MaxMarks != category.MaxMarks)
            {
                existingAssessment.MaxMarks = category.MaxMarks;
                _dbContext.Assessments.Update(existingAssessment);
                assessmentsChanged = true;
            }
        }

        if (assessmentsChanged)
            await _dbContext.SaveChangesAsync(ct);

        return assessments;
    }

    public async Task<ErrorOr<string>> BatchMigrateClassterFolderAsync(Guid userId, CancellationToken ct = default)
    {
        var session2425 = await _dbContext.AcademicSessions
            .FirstOrDefaultAsync(s => s.Name == "2024/2025", ct);

        if (session2425 == null)
            return Error.NotFound("Session.NotFound", "Academic Session 2024/2025 not found");

        var folderPath = @"/Users/mac/Apps/LMS APP/Classter Data";
        if (!Directory.Exists(folderPath))
            return Error.NotFound("Folder.NotFound", $"Classter Data folder not found at {folderPath}");

        var files = Directory.GetFiles(folderPath, "*.xlsx")
            .Where(f => !Path.GetFileName(f).StartsWith(".~"))
            .ToList();

        var defaultProgramId = await _dbContext.Programs.Select(p => p.Id).FirstOrDefaultAsync(ct);
        if (defaultProgramId == Guid.Empty)
            return Error.NotFound("Program.NotFound", "No academic program found in database");

        var processedCount = 0;
        var publishedCount = 0;
        var errors = new List<string>();

        var allCourses = await _dbContext.Courses.ToListAsync(ct);

        foreach (var filePath in files)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);
                var rawCode = Path.GetFileNameWithoutExtension(filePath).Trim();
                var cleanCode = rawCode.Replace("-", " ").Trim();

                // Find matching course
                var course = allCourses.FirstOrDefault(c =>
                    string.Equals(c.Code.Replace("-", " ").Trim(), cleanCode, StringComparison.OrdinalIgnoreCase));

                if (course == null)
                {
                    // Create course if missing
                    course = new Course
                    {
                        Code = rawCode,
                        Title = rawCode,
                        CreditUnits = 3,
                        ProgramId = defaultProgramId,
                        IsActive = true
                    };
                    _dbContext.Courses.Add(course);
                    await _dbContext.SaveChangesAsync(ct);
                    allCourses.Add(course);
                }

                using var fileStream = File.OpenRead(filePath);
                var formFile = new FormFile(fileStream, 0, fileStream.Length, "excelFile", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                };

                var migrationResult = await MigrateClassterGradesAsync(session2425.Id, course.Id, formFile, userId, null, ct);
                if (migrationResult.IsError)
                {
                    errors.Add($"{fileName}: {migrationResult.FirstError.Description}");
                    continue;
                }

                // Ensure GradePublications for this course in 2024/2025
                var offerings = await _dbContext.CourseOfferings
                    .Where(co => co.CourseId == course.Id && co.AcademicSessionId == session2425.Id)
                    .ToListAsync(ct);

                foreach (var offering in offerings)
                {
                    var publication = await _dbContext.GradePublications
                        .FirstOrDefaultAsync(p => p.CourseOfferingId == offering.Id, ct);

                    if (publication == null)
                    {
                        publication = new GradePublication
                        {
                            CourseOfferingId = offering.Id,
                            PublishedById = userId,
                            IsVisibleToStudents = true,
                            ApprovalWorkflowCompleted = true,
                            AcademicSessionId = session2425.Id,
                            Semester = (int)offering.Semester,
                            PublicationNotes = "Published from Classter Data Folder Migration"
                        };
                        _dbContext.GradePublications.Add(publication);
                    }
                    else
                    {
                        publication.IsVisibleToStudents = true;
                        publication.ApprovalWorkflowCompleted = true;
                        publication.PublishedById = userId;
                        publication.PublicationNotes = "Published from Classter Data Folder Migration";
                    }

                    // Auto-approve grade approvals
                    var requiredLevels = new[] { ApprovalLevel.Department, ApprovalLevel.College, ApprovalLevel.Senate };
                    foreach (var level in requiredLevels)
                    {
                        var app = await _dbContext.GradeApprovals
                            .FirstOrDefaultAsync(a => a.CourseOfferingId == offering.Id && a.Level == level, ct);

                        if (app == null)
                        {
                            app = new GradeApproval
                            {
                                CourseOfferingId = offering.Id,
                                Level = level,
                                Status = ApprovalStatus.Approved,
                                IsRequired = true,
                                ApprovalOrder = level == ApprovalLevel.Department ? 1 : (level == ApprovalLevel.College ? 2 : 3),
                                ApprovedById = userId,
                                ApprovedAt = DateTime.UtcNow,
                                Comments = "Approved via Classter Data Migration"
                            };
                            _dbContext.GradeApprovals.Add(app);
                        }
                        else if (app.Status != ApprovalStatus.Approved)
                        {
                            app.Status = ApprovalStatus.Approved;
                            app.ApprovedById = userId;
                            app.ApprovedAt = DateTime.UtcNow;
                            app.Comments = "Approved via Classter Data Migration";
                        }
                    }

                    publishedCount++;
                }

                await _dbContext.SaveChangesAsync(ct);

                foreach (var offering in offerings)
                {
                    await MaterializeCourseResultsAsync(offering.Id, userId, ct);
                }

                processedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        var message = $"Classter Data Folder Migration completed: Processed {processedCount}/{files.Count} files, {publishedCount} 2024/2025 course offerings published. Errors: {errors.Count}.";
        return message;
    }

    public async Task<ErrorOr<string>> RepairMigratedGradesAndResultsAsync(Guid userId, CancellationToken ct = default)
    {
        var sysConfig = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (sysConfig == null)
        {
            sysConfig = new SystemGradingConfiguration
            {
                RoundingStrategy = RoundingStrategy.Ceiling,
                RoundingDecimalPlaces = 0
            };
            _dbContext.SystemGradingConfigurations.Add(sysConfig);
            await _dbContext.SaveChangesAsync(ct);
        }
        else if (sysConfig.RoundingStrategy != RoundingStrategy.Ceiling || sysConfig.RoundingDecimalPlaces != 0)
        {
            sysConfig.RoundingStrategy = RoundingStrategy.Ceiling;
            sysConfig.RoundingDecimalPlaces = 0;
            sysConfig.UpdatedAt = DateTime.UtcNow;
            _dbContext.SystemGradingConfigurations.Update(sysConfig);
            await _dbContext.SaveChangesAsync(ct);
        }

        var ca1Weight = sysConfig.DefaultCA1Weight > 0 ? sysConfig.DefaultCA1Weight : 10m;
        var ca2Weight = sysConfig.DefaultCA2Weight > 0 ? sysConfig.DefaultCA2Weight : 10m;
        var ca3Weight = sysConfig.DefaultCA3Weight > 0 ? sysConfig.DefaultCA3Weight : 10m;
        var examWeight = sysConfig.DefaultExamWeight > 0 ? sysConfig.DefaultExamWeight : 70m;

        // 1. Repair AssessmentCategories where MaxMarks was incorrectly set to 100m instead of weight
        var categories = await _dbContext.AssessmentCategories.ToListAsync(ct);
        int categoriesRepaired = 0;
        foreach (var cat in categories)
        {
            var expectedMax = cat.CategoryType switch
            {
                AssessmentCategoryType.CA1 => ca1Weight,
                AssessmentCategoryType.CA2 => ca2Weight,
                AssessmentCategoryType.CA3 => ca3Weight,
                AssessmentCategoryType.Exam => examWeight,
                _ => cat.Weight > 0 ? cat.Weight : cat.MaxMarks
            };

            if (cat.MaxMarks != expectedMax)
            {
                cat.MaxMarks = expectedMax;
                categoriesRepaired++;
            }
        }

        // 2. Repair Assessments to ensure MaxMarks matches the category MaxMarks
        var assessments = await _dbContext.Assessments
            .Include(a => a.AssessmentCategory)
            .ToListAsync(ct);
        int assessmentsRepaired = 0;
        foreach (var ass in assessments)
        {
            if (ass.AssessmentCategory != null && ass.MaxMarks != ass.AssessmentCategory.MaxMarks)
            {
                ass.MaxMarks = ass.AssessmentCategory.MaxMarks;
                assessmentsRepaired++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // 3. Repair Grades: rescale any inflated grades and round up values to whole numbers
        var grades = await _dbContext.Grades
            .Include(g => g.Assessment)
                .ThenInclude(a => a.AssessmentCategory)
            .ToListAsync(ct);

        int gradesRepaired = 0;
        var affectedOfferingIds = new HashSet<Guid>();

        foreach (var grade in grades)
        {
            if (grade.Assessment?.AssessmentCategory == null) continue;
            var catType = grade.Assessment.AssessmentCategory.CategoryType;
            var maxMarks = grade.Assessment.MaxMarks;

            if (catType == AssessmentCategoryType.Exam)
            {
                if (grade.MarksObtained > maxMarks)
                {
                    // Scale from 100 down to target max (e.g. 70m) and round up
                    grade.MarksObtained = Math.Clamp(Math.Ceiling((grade.MarksObtained / 100m) * maxMarks), 0m, maxMarks);
                    grade.UpdatedAt = DateTime.UtcNow;
                    grade.UpdatedById = userId;
                    gradesRepaired++;
                    affectedOfferingIds.Add(grade.Assessment.CourseOfferingId);
                }
                else if (grade.MarksObtained != Math.Ceiling(grade.MarksObtained))
                {
                    // Round up fractional marks
                    grade.MarksObtained = Math.Clamp(Math.Ceiling(grade.MarksObtained), 0m, maxMarks);
                    grade.UpdatedAt = DateTime.UtcNow;
                    grade.UpdatedById = userId;
                    gradesRepaired++;
                    affectedOfferingIds.Add(grade.Assessment.CourseOfferingId);
                }
            }
            else
            {
                if (grade.MarksObtained > maxMarks)
                {
                    // Scale from 100 down to target max (e.g. 10m): 90 -> 9, and round up
                    grade.MarksObtained = Math.Clamp(Math.Ceiling(grade.MarksObtained / 10m), 0m, maxMarks);
                    grade.UpdatedAt = DateTime.UtcNow;
                    grade.UpdatedById = userId;
                    gradesRepaired++;
                    affectedOfferingIds.Add(grade.Assessment.CourseOfferingId);
                }
                else if (grade.MarksObtained != Math.Ceiling(grade.MarksObtained))
                {
                    // Round up fractional marks
                    grade.MarksObtained = Math.Clamp(Math.Ceiling(grade.MarksObtained), 0m, maxMarks);
                    grade.UpdatedAt = DateTime.UtcNow;
                    grade.UpdatedById = userId;
                    gradesRepaired++;
                    affectedOfferingIds.Add(grade.Assessment.CourseOfferingId);
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // 4. Include all offerings with results that exceeded 100 or where individual components are fractional or exceeded max
        var invalidResultsOfferings = await _dbContext.StudentCourseResults
            .Where(r => r.TotalScore > 100m ||
                        (r.Ca1Score != null && r.Ca1Score > 10m) ||
                        (r.Ca2Score != null && r.Ca2Score > 10m) ||
                        (r.Ca3Score != null && r.Ca3Score > 10m) ||
                        (r.ExamScore != null && r.ExamScore > 70m))
            .Select(r => r.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var id in invalidResultsOfferings)
        {
            affectedOfferingIds.Add(id);
        }

        // 5. Re-materialize student course results for all affected offerings
        int offeringsRematerialized = 0;
        foreach (var offeringId in affectedOfferingIds)
        {
            await MaterializeCourseResultsAsync(offeringId, userId, ct);
            offeringsRematerialized++;
        }

        var repairSummary = $"Grade repair completed successfully: {categoriesRepaired} categories updated, {assessmentsRepaired} assessments updated, {gradesRepaired} grades rescaled, {offeringsRematerialized} course offerings re-materialized.";
        await _auditService.LogAsync("RepairMigratedGrades", "Gradebook", "System", repairSummary, ct);
        return repairSummary;
    }

    #endregion

    #region Result Upload Aggregates Reporting

    public async Task<ErrorOr<ResultUploadAggregatesResponse>> GetResultUploadAggregatesAsync(
        ResultUploadAggregatesRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        // 1. Resolve Academic Session
        AcademicSession? session;
        if (request.AcademicSessionId.HasValue && request.AcademicSessionId.Value != Guid.Empty)
        {
            session = await _dbContext.AcademicSessions.FindAsync(new object[] { request.AcademicSessionId.Value }, ct);
        }
        else
        {
            session = await _dbContext.AcademicSessions
                .FirstOrDefaultAsync(s => s.IsActive, ct)
                ?? await _dbContext.AcademicSessions
                    .OrderByDescending(s => s.StartDate)
                    .FirstOrDefaultAsync(ct);
        }

        if (session == null)
        {
            return Error.NotFound("Session.NotFound", "No academic session could be resolved.");
        }

        // 2. Resolve Semester
        Data.Enums.Semester semester;
        if (request.Semester.HasValue && Enum.IsDefined(typeof(Data.Enums.Semester), request.Semester.Value))
        {
            semester = (Data.Enums.Semester)request.Semester.Value;
        }
        else
        {
            semester = session.ActiveSemester;
        }

        // 3. Query Course Offerings for this session and semester
        var offeringsQuery = _dbContext.CourseOfferings
            .AsNoTracking()
            .Include(co => co.Course)
                .ThenInclude(c => c.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(co => co.Course)
                .ThenInclude(c => c.Level)
            .Include(co => co.Programs)
                .ThenInclude(cop => cop.Program)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.Faculty)
            .Include(co => co.Programs)
                .ThenInclude(cop => cop.Level)
            .Include(co => co.Lecturers)
                .ThenInclude(l => l.Lecturer)
            .Where(co => co.AcademicSessionId == session.Id && co.Semester == semester);

        // Filter by Faculty / College
        if (request.FacultyId.HasValue && request.FacultyId.Value != Guid.Empty)
        {
            offeringsQuery = offeringsQuery.Where(co =>
                (co.Course.Program != null && co.Course.Program.Department != null && co.Course.Program.Department.FacultyId == request.FacultyId.Value) ||
                co.Programs.Any(cop => cop.Program != null && cop.Program.Department != null && cop.Program.Department.FacultyId == request.FacultyId.Value));
        }

        // Filter by Department
        if (request.DepartmentId.HasValue && request.DepartmentId.Value != Guid.Empty)
        {
            offeringsQuery = offeringsQuery.Where(co =>
                (co.Course.Program != null && co.Course.Program.DepartmentId == request.DepartmentId.Value) ||
                co.Programs.Any(cop => cop.Program != null && cop.Program.DepartmentId == request.DepartmentId.Value));
        }

        // Filter by Program
        if (request.ProgramId.HasValue && request.ProgramId.Value != Guid.Empty)
        {
            offeringsQuery = offeringsQuery.Where(co =>
                co.Course.ProgramId == request.ProgramId.Value ||
                co.Programs.Any(cop => cop.ProgramId == request.ProgramId.Value));
        }

        var offerings = await offeringsQuery.ToListAsync(ct);
        var offeringIds = offerings.Select(co => co.Id).ToList();
        var courseIds = offerings.Select(co => co.CourseId).Distinct().ToList();

        // 4. Bulk fetch related stats
        // A. Enrollments
        var allEnrollments = await _dbContext.CourseEnrollments
            .AsNoTracking()
            .Where(e => offeringIds.Contains(e.CourseOfferingId) && e.Status == "Registered" && e.DroppedAtUtc == null)
            .Select(e => new { e.CourseOfferingId, e.StudentId })
            .ToListAsync(ct);

        var enrollmentCounts = allEnrollments
            .GroupBy(e => e.CourseOfferingId)
            .ToDictionary(g => g.Key, g => g.Count());

        var distinctEnrolledStudentIds = allEnrollments
            .Select(e => e.StudentId)
            .Distinct()
            .ToHashSet();

        // B. Graded students from Grades table
        var gradedFromGrades = await _dbContext.Grades
            .AsNoTracking()
            .Where(g => g.Assessment != null && offeringIds.Contains(g.Assessment.CourseOfferingId))
            .GroupBy(g => g.Assessment.CourseOfferingId)
            .Select(g => new
            {
                OfferingId = g.Key,
                GradedCount = g.Select(x => x.StudentId).Distinct().Count(),
                GradedStudentIds = g.Select(x => x.StudentId).Distinct().ToList(),
                LastUpdated = g.Max(x => x.UpdatedAt),
                LastUpdatedById = g.OrderByDescending(x => x.UpdatedAt).Select(x => x.UpdatedById ?? x.CreatedById).FirstOrDefault()
            })
            .ToListAsync(ct);
        var gradesMap = gradedFromGrades.ToDictionary(g => g.OfferingId);

        // C. Graded students from StudentCourseResults
        var gradedFromResults = await _dbContext.StudentCourseResults
            .AsNoTracking()
            .Where(r => offeringIds.Contains(r.CourseOfferingId))
            .GroupBy(r => r.CourseOfferingId)
            .Select(g => new
            {
                OfferingId = g.Key,
                GradedCount = g.Count(),
                GradedStudentIds = g.Select(x => x.StudentId).Distinct().ToList(),
                HasPublished = g.Any(x => x.IsPublished),
                PublishedAt = g.Max(x => x.PublishedAt),
                PublishedById = g.OrderByDescending(x => x.PublishedAt).Select(x => x.PublishedById).FirstOrDefault()
            })
            .ToListAsync(ct);
        var resultsMap = gradedFromResults.ToDictionary(g => g.OfferingId);

        // D. Assessments presence
        var assessmentOfferingIds = (await _dbContext.Assessments
            .AsNoTracking()
            .Where(a => offeringIds.Contains(a.CourseOfferingId))
            .Select(a => a.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct))
            .ToHashSet();

        // E. Publications
        var publications = await _dbContext.GradePublications
            .AsNoTracking()
            .Include(p => p.PublishedBy)
            .Where(p => offeringIds.Contains(p.CourseOfferingId))
            .ToListAsync(ct);
        var publicationMap = publications
            .GroupBy(p => p.CourseOfferingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());

        // F. Approvals
        var approvals = await _dbContext.GradeApprovals
            .AsNoTracking()
            .Where(a => offeringIds.Contains(a.CourseOfferingId) && a.IsRequired)
            .ToListAsync(ct);
        var approvalMap = approvals
            .GroupBy(a => a.CourseOfferingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // G. Classter uploads
        var classterUploads = await _dbContext.ClassterResultUploads
            .AsNoTracking()
            .Include(u => u.CreatedBy)
            .Where(u => courseIds.Contains(u.CourseId) && u.AcademicSessionId == session.Id)
            .ToListAsync(ct);
        var classterUploadMap = classterUploads
            .GroupBy(u => u.CourseId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(u => u.CreatedAt).First());

        // H. Resolve user display names
        var userIdsToFetch = gradedFromGrades
            .Where(x => x.LastUpdatedById.HasValue)
            .Select(x => x.LastUpdatedById!.Value)
            .Union(gradedFromResults.Where(x => x.PublishedById.HasValue).Select(x => x.PublishedById!.Value))
            .Distinct()
            .ToList();
        var userNames = await _dbContext.Users
            .AsNoTracking()
            .Where(u => userIdsToFetch.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        // 5. Build Course Detail List
        var courseDetails = new List<CourseOfferingResultDetailDto>();

        foreach (var offering in offerings)
        {
            var enrolledCount = enrollmentCounts.GetValueOrDefault(offering.Id, 0);

            gradesMap.TryGetValue(offering.Id, out var gradeInfo);
            resultsMap.TryGetValue(offering.Id, out var resultInfo);

            var gradedCount = Math.Max(gradeInfo?.GradedCount ?? 0, resultInfo?.GradedCount ?? 0);
            var hasAssessments = assessmentOfferingIds.Contains(offering.Id);

            publicationMap.TryGetValue(offering.Id, out var pub);
            var isPublished = (pub != null && pub.IsVisibleToStudents) || (resultInfo?.HasPublished == true);

            approvalMap.TryGetValue(offering.Id, out var apps);
            apps ??= [];

            classterUploadMap.TryGetValue(offering.CourseId, out var classterUpload);

            // Determine status
            string status;
            if (isPublished)
            {
                status = "Published";
            }
            else if (apps.Any(a => a.Level == ApprovalLevel.Senate && a.Status == ApprovalStatus.Approved))
            {
                status = "Approved";
            }
            else if (apps.Any(a => a.Status == ApprovalStatus.Pending))
            {
                status = "InApproval";
            }
            else if (gradedCount > 0 || (classterUpload != null && classterUpload.Status == ClassterUploadStatus.Completed))
            {
                status = "Uploaded";
            }
            else
            {
                status = "Pending";
            }

            // Primary College, Department, Program
            var primaryProgram = offering.Course.Program ?? offering.Programs.FirstOrDefault()?.Program;
            var primaryDepartment = primaryProgram?.Department;
            var primaryFaculty = primaryDepartment?.Faculty;

            var primaryLevelName = offering.Programs.FirstOrDefault()?.Level?.Name
                ?? offering.Course.Level?.Name
                ?? "100 Level";

            var completionRate = enrolledCount > 0
                ? Math.Min(100.0, Math.Round(((double)gradedCount / enrolledCount) * 100.0, 1))
                : (gradedCount > 0 ? 100.0 : 0.0);

            // Last upload date & uploader
            DateTime? lastUploadDate = null;
            string? uploadedBy = null;

            if (isPublished && pub != null)
            {
                lastUploadDate = pub.PublishedAt;
                uploadedBy = pub.PublishedBy?.DisplayName;
            }
            else if (gradeInfo != null)
            {
                lastUploadDate = gradeInfo.LastUpdated;
                if (gradeInfo.LastUpdatedById.HasValue && userNames.TryGetValue(gradeInfo.LastUpdatedById.Value, out var uName))
                {
                    uploadedBy = uName;
                }
            }
            else if (classterUpload != null)
            {
                lastUploadDate = classterUpload.CompletedAt ?? classterUpload.CreatedAt;
                uploadedBy = classterUpload.CreatedBy?.DisplayName;
            }

            // Resolved lecturer names
            var lecturerNames = offering.Lecturers
                .Where(l => l.Lecturer != null && !string.IsNullOrWhiteSpace(l.Lecturer.DisplayName))
                .OrderBy(l => l.Role)
                .Select(l => l.Lecturer!.DisplayName)
                .OfType<string>()
                .ToList();

            // All programs associated with this offering
            var allProgramIds = new List<Guid>();
            if (offering.Course.ProgramId != Guid.Empty) allProgramIds.Add(offering.Course.ProgramId);
            allProgramIds.AddRange(offering.Programs.Select(p => p.ProgramId));
            allProgramIds = allProgramIds.Distinct().ToList();

            courseDetails.Add(new CourseOfferingResultDetailDto(
                OfferingId: offering.Id,
                CourseId: offering.CourseId,
                CourseCode: offering.Course.Code,
                CourseTitle: offering.Course.Title,
                CreditUnits: offering.Course.CreditUnits,
                CollegeId: primaryFaculty?.Id,
                CollegeName: primaryFaculty?.Name ?? "Unassigned College",
                DepartmentId: primaryDepartment?.Id,
                DepartmentName: primaryDepartment?.Name ?? "General",
                ProgramId: primaryProgram?.Id,
                ProgramName: primaryProgram?.Name ?? "General Program",
                ProgramIds: allProgramIds,
                LevelName: primaryLevelName,
                Semester: (int)offering.Semester,
                LecturerNames: lecturerNames,
                EnrolledCount: enrolledCount,
                GradedCount: gradedCount,
                CompletionRate: completionRate,
                Status: status,
                LastUploadDate: lastUploadDate,
                UploadedBy: uploadedBy,
                PublishedAt: pub?.PublishedAt ?? resultInfo?.PublishedAt,
                IsPublished: isPublished,
                HasAssessments: hasAssessments
            ));
        }

        // 6. Aggregate by College
        var collegeSummaries = courseDetails
            .GroupBy(c => new { Id = c.CollegeId ?? Guid.Empty, Name = c.CollegeName })
            .Select(g =>
            {
                var total = g.Count();
                var uploaded = g.Count(c => c.Status != "Pending");
                var pending = g.Count(c => c.Status == "Pending");
                var inApproval = g.Count(c => c.Status == "InApproval" || c.Status == "Approved");
                var published = g.Count(c => c.Status == "Published");
                
                var collegeOfferingIds = g.Select(c => c.OfferingId).ToHashSet();
                var collegeDistinctStudents = allEnrollments
                    .Where(e => collegeOfferingIds.Contains(e.CourseOfferingId))
                    .Select(e => e.StudentId)
                    .Distinct()
                    .Count();
                var collegeRegistrations = g.Sum(c => c.EnrolledCount);
                var collegeGraded = g.Sum(c => c.GradedCount);

                return new CollegeResultSummaryDto(
                    CollegeId: g.Key.Id,
                    CollegeName: g.Key.Name,
                    CollegeCode: g.Key.Name.Length >= 4 ? g.Key.Name[..4].ToUpper() : g.Key.Name.ToUpper(),
                    TotalOfferings: total,
                    UploadedOfferings: uploaded,
                    PendingOfferings: pending,
                    InApprovalOfferings: inApproval,
                    PublishedOfferings: published,
                    UploadPercentage: total > 0 ? Math.Round(((double)uploaded / total) * 100.0, 1) : 0.0,
                    TotalEnrolledStudents: collegeDistinctStudents,
                    TotalCourseRegistrations: collegeRegistrations,
                    TotalGradedStudents: collegeGraded,
                    GradingPercentage: collegeRegistrations > 0 ? Math.Round(((double)collegeGraded / collegeRegistrations) * 100.0, 1) : 0.0
                );
            })
            .OrderBy(c => c.CollegeName)
            .ToList();

        // 7. Aggregate by Program
        var programSummaries = courseDetails
            .GroupBy(c => new
            {
                Id = c.ProgramId ?? Guid.Empty,
                Name = c.ProgramName,
                DeptId = c.DepartmentId ?? Guid.Empty,
                DeptName = c.DepartmentName,
                CollegeId = c.CollegeId ?? Guid.Empty,
                CollegeName = c.CollegeName
            })
            .Select(g =>
            {
                var total = g.Count();
                var uploaded = g.Count(c => c.Status != "Pending");
                var pending = g.Count(c => c.Status == "Pending");
                var inApproval = g.Count(c => c.Status == "InApproval" || c.Status == "Approved");
                var published = g.Count(c => c.Status == "Published");
                
                var progOfferingIds = g.Select(c => c.OfferingId).ToHashSet();
                var progDistinctStudents = allEnrollments
                    .Where(e => progOfferingIds.Contains(e.CourseOfferingId))
                    .Select(e => e.StudentId)
                    .Distinct()
                    .Count();
                var progRegistrations = g.Sum(c => c.EnrolledCount);
                var progGraded = g.Sum(c => c.GradedCount);

                return new ProgramResultSummaryDto(
                    ProgramId: g.Key.Id,
                    ProgramName: g.Key.Name,
                    ProgramCode: g.Key.Name.Length >= 4 ? g.Key.Name[..4].ToUpper() : g.Key.Name.ToUpper(),
                    DepartmentId: g.Key.DeptId,
                    DepartmentName: g.Key.DeptName,
                    CollegeId: g.Key.CollegeId,
                    CollegeName: g.Key.CollegeName,
                    TotalOfferings: total,
                    UploadedOfferings: uploaded,
                    PendingOfferings: pending,
                    InApprovalOfferings: inApproval,
                    PublishedOfferings: published,
                    UploadPercentage: total > 0 ? Math.Round(((double)uploaded / total) * 100.0, 1) : 0.0,
                    TotalEnrolledStudents: progDistinctStudents,
                    TotalCourseRegistrations: progRegistrations,
                    TotalGradedStudents: progGraded
                );
            })
            .OrderBy(p => p.CollegeName)
            .ThenBy(p => p.ProgramName)
            .ToList();

        // 8. Overall Stats
        var totalOfferingsCount = courseDetails.Count;
        var uploadedOfferingsCount = courseDetails.Count(c => c.Status != "Pending");
        var pendingOfferingsCount = courseDetails.Count(c => c.Status == "Pending");
        var inApprovalOfferingsCount = courseDetails.Count(c => c.Status == "InApproval" || c.Status == "Approved");
        var publishedOfferingsCount = courseDetails.Count(c => c.Status == "Published");

        var distinctGradedStudentIds = gradedFromGrades
            .SelectMany(g => g.GradedStudentIds)
            .Union(gradedFromResults.SelectMany(r => r.GradedStudentIds))
            .Distinct()
            .ToHashSet();

        var distinctStudentsCount = distinctEnrolledStudentIds.Count;
        var totalCourseRegistrations = allEnrollments.Count;
        var totalGradedRegistrations = courseDetails.Sum(c => c.GradedCount);
        var distinctGradedCount = distinctGradedStudentIds.Count;

        var overallStats = new ResultUploadOverallStatsDto(
            TotalOfferings: totalOfferingsCount,
            UploadedOfferings: uploadedOfferingsCount,
            UploadedPercentage: totalOfferingsCount > 0 ? Math.Round(((double)uploadedOfferingsCount / totalOfferingsCount) * 100.0, 1) : 0.0,
            PendingOfferings: pendingOfferingsCount,
            PendingPercentage: totalOfferingsCount > 0 ? Math.Round(((double)pendingOfferingsCount / totalOfferingsCount) * 100.0, 1) : 0.0,
            InApprovalOfferings: inApprovalOfferingsCount,
            PublishedOfferings: publishedOfferingsCount,
            PublishedPercentage: totalOfferingsCount > 0 ? Math.Round(((double)publishedOfferingsCount / totalOfferingsCount) * 100.0, 1) : 0.0,
            TotalEnrolledStudents: distinctStudentsCount,
            TotalCourseRegistrations: totalCourseRegistrations,
            TotalGradedStudents: totalGradedRegistrations,
            DistinctGradedStudents: distinctGradedCount,
            GradingCompletionPercentage: totalCourseRegistrations > 0 ? Math.Round(((double)totalGradedRegistrations / totalCourseRegistrations) * 100.0, 1) : 0.0
        );

        // 9. Apply in-memory filtering for Course Details list if requested
        var filteredCourses = courseDetails.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var reqStatus = request.Status.Trim().ToLowerInvariant();
            if (reqStatus == "pending")
            {
                filteredCourses = filteredCourses.Where(c => c.Status == "Pending");
            }
            else if (reqStatus == "uploaded")
            {
                filteredCourses = filteredCourses.Where(c => c.Status == "Uploaded");
            }
            else if (reqStatus == "inapproval" || reqStatus == "in_approval" || reqStatus == "approval")
            {
                filteredCourses = filteredCourses.Where(c => c.Status == "InApproval" || c.Status == "Approved");
            }
            else if (reqStatus == "published")
            {
                filteredCourses = filteredCourses.Where(c => c.Status == "Published");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            filteredCourses = filteredCourses.Where(c =>
                c.CourseCode.ToLowerInvariant().Contains(term) ||
                c.CourseTitle.ToLowerInvariant().Contains(term) ||
                c.LecturerNames.Any(l => l.ToLowerInvariant().Contains(term)) ||
                c.ProgramName.ToLowerInvariant().Contains(term) ||
                c.CollegeName.ToLowerInvariant().Contains(term));
        }

        var response = new ResultUploadAggregatesResponse(
            Session: new ResultUploadSessionDto(session.Id, session.Name, session.IsActive, (int)session.ActiveSemester),
            Semester: (int)semester,
            OverallStats: overallStats,
            Colleges: collegeSummaries,
            Programs: programSummaries,
            Courses: filteredCourses.OrderBy(c => c.CourseCode).ToList()
        );

        return response;
    }

    #endregion
}
