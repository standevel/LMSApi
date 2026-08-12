using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AssignmentService(
    LmsDbContext context,
    INotificationService notificationService,
    IAssignmentGroupService groupService,
    ITurnitinService turnitinService) : IAssignmentService
{
    public async Task<ErrorOr<AssignmentDto>> CreateAssignmentAsync(UpsertAssignmentRequest request, Guid creatorId, CancellationToken ct = default)
    {
        var validation = ValidateAssignment(request);
        if (validation is not null) return validation.Value;
        var programValidation = await ValidateTargetProgramsAsync(request.CourseOfferingId, request.TargetProgramIds, ct);
        if (programValidation is not null) return programValidation.Value;

        var assignment = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(ct);
            var a = new Assignment();
            Apply(a, request);
            context.Assignments.Add(a);
            await context.SaveChangesAsync(ct);

            // Get enrolled students
            var targetProgramIds = NormalizeTargetProgramIds(request.TargetProgramIds);
            var enrolledStudentIds = await context.CourseEnrollments
                .AsNoTracking()
                .Where(e =>
                    e.CourseOfferingId == request.CourseOfferingId &&
                    e.Status == "Registered" &&
                    (targetProgramIds.Count == 0 || context.CourseOfferingPrograms.Any(p =>
                        p.CourseOfferingId == e.CourseOfferingId &&
                        targetProgramIds.Contains(p.ProgramId))))
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(ct);

            // Fetch course details for notification
            var course = await context.CourseOfferings
                .AsNoTracking()
                .Include(co => co.Course)
                .Where(co => co.Id == request.CourseOfferingId)
                .Select(co => co.Course)
                .FirstOrDefaultAsync(ct);
                
            var courseCode = course?.Code ?? "Course";

            foreach (var studentId in enrolledStudentIds)
            {
                await notificationService.CreateAsync(new CreateNotificationRequest(
                    studentId,
                    creatorId,
                    $"New Assignment: {request.Title}",
                    $"A new assignment has been created for {courseCode}. Due date: {request.DueDate:f}",
                    "System",
                    $"/courses/{request.CourseOfferingId}/assignments/{a.Id}"
                ), ct);
            }

            await tx.CommitAsync(ct);
            return a;
        });

        return ToDto(assignment);
    }

    public async Task<ErrorOr<AssignmentDto>> UpdateAssignmentAsync(Guid id, UpsertAssignmentRequest request, CancellationToken ct = default)
    {
        var validation = ValidateAssignment(request);
        if (validation is not null) return validation.Value;
        var programValidation = await ValidateTargetProgramsAsync(request.CourseOfferingId, request.TargetProgramIds, ct);
        if (programValidation is not null) return programValidation.Value;

        var assignment = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var a = await context.Assignments.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return null;

            await using var tx = await context.Database.BeginTransactionAsync(ct);
            Apply(a, request);
            a.Version++;
            a.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return a;
        });

        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");
        return ToDto(assignment);
    }

    public async Task<ErrorOr<Deleted>> DeleteAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var deleted = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var a = await context.Assignments.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return false;

            await using var tx = await context.Database.BeginTransactionAsync(ct);
            a.IsDeleted = true;
            a.UpdatedAt = DateTimeOffset.UtcNow;
            a.Version++;
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        });

        if (!deleted) return Error.NotFound("Assignment.NotFound", "Assignment not found.");
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<AssignmentDto>>> GetAssignmentsAsync(Guid? courseOfferingId, Guid? currentUserId = null, bool restrictToStudentEnrollments = false, CancellationToken ct = default)
    {
        var query = context.Assignments.AsNoTracking();
        if (courseOfferingId.HasValue) query = query.Where(x => x.CourseOfferingId == courseOfferingId.Value);
        if (restrictToStudentEnrollments)
        {
            if (!currentUserId.HasValue) return Error.Unauthorized("Assignment.Unauthorized", "User is not authenticated.");

            var enrollments = await context.CourseEnrollments
                .AsNoTracking()
                .Where(e => e.StudentId == currentUserId.Value && e.Status == "Registered")
                .Select(e => new
                {
                    e.CourseOfferingId,
                    ProgramId = context.CourseOfferingPrograms
                        .Where(p => p.CourseOfferingId == e.CourseOfferingId)
                        .Select(p => p.ProgramId)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var enrolledOfferingIds = enrollments.Select(e => e.CourseOfferingId).Distinct().ToList();
            var assignments = await query
                .Where(x => enrolledOfferingIds.Contains(x.CourseOfferingId))
                .OrderBy(x => x.DueDate)
                .ToListAsync(ct);

            return assignments
                .Where(assignment => StudentCanAccessAssignmentProgram(assignment, enrollments.Select(e => (e.CourseOfferingId, e.ProgramId))))
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
                enrollment.CourseOfferingId == assignment.CourseOfferingId)
            .Select(enrollment => new
            {
                enrollment.CourseOfferingId,
                ProgramId = context.CourseOfferingPrograms
                    .Where(p => p.CourseOfferingId == enrollment.CourseOfferingId)
                    .Select(p => p.ProgramId)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        if (!StudentCanAccessAssignmentProgram(assignment, canAccessAssignment.Select(e => (e.CourseOfferingId, e.ProgramId))))
        {
            return Error.Forbidden("Assignment.Forbidden", "This assignment is not available to your program.");
        }

        var now = DateTimeOffset.UtcNow;
        if (!request.SaveAsDraft && assignment.CutoffDate.HasValue && now > assignment.CutoffDate.Value)
        {
            return Error.Validation("Assignment.CutoffExceeded", "The cutoff date has passed for this assignment.");
        }

        var submitterId = currentUserId;
        var submission = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(ct);

            var s = await context.AssignmentSubmissions
                .FirstOrDefaultAsync(x => x.AssignmentId == request.AssignmentId && x.SubmitterId == submitterId, ct);

            if (s is null)
            {
                s = new AssignmentSubmission
                {
                    AssignmentId = request.AssignmentId,
                    SubmitterId = submitterId
                };
                context.AssignmentSubmissions.Add(s);
            }

            s.SubmissionMetadataJson = string.IsNullOrWhiteSpace(request.SubmissionMetadataJson) ? "{}" : request.SubmissionMetadataJson;
            s.Status = request.SaveAsDraft
                ? AssignmentSubmissionStatus.Draft
                : now > assignment.DueDate ? AssignmentSubmissionStatus.Late : AssignmentSubmissionStatus.Submitted;
            s.SubmittedAt = request.SaveAsDraft ? null : now;
            s.DigitalReceipt = request.SaveAsDraft ? string.Empty : BuildReceipt(s.SubmissionMetadataJson, now);
            s.UpdatedAt = now;
            s.Version++;

            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return s;
        });

        // Trigger Turnitin plagiarism check for final (non-draft) submissions
        if (!request.SaveAsDraft)
        {
            try
            {
                await turnitinService.CheckSubmissionAsync(submission.Id, ct);
                // Reload updated Turnitin properties on submission
                submission = await context.AssignmentSubmissions
                    .Include(x => x.Grade)
                    .FirstOrDefaultAsync(x => x.Id == submission.Id, ct) ?? submission;
            }
            catch (Exception ex)
            {
                // Log and continue gracefully if Turnitin service encounters external API timeout
            }
        }

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

        var gradedSubmission = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var s = await context.AssignmentSubmissions
                .Include(x => x.Assignment)
                .Include(x => x.Grade)
                .FirstOrDefaultAsync(x => x.Id == request.SubmissionId, ct);
            
            if (s is null) return null;

            await using var tx = await context.Database.BeginTransactionAsync(ct);
            var now = DateTimeOffset.UtcNow;
            var grade = s.Grade ?? new SubmissionGrade { SubmissionId = s.Id };
            grade.GraderId = graderId;
            grade.Score = request.Score;
            grade.FeedbackText = request.FeedbackText;
            grade.FeedbackMediaUrl = request.FeedbackMediaUrl;
            grade.RubricExecutionJson = string.IsNullOrWhiteSpace(request.RubricExecutionJson) ? "{}" : request.RubricExecutionJson;
            grade.GradedAt = now;
            grade.UpdatedAt = now;
            grade.Version++;
            if (s.Grade is null) context.SubmissionGrades.Add(grade);

            s.Status = AssignmentSubmissionStatus.Graded;
            s.UpdatedAt = now;
            s.Version++;
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            s.Grade = grade;
            return s;
        });

        if (gradedSubmission is null) return Error.NotFound("Submission.NotFound", "Submission not found.");

        // Propagate grade to all other group members
        if (gradedSubmission.GroupId.HasValue)
        {
            await groupService.PropagateGradeAsync(
                gradedSubmission.Id, graderId,
                gradedSubmission.Grade!.Score,
                gradedSubmission.Grade.FeedbackText,
                gradedSubmission.Grade.FeedbackMediaUrl,
                gradedSubmission.Grade.RubricExecutionJson,
                ct);
        }

        return ToDto(gradedSubmission);
    }

    private static Error? ValidateAssignment(UpsertAssignmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Error.Validation("Assignment.TitleRequired", "Title is required.");
        if (request.Title.Length > 200) return Error.Validation("Assignment.TitleTooLong", "Title cannot exceed 200 characters.");
        if (request.CourseOfferingId == Guid.Empty) return Error.Validation("Assignment.CourseOfferingRequired", "Course offering is required.");
        if (request.MaxPoints <= 0) return Error.Validation("Assignment.InvalidPoints", "Max points must be greater than zero.");
        if (request.MaxFileSizeMb <= 0) return Error.Validation("Assignment.InvalidFileSize", "Maximum file size must be greater than zero.");
        if (request.CutoffDate.HasValue && request.CutoffDate.Value < request.DueDate)
        {
            return Error.Validation("Assignment.InvalidCutoff", "Cutoff date must be on or after due date.");
        }
        return null;
    }

    private async Task<Error?> ValidateTargetProgramsAsync(Guid courseOfferingId, IEnumerable<Guid>? programIds, CancellationToken ct)
    {
        var ids = NormalizeTargetProgramIds(programIds);
        if (ids.Count == 0) return null;

        var validProgramIds = await context.CourseOfferingPrograms
            .AsNoTracking()
            .Where(p => p.CourseOfferingId == courseOfferingId && ids.Contains(p.ProgramId))
            .Select(p => p.ProgramId)
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
        assignment.CourseOfferingId = request.CourseOfferingId;
        assignment.AssessmentCategoryId = request.AssessmentCategoryId;
        assignment.DueDate = request.DueDate.ToUniversalTime();
        assignment.CutoffDate = request.CutoffDate?.ToUniversalTime();
        assignment.AllowedExtensions = string.IsNullOrWhiteSpace(request.AllowedExtensions) ? "pdf,docx,zip" : request.AllowedExtensions.Trim().ToLowerInvariant();
        assignment.MaxFileSizeMb = request.MaxFileSizeMb;
        assignment.IsGroupAssignment = request.IsGroupAssignment;
        assignment.MaxGroupSize = request.IsGroupAssignment ? request.MaxGroupSize : null;
        assignment.ReleaseConditionsJson = string.IsNullOrWhiteSpace(request.ReleaseConditionsJson) ? "{}" : request.ReleaseConditionsJson;
        assignment.TargetProgramIdsJson = JsonSerializer.Serialize(NormalizeTargetProgramIds(request.TargetProgramIds));
        assignment.EnableTurnitinCheck = request.EnableTurnitinCheck ?? true;
    }

    private static List<Guid> NormalizeTargetProgramIds(IEnumerable<Guid>? programIds) =>
        (programIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    private static bool StudentCanAccessAssignmentProgram(Assignment assignment, IEnumerable<(Guid CourseOfferingId, Guid ProgramId)> enrollments)
    {
        var targetProgramIds = DeserializeTargetProgramIds(assignment.TargetProgramIdsJson);
        return enrollments.Any(enrollment =>
            enrollment.CourseOfferingId == assignment.CourseOfferingId &&
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

    private static AssignmentDto ToDto(Assignment x) => new(x.Id, x.Title, x.Description, x.MaxPoints, x.CourseOfferingId, x.AssessmentCategoryId, x.DueDate, x.CutoffDate, x.AllowedExtensions, x.MaxFileSizeMb, x.IsGroupAssignment, x.MaxGroupSize, x.ReleaseConditionsJson, DeserializeTargetProgramIds(x.TargetProgramIdsJson), x.CreatedAt, x.UpdatedAt, x.EnableTurnitinCheck);

    private static AssignmentSubmissionDto ToDto(AssignmentSubmission x)
    {
        TurnitinCheckResultDto? turnitinResult = null;
        if (!string.IsNullOrWhiteSpace(x.TurnitinResultJson))
        {
            try
            {
                turnitinResult = JsonSerializer.Deserialize<TurnitinCheckResultDto>(
                    x.TurnitinResultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch { }
        }

        return new(
            x.Id,
            x.AssignmentId,
            x.SubmitterId,
            x.GroupId,
            x.Status.ToString(),
            x.SubmittedAt,
            x.SubmissionMetadataJson,
            x.DigitalReceipt,
            x.Grade is null ? null : ToDto(x.Grade),
            x.CreatedAt,
            x.UpdatedAt,
            x.TurnitinSimilarityScore,
            x.TurnitinStatus,
            x.TurnitinReportUrl,
            turnitinResult,
            x.TurnitinCheckedAt);
    }

    private static SubmissionGradeDto ToDto(SubmissionGrade x) => new(x.Id, x.SubmissionId, x.GraderId, x.Score, x.FeedbackText, x.FeedbackMediaUrl, x.RubricExecutionJson, x.GradedAt);

    public async Task<ErrorOr<TurnitinCheckResultDto>> CheckTurnitinAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await turnitinService.CheckSubmissionAsync(submissionId, ct);
    }

    public async Task<ErrorOr<TurnitinCheckResultDto>> GetTurnitinReportAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await turnitinService.GetSubmissionReportAsync(submissionId, ct);
    }

    public async Task<ErrorOr<int>> ImportAssignmentsFromOfferingAsync(Guid sourceOfferingId, Guid targetOfferingId, Guid userId, CancellationToken ct = default)
    {
        var sourceAssignments = await context.Assignments
            .AsNoTracking()
            .Where(x => x.CourseOfferingId == sourceOfferingId)
            .ToListAsync(ct);

        if (sourceAssignments.Count == 0) return 0;

        var sourceOffering = await context.CourseOfferings
            .AsNoTracking()
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == sourceOfferingId, ct);

        var targetOffering = await context.CourseOfferings
            .AsNoTracking()
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == targetOfferingId, ct);

        var timeShift = TimeSpan.Zero;
        if (sourceOffering?.AcademicSession != null && targetOffering?.AcademicSession != null)
        {
            timeShift = targetOffering.AcademicSession.StartDate - sourceOffering.AcademicSession.StartDate;
        }

        var importedCount = 0;
        foreach (var src in sourceAssignments)
        {
            var exists = await context.Assignments
                .AnyAsync(x => x.CourseOfferingId == targetOfferingId && x.Title == src.Title, ct);

            if (!exists)
            {
                var copy = new Assignment
                {
                    Id = Guid.NewGuid(),
                    Title = src.Title,
                    Description = src.Description,
                    MaxPoints = src.MaxPoints,
                    CourseOfferingId = targetOfferingId,
                    AssessmentCategoryId = src.AssessmentCategoryId,
                    DueDate = src.DueDate.Add(timeShift),
                    CutoffDate = src.CutoffDate?.Add(timeShift),
                    AllowedExtensions = src.AllowedExtensions,
                    MaxFileSizeMb = src.MaxFileSizeMb,
                    IsGroupAssignment = src.IsGroupAssignment,
                    MaxGroupSize = src.MaxGroupSize,
                    ReleaseConditionsJson = src.ReleaseConditionsJson,
                    TargetProgramIdsJson = src.TargetProgramIdsJson,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                context.Assignments.Add(copy);
                importedCount++;
            }
        }

        if (importedCount > 0)
        {
            await context.SaveChangesAsync(ct);
        }

        return importedCount;
    }
}
