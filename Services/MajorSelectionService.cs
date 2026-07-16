using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class MajorSelectionService : BaseService, IMajorSelectionService
{
    private readonly LmsDbContext _db;
    private readonly IDegreeAuditService _degreeAuditService;

    public MajorSelectionService(
        LmsDbContext db,
        IDegreeAuditService degreeAuditService,
        IAuditService auditService) : base(auditService)
    {
        _db = db;
        _degreeAuditService = degreeAuditService;
    }

    public async Task<ErrorOr<List<SpecializationOptionDto>>> GetAvailableSpecializationsAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var student = await _db.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return Error.NotFound("Student.NotFound", "Student not found.");

        if (student.AcademicProgramId == null)
            return Error.Validation("Student.NoProgram", "Student does not have a current program assigned.");

        var specializations = await _db.Programs
            .AsNoTracking()
            .Where(p => p.ParentProgramId == student.AcademicProgramId.Value && p.IsActive)
            .Select(p => new SpecializationOptionDto(
                p.Id,
                p.Name,
                p.Code,
                p.Description,
                p.SpecializationStartYear))
            .ToListAsync(ct);

        return specializations;
    }

    public async Task<ErrorOr<MajorDeclarationRequestDto>> CreateDeclarationRequestAsync(
        Guid studentId, CreateMajorDeclarationRequest request, CancellationToken ct = default)
    {
        var student = await _db.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return Error.NotFound("Student.NotFound", "Student not found.");

        if (student.AcademicProgramId == null)
            return Error.Validation("Student.NoProgram", "Student does not have a current program assigned.");

        var targetProgram = await _db.Programs
            .FirstOrDefaultAsync(p => p.Id == request.TargetProgramId && p.IsActive, ct);

        if (targetProgram == null)
            return Error.NotFound("Program.NotFound", "Target program not found or is not active.");

        if (targetProgram.ParentProgramId != student.AcademicProgramId)
            return Error.Validation("MajorSelection.InvalidTarget", "Target program must be a sub-major/specialization of the student's current program.");

        // Check if there is already a pending request
        var existing = await _db.MajorDeclarationRequests
            .AnyAsync(r => r.StudentId == studentId && r.Status == "Pending", ct);

        if (existing)
            return Error.Conflict("MajorSelection.PendingRequest", "A pending major declaration request already exists.");

        var newRequest = new MajorDeclarationRequest
        {
            StudentId = studentId,
            ParentProgramId = student.AcademicProgramId.Value,
            DeclaredProgramId = request.TargetProgramId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.MajorDeclarationRequests.Add(newRequest);
        await _db.SaveChangesAsync(ct);

        await LogActionAsync("CreateMajorDeclarationRequest", "MajorDeclarationRequest", newRequest.Id.ToString(),
            $"Student {studentId} declared major {targetProgram.Name}", ct);

        return await GetByIdAsync(newRequest.Id, ct);
    }

    public async Task<ErrorOr<List<MajorDeclarationRequestDto>>> GetPendingRequestsForAdviserAsync(
        Guid adviserId, CancellationToken ct = default)
    {
        // Get student IDs assigned to this adviser
        var studentIds = await _db.CourseAdviserAssignments
            .Where(a => a.AdviserId == adviserId)
            .Select(a => a.StudentId)
            .ToListAsync(ct);

        var requests = await _db.MajorDeclarationRequests
            .Include(r => r.Student)
            .Include(r => r.ParentProgram)
            .Include(r => r.DeclaredProgram)
            .Where(r => r.Status == "Pending" && studentIds.Contains(r.StudentId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        // Fallback: if the adviser is HOD of the department, pull all pending requests for that department
        if (!requests.Any())
        {
            var isHod = await _db.Departments.AnyAsync(d => d.HeadId == adviserId, ct);
            if (isHod)
            {
                requests = await _db.MajorDeclarationRequests
                    .Include(r => r.Student)
                    .Include(r => r.ParentProgram)
                    .Include(r => r.DeclaredProgram)
                    .Where(r => r.Status == "Pending" && r.ParentProgram.Department.HeadId == adviserId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync(ct);
            }
        }

        return requests.Select(MapToDto).ToList();
    }

    public async Task<ErrorOr<List<MajorDeclarationRequestDto>>> GetStudentRequestsAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var requests = await _db.MajorDeclarationRequests
            .Include(r => r.Student)
            .Include(r => r.ParentProgram)
            .Include(r => r.DeclaredProgram)
            .Include(r => r.ApprovedBy)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToDto).ToList();
    }

    public async Task<ErrorOr<MajorDeclarationRequestDto>> ReviewRequestAsync(
        Guid requestId, Guid adviserId, ReviewMajorDeclarationRequest review, CancellationToken ct = default)
    {
        var request = await _db.MajorDeclarationRequests
            .Include(r => r.Student)
            .Include(r => r.DeclaredProgram)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null)
            return Error.NotFound("MajorDeclarationRequest.NotFound", "Declaration request not found.");

        if (request.Status != "Pending")
            return Error.Validation("MajorDeclarationRequest.InvalidStatus", "Only pending requests can be reviewed.");

        var adviser = await _db.Users.FirstOrDefaultAsync(u => u.Id == adviserId, ct);
        if (adviser == null)
            return Error.NotFound("Adviser.NotFound", "Adviser profile not found.");

        request.ApprovedById = adviserId;
        request.ApprovedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        if (review.Approved)
        {
            request.Status = "Approved";

            var student = request.Student;
            var targetProgram = request.DeclaredProgram;

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
                    
                    // Find the target child program's active curriculum for this session
                    var targetCurriculum = await _db.Curricula
                        .FirstOrDefaultAsync(c => c.ProgramId == targetProgram.Id && c.IsActive, ct);
                    
                    if (targetCurriculum != null)
                    {
                        currentEnrollment.CurriculumId = targetCurriculum.Id;
                    }
                    else
                    {
                        // Fallback: copy parent's active curriculum if none specifically defined for child
                        var parentCurriculum = await _db.Curricula
                            .FirstOrDefaultAsync(c => c.ProgramId == request.ParentProgramId && c.IsActive, ct);
                        if (parentCurriculum != null)
                        {
                            currentEnrollment.CurriculumId = parentCurriculum.Id;
                        }
                    }
                }
            }

            // ── 3. Mark any open DegreeAudit for old program as Incomplete ────
            var openAudits = await _db.DegreeAudits
                .Where(a => a.StudentId == student.Id && a.Status == DegreeAuditStatus.InProgress)
                .ToListAsync(ct);

            foreach (var audit in openAudits)
            {
                audit.Status = DegreeAuditStatus.Incomplete;
                audit.Summary = "Marked incomplete due to major specialization selection.";
            }

            // ── 4. Create a new DegreeAudit for the new program ───────────────
            await _degreeAuditService.CreateDegreeAuditAsync(student.Id,
                new CreateDegreeAuditRequest(student.Id, targetProgram.Id, null), adviserId, ct);
        }
        else
        {
            request.Status = "Rejected";
            request.RejectionReason = review.RejectionReason ?? "Rejected by adviser.";
        }

        await _db.SaveChangesAsync(ct);

        await LogActionAsync("ReviewMajorDeclarationRequest", "MajorDeclarationRequest", requestId.ToString(),
            $"Adviser {adviserId} {(review.Approved ? "approved" : "rejected")} major selection {requestId}", ct);

        return await GetByIdAsync(requestId, ct);
    }

    public async Task<ErrorOr<MajorDeclarationRequestDto>> GetByIdAsync(
        Guid requestId, CancellationToken ct = default)
    {
        var r = await _db.MajorDeclarationRequests
            .Include(x => x.Student)
            .Include(x => x.ParentProgram)
            .Include(x => x.DeclaredProgram)
            .Include(x => x.ApprovedBy)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (r == null)
            return Error.NotFound("MajorDeclarationRequest.NotFound", "Declaration request not found.");

        return MapToDto(r);
    }

    private static MajorDeclarationRequestDto MapToDto(MajorDeclarationRequest r) =>
        new(
            r.Id,
            r.StudentId,
            r.Student != null ? $"{r.Student.FirstName} {r.Student.LastName}" : "Unknown",
            r.Student?.StudentNumber ?? "N/A",
            r.ParentProgramId,
            r.ParentProgram?.Name ?? "N/A",
            r.DeclaredProgramId,
            r.DeclaredProgram?.Name ?? "N/A",
            r.Status,
            r.ApprovedBy?.DisplayName ?? r.ApprovedBy?.Email,
            r.ApprovedAt,
            r.RejectionReason,
            r.CreatedAt,
            r.UpdatedAt);
}
