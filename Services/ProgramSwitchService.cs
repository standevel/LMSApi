using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class ProgramSwitchService : BaseService, IProgramSwitchService
{
    private readonly LmsDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IDegreeAuditService _degreeAuditService;

    public ProgramSwitchService(
        LmsDbContext db,
        IFileStorageService fileStorage,
        IDegreeAuditService degreeAuditService,
        IAuditService auditService) : base(auditService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _degreeAuditService = degreeAuditService;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STUDENT ACTIONS
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<ProgramSwitchRequestDto>> CreateRequestAsync(
        Guid studentId, CreateProgramSwitchRequest request, CancellationToken ct = default)
    {
        var student = await _db.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return Error.NotFound("Student.NotFound", "Student not found.");

        if (student.AcademicProgramId == null)
            return Error.Validation("Student.NoProgram", "Student does not have a current program assigned.");

        if (student.AcademicProgramId == request.TargetProgramId)
            return Error.Validation("ProgramSwitch.SameProgram", "Target program is the same as the current program.");

        var targetProgram = await _db.Programs
            .FirstOrDefaultAsync(p => p.Id == request.TargetProgramId && p.IsActive, ct);

        if (targetProgram == null)
            return Error.NotFound("Program.NotFound", "Target program not found or is not active.");

        // Check for an existing open request
        var existing = await _db.ProgramSwitchRequests
            .AnyAsync(r => r.StudentId == studentId
                && r.Status != ProgramSwitchStatus.Completed
                && r.Status != ProgramSwitchStatus.RejectedByHoD
                && r.Status != ProgramSwitchStatus.RejectedByDean
                && r.Status != ProgramSwitchStatus.RejectedByAdmin, ct);

        if (existing)
            return Error.Conflict("ProgramSwitch.OpenRequest",
                "An open program switch request already exists. Please wait for it to be processed or withdraw it before submitting a new one.");

        var switchRequest = new ProgramSwitchRequest
        {
            StudentId = studentId,
            FromProgramId = student.AcademicProgramId.Value,
            ToProgramId = request.TargetProgramId,
            Reason = request.Reason,
            Status = ProgramSwitchStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ProgramSwitchRequests.Add(switchRequest);
        await _db.SaveChangesAsync(ct);

        await LogActionAsync("CreateProgramSwitchRequest", "ProgramSwitchRequest", switchRequest.Id.ToString(),
            $"Student {studentId} requested switch from {student.AcademicProgram?.Name} to {targetProgram.Name}", ct);

        return await GetByIdAsync(switchRequest.Id, ct);
    }

    public async Task<ErrorOr<ProgramSwitchRequestDto>> UploadJambDocumentAsync(
        Guid requestId, Guid studentId, IFormFile file, CancellationToken ct = default)
    {
        var switchRequest = await _db.ProgramSwitchRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (switchRequest == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        if (switchRequest.StudentId != studentId)
            return Error.Forbidden("ProgramSwitchRequest.Forbidden", "You do not own this switch request.");

        if (switchRequest.Status != ProgramSwitchStatus.Draft && switchRequest.Status != ProgramSwitchStatus.PendingHoDReview)
            return Error.Validation("ProgramSwitchRequest.InvalidStatus",
                "The JAMB document can only be uploaded when the request is in Draft or PendingHoDReview status.");

        // Validate file type
        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/jpg" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return Error.Validation("Document.InvalidType", "Only PDF, JPG, and PNG documents are accepted.");

        if (file.Length > 5 * 1024 * 1024) // 5 MB limit
            return Error.Validation("Document.TooLarge", "Document must be less than 5 MB.");

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"jamb-letter-{switchRequest.Id}{ext}";
        var fileUrl = await _fileStorage.UploadFileAsync(file, "program-switch-docs", fileName);

        switchRequest.JambDocumentUrl = fileUrl;
        switchRequest.JambDocumentFileName = file.FileName;
        switchRequest.JambDocumentUploadedAt = DateTime.UtcNow;

        // Advance from Draft to pending HoD review
        if (switchRequest.Status == ProgramSwitchStatus.Draft)
            switchRequest.Status = ProgramSwitchStatus.PendingHoDReview;

        switchRequest.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await LogActionAsync("UploadJambDocument", "ProgramSwitchRequest", requestId.ToString(),
            $"Student {studentId} uploaded JAMB document for switch request {requestId}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HOD REVIEW
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<ProgramSwitchRequestDto>> HoDReviewAsync(
        Guid requestId, Guid reviewerId, bool approved, string? notes, string? rejectionReason, CancellationToken ct = default)
    {
        var switchRequest = await _db.ProgramSwitchRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (switchRequest == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        // Enforce: must be in PendingHoDReview (ensures prior stages are complete)
        if (switchRequest.Status != ProgramSwitchStatus.PendingHoDReview)
            return Error.Conflict("ProgramSwitchRequest.InvalidStatus",
                $"This request cannot be reviewed by HoD in its current state ({switchRequest.Status}). " +
                "The student must upload the JAMB admission letter first.");

        // Enforce: JAMB document gate
        if (string.IsNullOrEmpty(switchRequest.JambDocumentUrl))
            return Error.Validation("ProgramSwitchRequest.MissingDocument",
                "Cannot proceed with approval — the student has not yet uploaded the JAMB admission letter.");

        switchRequest.HoDReviewedById = reviewerId;
        switchRequest.HoDReviewedAt = DateTime.UtcNow;
        switchRequest.HoDNotes = notes;
        switchRequest.UpdatedAt = DateTime.UtcNow;

        if (approved)
        {
            switchRequest.Status = ProgramSwitchStatus.PendingDeanReview;
        }
        else
        {
            switchRequest.Status = ProgramSwitchStatus.RejectedByHoD;
            switchRequest.RejectionReason = rejectionReason ?? "Rejected by Head of Department.";
            switchRequest.RejectedById = reviewerId;
            switchRequest.RejectedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await LogActionAsync("HoDReview", "ProgramSwitchRequest", requestId.ToString(),
            $"HoD {reviewerId} {(approved ? "approved" : "rejected")} switch request {requestId}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEAN REVIEW
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<ProgramSwitchRequestDto>> DeanReviewAsync(
        Guid requestId, Guid reviewerId, bool approved, string? notes, string? rejectionReason, CancellationToken ct = default)
    {
        var switchRequest = await _db.ProgramSwitchRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (switchRequest == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        // Enforce: must be in PendingDeanReview — guarantees HoD already approved
        if (switchRequest.Status != ProgramSwitchStatus.PendingDeanReview)
            return Error.Conflict("ProgramSwitchRequest.InvalidStatus",
                $"This request cannot be reviewed by the Dean in its current state ({switchRequest.Status}). " +
                "Head of Department approval must be completed first.");

        // Enforce: JAMB document gate (double-check)
        if (string.IsNullOrEmpty(switchRequest.JambDocumentUrl))
            return Error.Validation("ProgramSwitchRequest.MissingDocument",
                "Cannot proceed with approval — the JAMB admission letter document is missing.");

        switchRequest.DeanReviewedById = reviewerId;
        switchRequest.DeanReviewedAt = DateTime.UtcNow;
        switchRequest.DeanNotes = notes;
        switchRequest.UpdatedAt = DateTime.UtcNow;

        if (approved)
        {
            switchRequest.Status = ProgramSwitchStatus.PendingAdminAction;
        }
        else
        {
            switchRequest.Status = ProgramSwitchStatus.RejectedByDean;
            switchRequest.RejectionReason = rejectionReason ?? "Rejected by Dean.";
            switchRequest.RejectedById = reviewerId;
            switchRequest.RejectedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await LogActionAsync("DeanReview", "ProgramSwitchRequest", requestId.ToString(),
            $"Dean {reviewerId} {(approved ? "approved" : "rejected")} switch request {requestId}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN COMPLETION
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<ProgramSwitchRequestDto>> AdminCompleteAsync(
        Guid requestId, Guid adminId, string? notes, CancellationToken ct = default)
    {
        var switchRequest = await _db.ProgramSwitchRequests
            .Include(r => r.Student)
            .Include(r => r.ToProgram)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (switchRequest == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        // Enforce: must be PendingAdminAction — guarantees HoD + Dean both approved
        if (switchRequest.Status != ProgramSwitchStatus.PendingAdminAction)
            return Error.Conflict("ProgramSwitchRequest.InvalidStatus",
                $"Cannot complete switch in current state ({switchRequest.Status}). " +
                "Both HoD and Dean approval must be completed first.");

        // Enforce: JAMB document gate
        if (string.IsNullOrEmpty(switchRequest.JambDocumentUrl))
            return Error.Validation("ProgramSwitchRequest.MissingDocument",
                "Cannot complete program switch — the JAMB admission letter document is missing.");

        var student = switchRequest.Student;
        var targetProgram = switchRequest.ToProgram;

        // ── 1. Update Student's primary program ───────────────────────────
        student.AcademicProgramId = targetProgram.Id;
        student.FacultyId = targetProgram.DepartmentId != Guid.Empty
            ? (await _db.Departments.FirstOrDefaultAsync(d => d.Id == targetProgram.DepartmentId, ct))?.FacultyId
            : student.FacultyId;
        student.UpdatedAt = DateTime.UtcNow;

        // ── 2. Update current active session's ProgramEnrollment ──────────
        var activeSession = await _db.AcademicSessions
            .FirstOrDefaultAsync(s => s.IsActive, ct);

        if (activeSession != null)
        {
            var currentEnrollment = await _db.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == student.Id
                    && e.AcademicSessionId == activeSession.Id, ct);

            if (currentEnrollment != null)
            {
                currentEnrollment.ProgramId = targetProgram.Id;
                // Find the target program's active curriculum for this session
                var targetCurriculum = await _db.Curricula
                    .FirstOrDefaultAsync(c => c.ProgramId == targetProgram.Id && c.IsActive, ct);
                if (targetCurriculum != null)
                    currentEnrollment.CurriculumId = targetCurriculum.Id;
            }
        }

        // ── 3. Mark any open DegreeAudit for old program as Incomplete ────
        var openAudits = await _db.DegreeAudits
            .Where(a => a.StudentId == student.Id && a.Status == DegreeAuditStatus.InProgress)
            .ToListAsync(ct);

        foreach (var audit in openAudits)
        {
            audit.Status = DegreeAuditStatus.Incomplete;
            audit.Summary = "Marked incomplete due to program switch.";
        }

        // ── 4. Create a new DegreeAudit for the new program ───────────────
        await _degreeAuditService.CreateDegreeAuditAsync(student.Id,
            new CreateDegreeAuditRequest(student.Id, targetProgram.Id, null), adminId, ct);

        // ── 5. Finalize the switch request ────────────────────────────────
        switchRequest.Status = ProgramSwitchStatus.Completed;
        switchRequest.AdminCompletedById = adminId;
        switchRequest.AdminCompletedAt = DateTime.UtcNow;
        switchRequest.AdminNotes = notes;
        switchRequest.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await LogActionAsync("AdminCompleteProgramSwitch", "ProgramSwitchRequest", requestId.ToString(),
            $"Admin {adminId} completed program switch for student {student.Id} to program {targetProgram.Name}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    public async Task<ErrorOr<ProgramSwitchRequestDto>> AdminRejectAsync(
        Guid requestId, Guid adminId, string rejectionReason, CancellationToken ct = default)
    {
        var switchRequest = await _db.ProgramSwitchRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (switchRequest == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        if (switchRequest.Status != ProgramSwitchStatus.PendingAdminAction)
            return Error.Conflict("ProgramSwitchRequest.InvalidStatus",
                $"Cannot reject in current state ({switchRequest.Status}).");

        if (string.IsNullOrEmpty(switchRequest.JambDocumentUrl))
            return Error.Validation("ProgramSwitchRequest.MissingDocument",
                "Cannot process rejection — the JAMB admission letter document is missing.");

        switchRequest.Status = ProgramSwitchStatus.RejectedByAdmin;
        switchRequest.RejectionReason = rejectionReason;
        switchRequest.RejectedById = adminId;
        switchRequest.RejectedAt = DateTime.UtcNow;
        switchRequest.AdminCompletedById = adminId;
        switchRequest.AdminCompletedAt = DateTime.UtcNow;
        switchRequest.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await LogActionAsync("AdminRejectProgramSwitch", "ProgramSwitchRequest", requestId.ToString(),
            $"Admin {adminId} rejected switch request {requestId}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // QUERIES
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetStudentRequestsAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var requests = await _db.ProgramSwitchRequests
            .Include(r => r.Student)
            .Include(r => r.FromProgram)
            .Include(r => r.ToProgram)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToSummary).ToList();
    }

    public async Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetPendingForRoleAsync(
        string role, CancellationToken ct = default)
    {
        var targetStatus = role.ToUpper() switch
        {
            "HOD" => ProgramSwitchStatus.PendingHoDReview,
            "DEAN" => ProgramSwitchStatus.PendingDeanReview,
            "ADMIN" or "REGISTRAR" => ProgramSwitchStatus.PendingAdminAction,
            _ => (ProgramSwitchStatus?)null
        };

        if (targetStatus == null)
            return Error.Validation("InvalidRole", $"Unrecognized role queue: {role}. Use 'HoD', 'Dean', or 'Admin'.");

        var requests = await _db.ProgramSwitchRequests
            .Include(r => r.Student)
            .Include(r => r.FromProgram)
            .Include(r => r.ToProgram)
            .Where(r => r.Status == targetStatus.Value)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToSummary).ToList();
    }

    public async Task<ErrorOr<List<ProgramSwitchRequestSummaryDto>>> GetAllRequestsAsync(
        string? statusFilter, CancellationToken ct = default)
    {
        var query = _db.ProgramSwitchRequests
            .Include(r => r.Student)
            .Include(r => r.FromProgram)
            .Include(r => r.ToProgram)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter)
            && Enum.TryParse<ProgramSwitchStatus>(statusFilter, true, out var parsedStatus))
        {
            query = query.Where(r => r.Status == parsedStatus);
        }

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return requests.Select(MapToSummary).ToList();
    }

    public async Task<ErrorOr<ProgramSwitchRequestDto>> GetByIdAsync(
        Guid requestId, CancellationToken ct = default)
    {
        var r = await _db.ProgramSwitchRequests
            .Include(x => x.Student)
            .Include(x => x.FromProgram)
            .Include(x => x.ToProgram)
            .Include(x => x.HoDReviewedBy)
            .Include(x => x.DeanReviewedBy)
            .Include(x => x.AdminCompletedBy)
            .Include(x => x.RejectedBy)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (r == null)
            return Error.NotFound("ProgramSwitchRequest.NotFound", "Program switch request not found.");

        return MapToDto(r);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MAPPING
    // ─────────────────────────────────────────────────────────────────────────

    private static ProgramSwitchRequestDto MapToDto(ProgramSwitchRequest r) =>
        new(
            r.Id,
            r.StudentId,
            r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "Unknown",
            r.Student?.StudentNumber ?? "N/A",
            r.FromProgramId,
            r.FromProgram?.Name ?? "N/A",
            r.ToProgramId,
            r.ToProgram?.Name ?? "N/A",
            r.Reason,
            r.Status.ToString(),
            (int)r.Status,
            r.JambDocumentUrl,
            r.JambDocumentFileName,
            r.JambDocumentUploadedAt,
            r.HoDReviewedBy?.DisplayName ?? r.HoDReviewedBy?.Email,
            r.HoDReviewedAt,
            r.HoDNotes,
            r.DeanReviewedBy?.DisplayName ?? r.DeanReviewedBy?.Email,
            r.DeanReviewedAt,
            r.DeanNotes,
            r.AdminCompletedBy?.DisplayName ?? r.AdminCompletedBy?.Email,
            r.AdminCompletedAt,
            r.AdminNotes,
            r.RejectionReason,
            r.RejectedBy?.DisplayName ?? r.RejectedBy?.Email,
            r.RejectedAt,
            r.CreatedAt,
            r.UpdatedAt);

    private static ProgramSwitchRequestSummaryDto MapToSummary(ProgramSwitchRequest r) =>
        new(
            r.Id,
            r.StudentId,
            r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "Unknown",
            r.Student?.StudentNumber ?? "N/A",
            r.FromProgram?.Name ?? "N/A",
            r.ToProgram?.Name ?? "N/A",
            r.Status.ToString(),
            !string.IsNullOrEmpty(r.JambDocumentUrl),
            r.CreatedAt);
}
