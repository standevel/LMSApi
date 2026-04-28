using System;
using LMS.Api.Data.Entities;

namespace LMS.Api.Contracts;

// ==================== SYSTEM CONFIGURATION ====================

public record SystemGradingConfigurationDto(
    Guid Id,
    GradingStyle DefaultGradingStyle,
    decimal DefaultExamPercentage,
    bool ApprovalWorkflowEnabled,
    decimal DefaultCA1Weight,
    decimal DefaultCA2Weight,
    decimal DefaultCA3Weight,
    decimal DefaultExamWeight,
    DateTime UpdatedAt);

public record UpdateSystemGradingConfigurationRequest(
    GradingStyle? DefaultGradingStyle,
    decimal? DefaultExamPercentage,
    bool? ApprovalWorkflowEnabled,
    decimal? DefaultCA1Weight,
    decimal? DefaultCA2Weight,
    decimal? DefaultCA3Weight,
    decimal? DefaultExamWeight);

// ==================== ASSESSMENT CATEGORIES ====================

public record AssessmentCategoryDto(
    Guid Id,
    AssessmentCategoryType CategoryType,
    string CategoryName,
    decimal Weight,
    decimal MaxMarks,
    bool IsExamCategory,
    int DisplayOrder);

public record CreateAssessmentCategoryRequest(
    AssessmentCategoryType CategoryType,
    string CategoryName,
    decimal Weight,
    decimal MaxMarks,
    bool IsExamCategory,
    int DisplayOrder);

// ==================== ASSESSMENTS ====================

public record AssessmentDto(
    Guid Id,
    Guid AssessmentCategoryId,
    string CategoryName,
    string Title,
    string? Description,
    decimal MaxMarks,
    DateTime? AssessmentDate,
    DateTime? DueDate,
    int GradesCount);

public record CreateAssessmentRequest(
    Guid AssessmentCategoryId,
    string Title,
    string? Description,
    decimal MaxMarks,
    DateTime? AssessmentDate,
    DateTime? DueDate);

public record UpdateAssessmentRequest(
    string? Title,
    string? Description,
    decimal? MaxMarks,
    DateTime? AssessmentDate,
    DateTime? DueDate);

// ==================== GRADES ====================

public record GradeDto(
    Guid Id,
    Guid AssessmentId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    decimal MarksObtained,
    decimal MaxMarks,
    decimal Percentage,
    bool IsLocked,
    string? Remarks,
    DateTime UpdatedAt);

public record StudentGradeSummaryDto(
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    decimal CA1Score,
    decimal CA2Score,
    decimal CA3Score,
    decimal ExamScore,
    decimal TotalScore,
    string LetterGrade,
    string? Remarks);

public record EnterGradeRequest(
    Guid AssessmentId,
    Guid StudentId,
    decimal MarksObtained,
    string? Remarks);

public record BulkEnterGradesRequest(
    List<EnterGradeRequest> Grades);

// ==================== GRADEBOOK SUMMARY ====================

public record GradebookSummaryDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    string ProgramName,
    string LevelName,
    string AcademicSessionName,
    int Semester,
    List<AssessmentCategoryDto> Categories,
    List<AssessmentDto> Assessments,
    int TotalStudents,
    int GradesEntered,
    bool IsPublished,
    bool ApprovalWorkflowCompleted,
    List<GradeApprovalDto> Approvals);

// ==================== APPROVAL WORKFLOW ====================

public record GradeApprovalDto(
    Guid Id,
    ApprovalLevel Level,
    ApprovalStatus Status,
    Guid? ApprovedById,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? Comments,
    bool IsRequired,
    int ApprovalOrder);

public record SubmitForApprovalRequest(
    string? Comments);

public record ApproveGradesRequest(
    ApprovalLevel Level,
    string? Comments);

public record RejectGradesRequest(
    ApprovalLevel Level,
    string Comments);

// ==================== PUBLICATION ====================

public record GradePublicationDto(
    Guid Id,
    DateTime PublishedAt,
    Guid PublishedById,
    string PublishedByName,
    bool IsVisibleToStudents,
    bool ApprovalWorkflowCompleted,
    string? PublicationNotes);

public record PublishGradesRequest(
    string? PublicationNotes);

// ==================== EXCEL ====================

public record GradebookExcelTemplateDto(
    byte[] FileContent,
    string FileName,
    string ContentType);

public record GradeUploadResultDto(
    int TotalRecords,
    int SuccessfulUploads,
    int FailedUploads,
    List<string> Errors);

// ==================== STUDENT VIEW ====================

public record StudentGradeViewDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    string AcademicSessionName,
    int Semester,
    List<StudentAssessmentGradeDto> AssessmentGrades,
    decimal TotalScore,
    string LetterGrade,
    string? Remarks,
    bool IsPublished);

public record StudentAssessmentGradeDto(
    string CategoryName,
    string AssessmentTitle,
    decimal MarksObtained,
    decimal MaxMarks,
    decimal Weight,
    decimal WeightedScore);
