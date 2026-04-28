using ErrorOr;
using LMS.Api.Contracts;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Services;

public interface IGradebookService
{
    // System Configuration
    Task<ErrorOr<SystemGradingConfigurationDto>> GetSystemConfigurationAsync(CancellationToken ct = default);
    Task<ErrorOr<SystemGradingConfigurationDto>> UpdateSystemConfigurationAsync(UpdateSystemGradingConfigurationRequest request, Guid userId, CancellationToken ct = default);
    
    // Assessment Categories
    Task<ErrorOr<List<AssessmentCategoryDto>>> GetAssessmentCategoriesAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<AssessmentCategoryDto>> CreateAssessmentCategoryAsync(Guid courseOfferingId, CreateAssessmentCategoryRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAssessmentCategoryAsync(Guid categoryId, CancellationToken ct = default);
    
    // Assessments
    Task<ErrorOr<List<AssessmentDto>>> GetAssessmentsAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<AssessmentDto>> CreateAssessmentAsync(Guid courseOfferingId, CreateAssessmentRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<AssessmentDto>> UpdateAssessmentAsync(Guid assessmentId, UpdateAssessmentRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAssessmentAsync(Guid assessmentId, CancellationToken ct = default);
    
    // Grades
    Task<ErrorOr<List<GradeDto>>> GetGradesByAssessmentAsync(Guid assessmentId, CancellationToken ct = default);
    Task<ErrorOr<List<StudentGradeSummaryDto>>> GetStudentGradeSummariesAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<GradeDto>> EnterGradeAsync(EnterGradeRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<GradeUploadResultDto>> BulkUploadGradesAsync(Guid courseOfferingId, IFormFile excelFile, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<GradebookExcelTemplateDto>> GenerateExcelTemplateAsync(Guid courseOfferingId, CancellationToken ct = default);
    
    // Gradebook Summary
    Task<ErrorOr<GradebookSummaryDto>> GetGradebookSummaryAsync(Guid courseOfferingId, Guid? userId, CancellationToken ct = default);
    
    // Approval Workflow
    Task<ErrorOr<List<GradeApprovalDto>>> GetGradeApprovalsAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<GradeApprovalDto>> SubmitForApprovalAsync(Guid courseOfferingId, SubmitForApprovalRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<GradeApprovalDto>> ApproveGradesAsync(Guid courseOfferingId, ApproveGradesRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<GradeApprovalDto>> RejectGradesAsync(Guid courseOfferingId, RejectGradesRequest request, Guid userId, CancellationToken ct = default);
    
    // Publication
    Task<ErrorOr<GradePublicationDto>> GetPublicationStatusAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<GradePublicationDto>> PublishGradesAsync(Guid courseOfferingId, PublishGradesRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UnpublishGradesAsync(Guid courseOfferingId, Guid userId, CancellationToken ct = default);
    
    // Student View
    Task<ErrorOr<StudentGradeViewDto>> GetStudentGradesAsync(Guid courseOfferingId, Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<StudentGradeViewDto>>> GetStudentAllGradesAsync(Guid studentId, CancellationToken ct = default);
}
