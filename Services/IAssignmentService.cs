namespace LMS.Api.Services;

public interface IAssignmentService
{
    Task<ErrorOr<AssignmentDto>> CreateAssignmentAsync(UpsertAssignmentRequest request, Guid creatorId, CancellationToken ct = default);
    Task<ErrorOr<AssignmentDto>> UpdateAssignmentAsync(Guid id, UpsertAssignmentRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAssignmentAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<AssignmentDto>>> GetAssignmentsAsync(Guid? courseOfferingId, Guid? currentUserId = null, bool restrictToStudentEnrollments = false, CancellationToken ct = default);
    Task<ErrorOr<AssignmentDto>> GetAssignmentAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<AssignmentSubmissionDto>> SubmitAsync(SubmitAssignmentRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<ErrorOr<List<AssignmentSubmissionDto>>> GetSubmissionsAsync(Guid assignmentId, Guid? submitterId, CancellationToken ct = default);
    Task<ErrorOr<AssignmentSubmissionDto>> GradeAsync(GradeSubmissionRequest request, Guid graderId, CancellationToken ct = default);
    Task<ErrorOr<int>> ImportAssignmentsFromOfferingAsync(Guid sourceOfferingId, Guid targetOfferingId, Guid userId, CancellationToken ct = default);
}
