using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AssignmentService(
    LmsDbContext context,
    INotificationService notificationService) : IAssignmentService
{
    public async Task<ErrorOr<AssignmentDto>> CreateAssignmentAsync(UpsertAssignmentRequest request, Guid creatorId, CancellationToken ct = default)
    {
        var validation = ValidateAssignment(request);
        if (validation is not null) return validation.Value;
        var programValidation = await ValidateTargetProgramsAsync(request.CourseId, request.TargetProgramIds, ct);
        if (programValidation is not null) return programValidation.Value;

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var assignment = new Assignment();
        Apply(assignment, request);
        context.Assignments.Add(assignment);
        await context.SaveChangesAsync(ct);

        // Get enrolled students
        var targetProgramIds = NormalizeTargetProgramIds(request.TargetProgramIds);
        var enrolledStudentIds = await context.CourseEnrollments
            .AsNoTracking()
            .Where(e =>
                e.CourseOffering.CourseId == request.CourseId &&
                e.Status == "Registered" &&
                (targetProgramIds.Count == 0 || targetProgramIds.Contains(e.CourseOffering.ProgramId)))
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(ct);

        // Fetch course details for notification
        var course = await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, ct);
            
        var courseCode = course?.Code ?? "Course";

        foreach (var studentId in enrolledStudentIds)
        {
            await notificationService.CreateAsync(new CreateNotificationRequest(
                studentId,
                creatorId,
                $"New Assignment: {request.Title}",
                $"A new assignment has been created for {courseCode}. Due date: {request.DueDate:f}",
                "System",
                $"/courses/{request.CourseId}/assignments/{assignment.Id}"
            ), ct);
        }

        await tx.CommitAsync(ct);
        return ToDto(assignment);
    }

    public async Task<ErrorOr<AssignmentDto>> UpdateAssignmentAsync(Guid id, UpsertAssignmentRequest request, CancellationToken ct = default)
    {
        var validation = ValidateAssignment(request);
        if (validation is not null) return validation.Value;
        var programValidation = await ValidateTargetProgramsAsync(request.CourseId, request.TargetProgramIds, ct);
        if (programValidation is not null) return programValidation.Value;

        var assignment = await context.Assignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        Apply(assignment, request);
        assignment.Version++;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(assignment);
    }

    public async Task<ErrorOr<Deleted>> DeleteAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        assignment.IsDeleted = true;
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        assignment.Version++;
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<AssignmentDto>>> GetAssignmentsAsync(Guid? courseId, Guid? currentUserId = null, bool restrictToStudentEnrollments = false, CancellationToken ct = default)
    {
        var query = context.Assignments.AsNoTracking();
        if (courseId.HasValue) query = query.Where(x => x.CourseId == courseId.Value);
        if (restrictToStudentEnrollments)
        {
            if (!currentUserId.HasValue) return Error.Unauthorized("Assignment.Unauthorized", "User is not authenticated.");

            var enrollments = await context.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.StudentId == currentUserId.Value && e.Status == "Registered")
                .Select(e => new
                {
                    e.CourseOffering.CourseId,
                    e.CourseOffering.ProgramId
                })
                .ToListAsync(ct);

            var enrolledCourseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
            var assignments = await query
                .Where(x => enrolledCourseIds.Contains(x.CourseId))
                .OrderBy(x => x.DueDate)
                .ToListAsync(ct);

            return assignments
                .Where(assignment => StudentCanAccessAssignmentProgram(assignment, enrollments.Select(e => (e.CourseId, e.ProgramId))))
                .Select(ToDto)
                .ToList();
        }
        return await query.OrderBy(x => x.DueDate).Select(x => ToDto(x)).ToListAsync(ct);
    }

    public async Task<ErrorOr<AssignmentDto>> GetAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return assignment is null ? Error.NotFound("Assignment.NotFound", "Assignment not found.") : ToDto(assignment);
    }

    public async Task<ErrorOr<AssignmentSubmissionDto>> SubmitAsync(SubmitAssignmentRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.FirstOrDefaultAsync(x => x.Id == request.AssignmentId, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");

        var canAccessAssignment = await context.CourseEnrollments
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.StudentId == currentUserId &&
                enrollment.Status == "Registered" &&
                enrollment.CourseOffering.CourseId == assignment.CourseId)
            .Select(enrollment => new
            {
                enrollment.CourseOffering.CourseId,
                enrollment.CourseOffering.ProgramId
            })
            .ToListAsync(ct);

        if (!StudentCanAccessAssignmentProgram(assignment, canAccessAssignment.Select(e => (e.CourseId, e.ProgramId))))
        {
            return Error.Forbidden("Assignment.Forbidden", "This assignment is not available to your program.");
        }

        var now = DateTimeOffset.UtcNow;
        if (!request.SaveAsDraft && assignment.CutoffDate.HasValue && now > assignment.CutoffDate.Value)
        {
            return Error.Validation("Assignment.CutoffExceeded", "The cutoff date has passed for this assignment.");
        }

        var submitterId = currentUserId;
        await using var tx = await context.Database.BeginTransactionAsync(ct);

        var submission = await context.AssignmentSubmissions
            .FirstOrDefaultAsync(x => x.AssignmentId == request.AssignmentId && x.SubmitterId == submitterId, ct);

        if (submission is null)
        {
            submission = new AssignmentSubmission
            {
                AssignmentId = request.AssignmentId,
                SubmitterId = submitterId
            };
            context.AssignmentSubmissions.Add(submission);
        }

        submission.SubmissionMetadataJson = string.IsNullOrWhiteSpace(request.SubmissionMetadataJson) ? "{}" : request.SubmissionMetadataJson;
        submission.Status = request.SaveAsDraft
            ? AssignmentSubmissionStatus.Draft
            : now > assignment.DueDate ? AssignmentSubmissionStatus.Late : AssignmentSubmissionStatus.Submitted;
        submission.SubmittedAt = request.SaveAsDraft ? null : now;
        submission.DigitalReceipt = request.SaveAsDraft ? string.Empty : BuildReceipt(submission.SubmissionMetadataJson, now);
        submission.UpdatedAt = now;
        submission.Version++;

        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(submission);
    }

    public async Task<ErrorOr<List<AssignmentSubmissionDto>>> GetSubmissionsAsync(Guid assignmentId, Guid? submitterId, CancellationToken ct = default)
    {
        var query = context.AssignmentSubmissions.AsNoTracking()
            .Include(x => x.Grade)
            .Where(x => x.AssignmentId == assignmentId);
        if (submitterId.HasValue) query = query.Where(x => x.SubmitterId == submitterId.Value);
        return await query.OrderByDescending(x => x.SubmittedAt ?? x.UpdatedAt).Select(x => ToDto(x)).ToListAsync(ct);
    }

    public async Task<ErrorOr<AssignmentSubmissionDto>> GradeAsync(GradeSubmissionRequest request, Guid graderId, CancellationToken ct = default)
    {
        var submission = await context.AssignmentSubmissions
            .Include(x => x.Assignment)
            .Include(x => x.Grade)
            .FirstOrDefaultAsync(x => x.Id == request.SubmissionId, ct);
        if (submission is null) return Error.NotFound("Submission.NotFound", "Submission not found.");
        if (request.Score < 0 || request.Score > submission.Assignment.MaxPoints)
        {
            return Error.Validation("Grade.InvalidScore", $"Score must be between 0 and {submission.Assignment.MaxPoints}.");
        }

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var grade = submission.Grade ?? new SubmissionGrade { SubmissionId = submission.Id };
        grade.GraderId = graderId;
        grade.Score = request.Score;
        grade.FeedbackText = request.FeedbackText;
        grade.FeedbackMediaUrl = request.FeedbackMediaUrl;
        grade.RubricExecutionJson = string.IsNullOrWhiteSpace(request.RubricExecutionJson) ? "{}" : request.RubricExecutionJson;
        grade.GradedAt = now;
        grade.UpdatedAt = now;
        grade.Version++;
        if (submission.Grade is null) context.SubmissionGrades.Add(grade);

        submission.Status = AssignmentSubmissionStatus.Graded;
        submission.UpdatedAt = now;
        submission.Version++;
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        submission.Grade = grade;
        return ToDto(submission);
    }

    private static Error? ValidateAssignment(UpsertAssignmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Error.Validation("Assignment.TitleRequired", "Title is required.");
        if (request.Title.Length > 200) return Error.Validation("Assignment.TitleTooLong", "Title cannot exceed 200 characters.");
        if (request.CourseId == Guid.Empty) return Error.Validation("Assignment.CourseRequired", "Course is required.");
        if (request.MaxPoints <= 0) return Error.Validation("Assignment.InvalidPoints", "Max points must be greater than zero.");
        if (request.MaxFileSizeMb <= 0) return Error.Validation("Assignment.InvalidFileSize", "Maximum file size must be greater than zero.");
        if (request.CutoffDate.HasValue && request.CutoffDate.Value < request.DueDate)
        {
            return Error.Validation("Assignment.InvalidCutoff", "Cutoff date must be on or after due date.");
        }
        return null;
    }

    private async Task<Error?> ValidateTargetProgramsAsync(Guid courseId, IEnumerable<Guid>? programIds, CancellationToken ct)
    {
        var ids = NormalizeTargetProgramIds(programIds);
        if (ids.Count == 0) return null;

        var validProgramIds = await context.CourseOfferings
            .AsNoTracking()
            .Where(offering => offering.CourseId == courseId && ids.Contains(offering.ProgramId))
            .Select(offering => offering.ProgramId)
            .Distinct()
            .ToListAsync(ct);

        return validProgramIds.Count == ids.Count
            ? null
            : Error.Validation("Assignment.InvalidPrograms", "One or more selected programs do not offer this course.");
    }

    private static void Apply(Assignment assignment, UpsertAssignmentRequest request)
    {
        assignment.Title = request.Title.Trim();
        assignment.Description = request.Description;
        assignment.MaxPoints = request.MaxPoints;
        assignment.CourseId = request.CourseId;
        assignment.AssessmentCategoryId = request.AssessmentCategoryId;
        assignment.DueDate = request.DueDate.ToUniversalTime();
        assignment.CutoffDate = request.CutoffDate?.ToUniversalTime();
        assignment.AllowedExtensions = string.IsNullOrWhiteSpace(request.AllowedExtensions) ? "pdf,docx,zip" : request.AllowedExtensions.Trim().ToLowerInvariant();
        assignment.MaxFileSizeMb = request.MaxFileSizeMb;
        assignment.IsGroupAssignment = request.IsGroupAssignment;
        assignment.ReleaseConditionsJson = string.IsNullOrWhiteSpace(request.ReleaseConditionsJson) ? "{}" : request.ReleaseConditionsJson;
        assignment.TargetProgramIdsJson = JsonSerializer.Serialize(NormalizeTargetProgramIds(request.TargetProgramIds));
    }

    private static List<Guid> NormalizeTargetProgramIds(IEnumerable<Guid>? programIds) =>
        (programIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    private static bool StudentCanAccessAssignmentProgram(Assignment assignment, IEnumerable<(Guid CourseId, Guid ProgramId)> enrollments)
    {
        var targetProgramIds = DeserializeTargetProgramIds(assignment.TargetProgramIdsJson);
        return enrollments.Any(enrollment =>
            enrollment.CourseId == assignment.CourseId &&
            (targetProgramIds.Count == 0 || targetProgramIds.Contains(enrollment.ProgramId)));
    }

    private static List<Guid> DeserializeTargetProgramIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }

    private static string BuildReceipt(string payload, DateTimeOffset timestamp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{payload}|{timestamp:O}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static AssignmentDto ToDto(Assignment x) => new(x.Id, x.Title, x.Description, x.MaxPoints, x.CourseId, x.AssessmentCategoryId, x.DueDate, x.CutoffDate, x.AllowedExtensions, x.MaxFileSizeMb, x.IsGroupAssignment, x.ReleaseConditionsJson, DeserializeTargetProgramIds(x.TargetProgramIdsJson), x.CreatedAt, x.UpdatedAt);

    private static AssignmentSubmissionDto ToDto(AssignmentSubmission x) => new(x.Id, x.AssignmentId, x.SubmitterId, x.Status.ToString(), x.SubmittedAt, x.SubmissionMetadataJson, x.DigitalReceipt, x.Grade is null ? null : ToDto(x.Grade), x.CreatedAt, x.UpdatedAt);

    private static SubmissionGradeDto ToDto(SubmissionGrade x) => new(x.Id, x.SubmissionId, x.GraderId, x.Score, x.FeedbackText, x.FeedbackMediaUrl, x.RubricExecutionJson, x.GradedAt);
}
