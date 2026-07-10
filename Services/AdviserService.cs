using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AdviserService(
    LmsDbContext db,
    IRegistrationService registrationService,
    IAuditService auditService) : BaseService(auditService), IAdviserService
{
    private const string Active = "Active";
    private const string Verified = "Verified";
    private const string Unlocked = "Unlocked";

    public async Task<ErrorOr<List<AdviserUserDto>>> GetEligibleAdvisersAsync(Guid actorId, Guid? departmentId, Guid? facultyId, CancellationToken ct = default)
    {
        if (!await CanManageScopeAsync(actorId, departmentId, facultyId, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have permission to manage adviser assignments for this scope.");

        var activeCounts = await db.CourseAdviserAssignments.AsNoTracking()
            .Where(x => x.Status == Active)
            .GroupBy(x => x.AdviserId)
            .Select(x => new { AdviserId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.AdviserId, x => x.Count, ct);

        var query = EligibleAdviserQuery();
        if (departmentId.HasValue)
        {
            var departmentFacultyId = await db.Departments.AsNoTracking()
                .Where(x => x.Id == departmentId.Value)
                .Select(x => (Guid?)x.FacultyId)
                .FirstOrDefaultAsync(ct);

            query = query.Where(x =>
                x.DepartmentId == departmentId.Value ||
                (departmentFacultyId.HasValue && x.FacultyId == departmentFacultyId.Value));
        }
        else if (facultyId.HasValue)
        {
            query = query.Where(x => x.FacultyId == facultyId.Value);
        }

        var users = await query
            .OrderBy(x => x.DisplayName ?? x.Email)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.Email,
                x.DepartmentId,
                DepartmentName = x.Department != null ? x.Department.Name : null,
                x.FacultyId,
                FacultyName = x.Faculty != null ? x.Faculty.Name : null
            })
            .ToListAsync(ct);

        return users.Select(x => new AdviserUserDto(
            x.Id,
            x.DisplayName ?? x.Email ?? "Unnamed adviser",
            x.Email,
            x.DepartmentId,
            x.DepartmentName,
            x.FacultyId,
            x.FacultyName,
            activeCounts.TryGetValue(x.Id, out var count) ? count : 0)).ToList();
    }

    public async Task<ErrorOr<AdviserAssignmentDto>> AssignAdviserAsync(Guid actorId, AssignAdviserRequest request, CancellationToken ct = default)
    {
        if (request.StudentId == Guid.Empty || request.AdviserId == Guid.Empty)
            return Error.Validation("Advising.InvalidInput", "Student and adviser must be provided.");

        var student = await LoadStudentAsync(request.StudentId, ct);
        if (student is null)
            return Error.NotFound("Student.NotFound", "Student not found.");

        if (!await CanManageStudentAsync(actorId, student, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have permission to assign this student's adviser.");

        if (!await EligibleAdviserQuery().AnyAsync(x => x.Id == request.AdviserId, ct))
            return Error.Validation("Advising.InvalidAdviser", "Selected adviser is not an active lecturer or adviser.");

        var existing = await db.CourseAdviserAssignments
            .FirstOrDefaultAsync(x => x.StudentId == request.StudentId && x.Status == Active, ct);
        if (existing is not null)
        {
            existing.Status = "Ended";
            existing.EndedAtUtc = DateTime.UtcNow;
        }

        var assignment = new CourseAdviserAssignment
        {
            StudentId = request.StudentId,
            AdviserId = request.AdviserId,
            AssignedById = actorId,
            Source = "Manual",
            Note = request.Note,
            AssignedAtUtc = DateTime.UtcNow
        };

        db.CourseAdviserAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        await LogActionAsync("AssignCourseAdviser", nameof(CourseAdviserAssignment), assignment.Id.ToString(), $"Assigned adviser {request.AdviserId} to student {request.StudentId}", ct);

        return await MapAssignmentAsync(assignment.Id, ct);
    }

    public async Task<ErrorOr<List<AdviserAssignmentDto>>> BulkAssignAdviserAsync(Guid actorId, BulkAssignAdviserRequest request, CancellationToken ct = default)
    {
        if (request.StudentIds.Count == 0)
            return Error.Validation("Advising.InvalidInput", "At least one student must be selected.");

        var results = new List<AdviserAssignmentDto>();
        foreach (var studentId in request.StudentIds.Distinct())
        {
            var result = await AssignAdviserAsync(actorId, new AssignAdviserRequest(studentId, request.AdviserId, request.Note), ct);
            if (result.IsError)
                return result.Errors;
            results.Add(result.Value);
        }

        return results;
    }

    public async Task<ErrorOr<AutoAssignAdvisersResultDto>> AutoAssignAdvisersAsync(Guid actorId, AutoAssignAdvisersRequest request, CancellationToken ct = default)
    {
        if (!await CanManageScopeAsync(actorId, request.DepartmentId, request.FacultyId, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have permission to auto-assign advisers for this scope.");

        var students = await db.Students
            .Include(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .Where(x => x.Status == StudentStatus.Active &&
                        !db.CourseAdviserAssignments.Any(a => a.StudentId == x.Id && a.Status == Active))
            .Where(x => !request.DepartmentId.HasValue || (x.AcademicProgram != null && x.AcademicProgram.DepartmentId == request.DepartmentId.Value))
            .Where(x => !request.FacultyId.HasValue || x.FacultyId == request.FacultyId.Value || (x.AcademicProgram != null && x.AcademicProgram.Department.FacultyId == request.FacultyId.Value))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(ct);

        var advisers = await EligibleAdviserQuery().ToListAsync(ct);
        var load = await db.CourseAdviserAssignments.AsNoTracking()
            .Where(x => x.Status == Active)
            .GroupBy(x => x.AdviserId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var assignments = new List<CourseAdviserAssignment>();
        var skipped = 0;

        foreach (var student in students)
        {
            var departmentId = student.AcademicProgram?.DepartmentId;
            var facultyId = student.FacultyId ?? student.AcademicProgram?.Department?.FacultyId;
            var candidates = advisers.Where(x => departmentId.HasValue && x.DepartmentId == departmentId.Value).ToList();
            if (candidates.Count == 0 && facultyId.HasValue)
                candidates = advisers.Where(x => x.FacultyId == facultyId.Value).ToList();

            if (candidates.Count == 0)
            {
                skipped++;
                continue;
            }

            var adviser = candidates
                .OrderBy(x => load.TryGetValue(x.Id, out var count) ? count : 0)
                .ThenBy(x => x.DisplayName ?? x.Email)
                .First();

            load[adviser.Id] = load.TryGetValue(adviser.Id, out var current) ? current + 1 : 1;
            var assignment = new CourseAdviserAssignment
            {
                StudentId = student.Id,
                AdviserId = adviser.Id,
                AssignedById = actorId,
                Source = "Auto",
                AssignedAtUtc = DateTime.UtcNow
            };
            assignments.Add(assignment);
            db.CourseAdviserAssignments.Add(assignment);
        }

        await db.SaveChangesAsync(ct);
        await LogActionAsync("AutoAssignCourseAdvisers", nameof(CourseAdviserAssignment), actorId.ToString(), $"Auto-assigned {assignments.Count} students; skipped {skipped}.", ct);

        var mapped = new List<AdviserAssignmentDto>();
        foreach (var assignment in assignments)
            mapped.Add(await MapAssignmentAsync(assignment.Id, ct));

        return new AutoAssignAdvisersResultDto(mapped.Count, skipped, mapped);
    }

    public async Task<ErrorOr<Deleted>> EndAssignmentAsync(Guid actorId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await db.CourseAdviserAssignments
            .Include(x => x.Student).ThenInclude(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .FirstOrDefaultAsync(x => x.Id == assignmentId, ct);
        if (assignment is null)
            return Error.NotFound("Advising.AssignmentNotFound", "Adviser assignment not found.");

        if (!await CanManageStudentAsync(actorId, assignment.Student, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have permission to end this assignment.");

        assignment.Status = "Ended";
        assignment.EndedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await LogActionAsync("EndCourseAdviserAssignment", nameof(CourseAdviserAssignment), assignment.Id.ToString(), "Ended adviser assignment.", ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<AdviserDashboardDto>> GetDashboardAsync(Guid actorId, CancellationToken ct = default)
    {
        var students = await GetAssignedStudentsAsync(actorId, ct);
        if (students.IsError)
            return students.Errors;

        var activeSessionId = await db.AcademicSessions.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        var verified = activeSessionId.HasValue
            ? students.Value.Count(x => x.RegistrationVerified)
            : 0;

        var followUpsDue = await db.AdvisingNotes.AsNoTracking()
            .Where(x => x.AdviserId == actorId && x.FollowUpDateUtc != null && x.FollowUpDateUtc <= DateTime.UtcNow)
            .CountAsync(ct);

        return new AdviserDashboardDto(
            students.Value.Count,
            verified,
            students.Value.Count - verified,
            followUpsDue,
            students.Value.Take(8).ToList());
    }

    public async Task<ErrorOr<List<AdviserStudentSummaryDto>>> GetAssignedStudentsAsync(Guid actorId, CancellationToken ct = default)
    {
        var query = db.CourseAdviserAssignments.AsNoTracking()
            .Where(x => x.Status == Active)
            .Include(x => x.Student).ThenInclude(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .Include(x => x.Student).ThenInclude(x => x.Faculty)
            .Include(x => x.Student).ThenInclude(x => x.Level)
            .Include(x => x.Adviser)
            .AsQueryable();

        if (!await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin], ct))
            query = query.Where(x => x.AdviserId == actorId || (x.Student.AcademicProgram != null && x.Student.AcademicProgram.Department.HeadId == actorId));

        var assignments = await query.OrderBy(x => x.Student.LastName).ThenBy(x => x.Student.FirstName).ToListAsync(ct);
        return await MapStudentSummariesAsync(assignments, ct);
    }

    public async Task<ErrorOr<AdvisingStudentProfileDto>> GetStudentProfileAsync(Guid actorId, Guid studentId, CancellationToken ct = default)
    {
        if (!await CanAccessStudentAsync(actorId, studentId, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have access to this advisee.");

        var assignment = await db.CourseAdviserAssignments.AsNoTracking()
            .Include(x => x.Student).ThenInclude(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .Include(x => x.Student).ThenInclude(x => x.Faculty)
            .Include(x => x.Student).ThenInclude(x => x.Level)
            .Include(x => x.Adviser)
            .Where(x => x.StudentId == studentId && x.Status == Active)
            .FirstOrDefaultAsync(ct);
        if (assignment is null)
            return Error.NotFound("Advising.AssignmentNotFound", "No active adviser assignment found for this student.");

        var summary = (await MapStudentSummariesAsync([assignment], ct)).Single();
        var registration = await registrationService.GetRegistrationSummaryAsync(studentId, null, ct);
        if (registration.IsError)
            return registration.Errors;
        var swaps = await registrationService.GetSwapRequestsAsync(studentId, ct);
        if (swaps.IsError)
            return swaps.Errors;
        var notes = await GetNotesAsync(actorId, studentId, ct);
        if (notes.IsError)
            return notes.Errors;

        var verification = await GetActiveVerificationAsync(studentId, registration.Value.AcademicSessionId, ct);
        return new AdvisingStudentProfileDto(summary, registration.Value, swaps.Value, notes.Value, verification is null ? null : MapVerification(verification));
    }

    public async Task<ErrorOr<List<AdvisingNoteDto>>> GetNotesAsync(Guid actorId, Guid studentId, CancellationToken ct = default)
    {
        if (!await CanAccessStudentAsync(actorId, studentId, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have access to this advisee.");

        var notes = await db.AdvisingNotes.AsNoTracking()
            .Where(x => x.StudentId == studentId)
            .Include(x => x.Adviser)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        return notes.Select(MapNote).ToList();
    }

    public async Task<ErrorOr<AdvisingNoteDto>> CreateNoteAsync(Guid actorId, Guid studentId, CreateAdvisingNoteRequest request, CancellationToken ct = default)
    {
        if (!await CanAccessStudentAsync(actorId, studentId, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have access to this advisee.");
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return Error.Validation("Advising.InvalidNote", "Title and note body are required.");

        var note = new AdvisingNote
        {
            StudentId = studentId,
            AdviserId = actorId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            FollowUpDateUtc = request.FollowUpDateUtc
        };

        db.AdvisingNotes.Add(note);
        await db.SaveChangesAsync(ct);
        await LogActionAsync("CreateAdvisingNote", nameof(AdvisingNote), note.Id.ToString(), $"Created advising note for student {studentId}.", ct);

        note.Adviser = await db.Users.FindAsync([actorId], ct) ?? new AppUser();
        return MapNote(note);
    }

    public async Task<ErrorOr<RegistrationVerificationDto>> VerifyRegistrationAsync(Guid actorId, Guid studentId, VerifyRegistrationRequest request, CancellationToken ct = default)
    {
        if (!await CanVerifyRegistrationAsync(actorId, studentId, ct))
            return Error.Forbidden("Advising.Forbidden", "Only the assigned adviser or an administrator can verify this registration.");

        var session = await db.AcademicSessions.FirstOrDefaultAsync(x => x.IsActive, ct);
        if (session is null)
            return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");

        var existing = await db.RegistrationVerifications
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicSessionId == session.Id && x.Status == Verified, ct);
        if (existing is not null)
        {
            existing.Remarks = request.Remarks;
            await db.SaveChangesAsync(ct);
            return MapVerification(await LoadVerificationAsync(existing.Id, ct) ?? existing);
        }

        var verification = new RegistrationVerification
        {
            StudentId = studentId,
            AcademicSessionId = session.Id,
            VerifiedByAdviserId = actorId,
            VerifiedAtUtc = DateTime.UtcNow,
            Remarks = request.Remarks
        };

        db.RegistrationVerifications.Add(verification);
        await db.SaveChangesAsync(ct);
        await LogActionAsync("VerifyRegistration", nameof(RegistrationVerification), verification.Id.ToString(), $"Verified registration for student {studentId}.", ct);

        return MapVerification(await LoadVerificationAsync(verification.Id, ct) ?? verification);
    }

    public async Task<ErrorOr<Deleted>> UnlockRegistrationAsync(Guid actorId, Guid studentId, UnlockRegistrationVerificationRequest request, CancellationToken ct = default)
    {
        if (!await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.HOD], ct))
            return Error.Forbidden("Advising.Forbidden", "Only HoD, Admin, or SuperAdmin can unlock a verified registration.");

        var sessionId = await db.AcademicSessions.AsNoTracking().Where(x => x.IsActive).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (!sessionId.HasValue)
            return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");

        var verification = await db.RegistrationVerifications
            .Include(x => x.Student).ThenInclude(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicSessionId == sessionId.Value && x.Status == Verified, ct);
        if (verification is null)
            return Error.NotFound("RegistrationVerification.NotFound", "No verified registration was found for this student.");

        if (!await CanManageStudentAsync(actorId, verification.Student, ct))
            return Error.Forbidden("Advising.Forbidden", "You do not have permission to unlock this student's registration.");

        verification.Status = Unlocked;
        verification.UnlockedAtUtc = DateTime.UtcNow;
        verification.UnlockedById = actorId;
        verification.UnlockReason = request.Reason;
        await db.SaveChangesAsync(ct);
        await LogActionAsync("UnlockRegistrationVerification", nameof(RegistrationVerification), verification.Id.ToString(), request.Reason, ct);
        return Result.Deleted;
    }

    public Task<bool> IsRegistrationLockedAsync(Guid studentId, Guid academicSessionId, CancellationToken ct = default) =>
        db.RegistrationVerifications.AsNoTracking()
            .AnyAsync(x => x.StudentId == studentId && x.AcademicSessionId == academicSessionId && x.Status == Verified, ct);

    private IQueryable<AppUser> EligibleAdviserQuery() =>
        db.Users
            .Include(x => x.Department)
            .Include(x => x.Faculty)
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Where(x => x.IsActive && x.UserRoles.Any(role => role.Role.Name == LmsRoles.Lecturer));

    private async Task<Student?> LoadStudentAsync(Guid studentId, CancellationToken ct) =>
        await db.Students
            .Include(x => x.AcademicProgram).ThenInclude(x => x!.Department)
            .Include(x => x.Faculty)
            .Include(x => x.Level)
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

    private async Task<bool> CanAccessStudentAsync(Guid actorId, Guid studentId, CancellationToken ct)
    {
        if (await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin], ct))
            return true;
        if (await db.CourseAdviserAssignments.AsNoTracking().AnyAsync(x => x.StudentId == studentId && x.AdviserId == actorId && x.Status == Active, ct))
            return true;

        var student = await LoadStudentAsync(studentId, ct);
        return student is not null && await CanManageStudentAsync(actorId, student, ct);
    }

    private async Task<bool> CanVerifyRegistrationAsync(Guid actorId, Guid studentId, CancellationToken ct)
    {
        if (await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin], ct))
            return true;
        return await db.CourseAdviserAssignments.AsNoTracking()
            .AnyAsync(x => x.StudentId == studentId && x.AdviserId == actorId && x.Status == Active, ct);
    }

    private async Task<bool> CanManageStudentAsync(Guid actorId, Student student, CancellationToken ct)
    {
        if (await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin], ct))
            return true;
        if (!await HasAnyRoleAsync(actorId, [LmsRoles.HOD], ct))
            return false;

        var departmentId = student.AcademicProgram?.DepartmentId;
        if (!departmentId.HasValue)
            return false;

        return await db.Departments.AsNoTracking().AnyAsync(x =>
            x.Id == departmentId.Value &&
            (x.HeadId == actorId || db.Users.Any(u => u.Id == actorId && u.DepartmentId == departmentId.Value)), ct);
    }

    private async Task<bool> CanManageScopeAsync(Guid actorId, Guid? departmentId, Guid? facultyId, CancellationToken ct)
    {
        if (await HasAnyRoleAsync(actorId, [LmsRoles.SuperAdmin, LmsRoles.Admin], ct))
            return true;
        if (!await HasAnyRoleAsync(actorId, [LmsRoles.HOD], ct))
            return false;

        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorId, ct);
        if (actor is null)
            return false;
        if (departmentId.HasValue)
            return actor.DepartmentId == departmentId.Value || await db.Departments.AsNoTracking().AnyAsync(x => x.Id == departmentId.Value && x.HeadId == actorId, ct);
        if (facultyId.HasValue)
            return actor.FacultyId == facultyId.Value;
        return true;
    }

    private async Task<bool> HasAnyRoleAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct) =>
        await db.UserRoles.AsNoTracking()
            .Include(x => x.Role)
            .AnyAsync(x => x.UserId == userId && roles.Contains(x.Role.Name), ct);

    private async Task<List<AdviserStudentSummaryDto>> MapStudentSummariesAsync(IReadOnlyCollection<CourseAdviserAssignment> assignments, CancellationToken ct)
    {
        var activeSessionId = await db.AcademicSessions.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        var studentIds = assignments.Select(x => x.StudentId).ToList();
        var verifications = activeSessionId.HasValue
            ? await db.RegistrationVerifications.AsNoTracking()
                .Where(x => studentIds.Contains(x.StudentId) && x.AcademicSessionId == activeSessionId.Value && x.Status == Verified)
                .ToDictionaryAsync(x => x.StudentId, x => x.VerifiedAtUtc, ct)
            : new Dictionary<Guid, DateTime>();

        return assignments.Select(x =>
        {
            var student = x.Student;
            return new AdviserStudentSummaryDto(
                student.Id,
                $"{student.FirstName} {student.LastName}".Trim(),
                student.StudentNumber,
                student.AcademicProgram?.Name,
                student.AcademicProgram?.Department?.Name,
                student.Faculty?.Name ?? student.AcademicProgram?.Department?.Faculty?.Name,
                student.Level?.Name,
                verifications.ContainsKey(student.Id),
                verifications.TryGetValue(student.Id, out var verifiedAt) ? verifiedAt : null,
                x.Adviser.DisplayName ?? x.Adviser.Email);
        }).ToList();
    }

    private async Task<AdviserAssignmentDto> MapAssignmentAsync(Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.CourseAdviserAssignments.AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Adviser)
            .FirstAsync(x => x.Id == assignmentId, ct);
        return new AdviserAssignmentDto(
            assignment.Id,
            assignment.StudentId,
            $"{assignment.Student.FirstName} {assignment.Student.LastName}".Trim(),
            assignment.Student.StudentNumber,
            assignment.AdviserId,
            assignment.Adviser.DisplayName ?? assignment.Adviser.Email ?? "Unnamed adviser",
            assignment.Status,
            assignment.Source,
            assignment.Note,
            assignment.AssignedAtUtc);
    }

    private async Task<RegistrationVerification?> GetActiveVerificationAsync(Guid studentId, Guid sessionId, CancellationToken ct) =>
        await db.RegistrationVerifications.AsNoTracking()
            .Include(x => x.AcademicSession)
            .Include(x => x.VerifiedByAdviser)
            .FirstOrDefaultAsync(x => x.StudentId == studentId && x.AcademicSessionId == sessionId && x.Status == Verified, ct);

    private async Task<RegistrationVerification?> LoadVerificationAsync(Guid id, CancellationToken ct) =>
        await db.RegistrationVerifications.AsNoTracking()
            .Include(x => x.AcademicSession)
            .Include(x => x.VerifiedByAdviser)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    private static AdvisingNoteDto MapNote(AdvisingNote note) => new(
        note.Id,
        note.StudentId,
        note.AdviserId,
        note.Adviser.DisplayName ?? note.Adviser.Email ?? "Unknown adviser",
        note.Title,
        note.Body,
        note.FollowUpDateUtc,
        note.IsStaffOnly,
        note.CreatedAtUtc,
        note.UpdatedAtUtc);

    private static RegistrationVerificationDto MapVerification(RegistrationVerification verification) => new(
        verification.Id,
        verification.StudentId,
        verification.AcademicSessionId,
        verification.AcademicSession?.Name ?? "Unknown session",
        verification.VerifiedByAdviserId,
        verification.VerifiedByAdviser?.DisplayName ?? verification.VerifiedByAdviser?.Email ?? "Unknown adviser",
        verification.VerifiedAtUtc,
        verification.Status,
        verification.Remarks);
}
