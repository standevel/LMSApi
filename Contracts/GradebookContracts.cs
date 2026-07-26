using System;
using LMS.Api.Data.Entities;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

// ==================== SYSTEM CONFIGURATION ====================

public record SystemGradingConfigurationDto(
    Guid Id,
    string DefaultGradingStyle,
    decimal DefaultExamPercentage,
    bool ApprovalWorkflowEnabled,
    decimal DefaultCA1Weight,
    decimal DefaultCA2Weight,
    decimal DefaultCA3Weight,
    decimal DefaultExamWeight,
    decimal GpaScale,
    List<GradeMappingDto> LetterGradesMapping,
    string RoundingStrategy,
    int RoundingDecimalPlaces,
    decimal GraceThreshold,
    DateTime UpdatedAt);

public record UpdateSystemGradingConfigurationRequest(
    string? DefaultGradingStyle,
    decimal? DefaultExamPercentage,
    bool? ApprovalWorkflowEnabled,
    decimal? DefaultCA1Weight,
    decimal? DefaultCA2Weight,
    decimal? DefaultCA3Weight,
    decimal? DefaultExamWeight,
    decimal? GpaScale,
    List<GradeMappingDto>? LetterGradesMapping,
    string? RoundingStrategy,
    int? RoundingDecimalPlaces,
    decimal? GraceThreshold);

public record GradeMappingDto(decimal MinPercentage, string LetterGrade, decimal GradePoints);

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
    Guid? AssessmentCategoryId,
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
    string MatricNumber,
    string StudentName,
    string StudentEmail,
    decimal Ca1Score,
    decimal Ca2Score,
    decimal Ca3Score,
    decimal ExamScore,
    decimal TotalScore,
    string LetterGrade,
    string? Remarks);

public record GradeDistributionDto(string LetterGrade, int Count);

public record UpdateStudentGradeSummaryItem(
    Guid StudentId,
    decimal? Ca1Score,
    decimal? Ca2Score,
    decimal? Ca3Score,
    decimal? ExamScore);

public record UpdateStudentGradeSummaryRequest(
    List<UpdateStudentGradeSummaryItem> Grades);

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
    Guid UploadId,
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

// ==================== COURSE LISTING (Course Selector) ====================

public record CourseOfferingSummaryDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    string ProgramName,
    string LevelName,
    string AcademicSessionName,
    int Semester,
    bool IsPublished,
    string? LecturerName,
    bool IsSessionActive);

// ==================== BULK PUBLICATION ====================

public record BulkPublishGradesRequest(
    Guid? AcademicSessionId,
    int? Semester,
    Guid? FacultyId,
    Guid? DepartmentId,
    Guid? ProgramId,
    string? PublicationNotes,
    bool ForcePublish = false);

public record BulkPublishResultDto(
    int TotalProcessed,
    int TotalPublished,
    int TotalSkipped,
    List<BulkPublishDetailDto> Details);

public record BulkPublishDetailDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    bool Succeeded,
    string Message);

public record BulkUnpublishGradesRequest(
    Guid? AcademicSessionId,
    int? Semester,
    Guid? FacultyId,
    Guid? DepartmentId,
    Guid? ProgramId,
    string? UnpublicationNotes);

public record BulkUnpublishResultDto(
    int TotalProcessed,
    int TotalUnpublished,
    int TotalSkipped,
    List<BulkUnpublishDetailDto> Details);

public record BulkUnpublishDetailDto(
    Guid CourseOfferingId,
    string CourseCode,
    string CourseTitle,
    bool Succeeded,
    string Message);

