using System.Text.Json;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public interface IAssignmentGroupService
{
    Task<ErrorOr<List<AssignmentGroupDto>>> GetGroupsAsync(Guid assignmentId, CancellationToken ct = default);
    Task<ErrorOr<AssignmentGroupDto?>> GetMyGroupAsync(Guid assignmentId, Guid currentUserId, CancellationToken ct = default);
    Task<ErrorOr<List<EnrolledStudentDto>>> GetEnrolledStudentsAsync(Guid assignmentId, CancellationToken ct = default);
    Task<ErrorOr<AssignmentGroupDto>> CreateGroupAsync(Guid assignmentId, CreateGroupRequest request, CancellationToken ct = default);
    Task<ErrorOr<AssignmentGroupDto>> UpdateGroupAsync(Guid groupId, UpdateGroupRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<ErrorOr<List<AssignmentGroupDto>>> AutoGroupAsync(Guid assignmentId, AutoGroupRequest request, CancellationToken ct = default);
    Task PropagateSubmissionAsync(Guid submissionId, CancellationToken ct = default);
    Task PropagateGradeAsync(Guid submissionId, Guid graderId, decimal score, string? feedbackText, string? feedbackMediaUrl, string rubricJson, CancellationToken ct = default);
}

public sealed class AssignmentGroupService(LmsDbContext context) : IAssignmentGroupService
{
    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<List<AssignmentGroupDto>>> GetGroupsAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");

        var groups = await context.AssignmentGroups
            .AsNoTracking()
            .Where(g => g.AssignmentId == assignmentId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        return groups.Select(ToDto).ToList();
    }

    public async Task<ErrorOr<AssignmentGroupDto?>> GetMyGroupAsync(Guid assignmentId, Guid currentUserId, CancellationToken ct = default)
    {
        var groups = await context.AssignmentGroups
            .AsNoTracking()
            .Where(g => g.AssignmentId == assignmentId)
            .ToListAsync(ct);

        var myGroup = groups.FirstOrDefault(g => DeserializeIds(g.MemberStudentIdsJson).Contains(currentUserId));
        return myGroup is null ? (AssignmentGroupDto?)null : ToDto(myGroup);
    }

    public async Task<ErrorOr<List<EnrolledStudentDto>>> GetEnrolledStudentsAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");

        var enrolled = await context.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOffering.CourseId == assignment.CourseId && e.Status == "Registered")
            .Select(e => new
            {
                e.StudentId,
                e.Student.DisplayName,
                e.Student.Email
            })
            .Distinct()
            .ToListAsync(ct);

        var studentIds = enrolled.Select(e => e.StudentId).ToList();
        var studentNumbers = await context.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.StudentNumber })
            .ToListAsync(ct);

        return enrolled.Select(e => new EnrolledStudentDto(
            e.StudentId,
            e.DisplayName ?? "Unknown",
            e.Email,
            studentNumbers.FirstOrDefault(s => s.Id == e.StudentId)?.StudentNumber
        )).ToList();
    }

    // ── Create / Update / Delete ──────────────────────────────────────────────

    public async Task<ErrorOr<AssignmentGroupDto>> CreateGroupAsync(Guid assignmentId, CreateGroupRequest request, CancellationToken ct = default)
    {
        var assignment = await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");
        if (!assignment.IsGroupAssignment) return Error.Validation("Assignment.NotGroup", "This assignment is not configured as a group assignment.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Error.Validation("Group.NameRequired", "Group name is required.");

        // Validate max group size
        if (assignment.MaxGroupSize.HasValue && request.StudentIds.Count > assignment.MaxGroupSize.Value)
            return Error.Validation("Group.TooLarge", $"Group cannot exceed {assignment.MaxGroupSize.Value} members.");

        var group = new AssignmentGroup
        {
            AssignmentId = assignmentId,
            Name = request.Name.Trim(),
            MemberStudentIdsJson = JsonSerializer.Serialize(request.StudentIds.Distinct().ToList())
        };

        context.AssignmentGroups.Add(group);
        await context.SaveChangesAsync(ct);
        return ToDto(group);
    }

    public async Task<ErrorOr<AssignmentGroupDto>> UpdateGroupAsync(Guid groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await context.AssignmentGroups
            .Include(g => g.Assignment)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return Error.NotFound("Group.NotFound", "Group not found.");

        if (request.Name is not null) group.Name = request.Name.Trim();

        if (request.StudentIds is not null)
        {
            if (group.Assignment.MaxGroupSize.HasValue && request.StudentIds.Count > group.Assignment.MaxGroupSize.Value)
                return Error.Validation("Group.TooLarge", $"Group cannot exceed {group.Assignment.MaxGroupSize.Value} members.");
            group.MemberStudentIdsJson = JsonSerializer.Serialize(request.StudentIds.Distinct().ToList());
        }

        group.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return ToDto(group);
    }

    public async Task<ErrorOr<Deleted>> DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await context.AssignmentGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return Error.NotFound("Group.NotFound", "Group not found.");

        context.AssignmentGroups.Remove(group);
        await context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    // ── Auto-Group ────────────────────────────────────────────────────────────

    public async Task<ErrorOr<List<AssignmentGroupDto>>> AutoGroupAsync(Guid assignmentId, AutoGroupRequest request, CancellationToken ct = default)
    {
        if (request.MaxPerGroup < 2) return Error.Validation("AutoGroup.Invalid", "Max per group must be at least 2.");

        var assignment = await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null) return Error.NotFound("Assignment.NotFound", "Assignment not found.");
        if (!assignment.IsGroupAssignment) return Error.Validation("Assignment.NotGroup", "This assignment is not configured as a group assignment.");

        // Get all enrolled students
        var enrolled = await context.CourseEnrollments
            .AsNoTracking()
            .Where(e => e.CourseOffering.CourseId == assignment.CourseId && e.Status == "Registered")
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(ct);

        // Get students already in groups
        var existingGroups = await context.AssignmentGroups
            .Where(g => g.AssignmentId == assignmentId)
            .ToListAsync(ct);

        var alreadyGrouped = existingGroups
            .SelectMany(g => DeserializeIds(g.MemberStudentIdsJson))
            .ToHashSet();

        var ungrouped = enrolled.Where(id => !alreadyGrouped.Contains(id)).ToList();

        // Shuffle randomly
        var rng = new Random();
        ungrouped = ungrouped.OrderBy(_ => rng.Next()).ToList();

        var newGroups = new List<AssignmentGroup>();
        int groupNumber = existingGroups.Count + 1;

        for (int i = 0; i < ungrouped.Count; i += request.MaxPerGroup)
        {
            var batch = ungrouped.Skip(i).Take(request.MaxPerGroup).ToList();
            var group = new AssignmentGroup
            {
                AssignmentId = assignmentId,
                Name = $"Group {groupNumber++}",
                MemberStudentIdsJson = JsonSerializer.Serialize(batch)
            };
            newGroups.Add(group);
            context.AssignmentGroups.Add(group);
        }

        await context.SaveChangesAsync(ct);
        return newGroups.Select(ToDto).ToList();
    }

    // ── Propagation ───────────────────────────────────────────────────────────

    public async Task PropagateSubmissionAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission is null || !submission.Assignment.IsGroupAssignment) return;

        // Find which group this student belongs to
        var groups = await context.AssignmentGroups
            .Where(g => g.AssignmentId == submission.AssignmentId)
            .ToListAsync(ct);

        var memberGroup = groups.FirstOrDefault(g => DeserializeIds(g.MemberStudentIdsJson).Contains(submission.SubmitterId));
        if (memberGroup is null) return;

        var memberIds = DeserializeIds(memberGroup.MemberStudentIdsJson)
            .Where(id => id != submission.SubmitterId)
            .ToList();

        foreach (var memberId in memberIds)
        {
            var existing = await context.AssignmentSubmissions
                .FirstOrDefaultAsync(s => s.AssignmentId == submission.AssignmentId && s.SubmitterId == memberId, ct);

            if (existing is null)
            {
                context.AssignmentSubmissions.Add(new AssignmentSubmission
                {
                    AssignmentId = submission.AssignmentId,
                    SubmitterId = memberId,
                    GroupId = memberGroup.Id,
                    Status = submission.Status,
                    SubmittedAt = submission.SubmittedAt,
                    SubmissionMetadataJson = submission.SubmissionMetadataJson,
                    DigitalReceipt = submission.DigitalReceipt,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.GroupId = memberGroup.Id;
                existing.Status = submission.Status;
                existing.SubmittedAt = submission.SubmittedAt;
                existing.SubmissionMetadataJson = submission.SubmissionMetadataJson;
                existing.DigitalReceipt = submission.DigitalReceipt;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                existing.Version++;
            }
        }

        // Also update GroupId on the original submission
        submission.GroupId = memberGroup.Id;
        await context.SaveChangesAsync(ct);
    }

    public async Task PropagateGradeAsync(Guid submissionId, Guid graderId, decimal score, string? feedbackText, string? feedbackMediaUrl, string rubricJson, CancellationToken ct = default)
    {
        var submission = await context.AssignmentSubmissions
            .Include(s => s.Assignment)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission is null || submission.GroupId is null || !submission.Assignment.IsGroupAssignment) return;

        var group = await context.AssignmentGroups.FirstOrDefaultAsync(g => g.Id == submission.GroupId, ct);
        if (group is null) return;

        var memberIds = DeserializeIds(group.MemberStudentIdsJson)
            .Where(id => id != submission.SubmitterId)
            .ToList();

        var now = DateTimeOffset.UtcNow;

        foreach (var memberId in memberIds)
        {
            var memberSubmission = await context.AssignmentSubmissions
                .Include(s => s.Grade)
                .FirstOrDefaultAsync(s => s.AssignmentId == submission.AssignmentId && s.SubmitterId == memberId, ct);

            if (memberSubmission is null) continue;

            var grade = memberSubmission.Grade ?? new SubmissionGrade { SubmissionId = memberSubmission.Id };
            grade.GraderId = graderId;
            grade.Score = score;
            grade.FeedbackText = feedbackText;
            grade.FeedbackMediaUrl = feedbackMediaUrl;
            grade.RubricExecutionJson = string.IsNullOrWhiteSpace(rubricJson) ? "{}" : rubricJson;
            grade.GradedAt = now;
            grade.UpdatedAt = now;
            grade.Version++;

            if (memberSubmission.Grade is null)
                context.SubmissionGrades.Add(grade);

            memberSubmission.Status = AssignmentSubmissionStatus.Graded;
            memberSubmission.UpdatedAt = now;
            memberSubmission.Version++;
        }

        await context.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AssignmentGroupDto ToDto(AssignmentGroup g) => new(
        g.Id,
        g.AssignmentId,
        g.Name,
        DeserializeIds(g.MemberStudentIdsJson),
        g.CreatedAt,
        g.UpdatedAt);

    private static List<Guid> DeserializeIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch { return []; }
    }
}
