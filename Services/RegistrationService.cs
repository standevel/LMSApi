using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using LMS.Api.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class RegistrationService : BaseService, IRegistrationService
{
    private readonly LmsDbContext _context;

    public RegistrationService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<CourseRegistrationDto>> RegisterStudent(Guid studentId, Guid courseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || courseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID and Course Offering ID must be provided.");
        }

        // Check for existing enrollment
        var programId = await GetProgramIdFromOffering(courseOfferingId, ct);
        var existingEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.ProgramId == programId, ct);

        if (existingEnrollment != null)
        {
            return Error.Conflict("AlreadyEnrolled", "Student is already enrolled in this program.");
        }

        var courseOffering = await _context.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.Program)
            .Include(co => co.Level)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId, ct);

        if (courseOffering == null)
        {
            return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");
        }

        var enrollment = new ProgramEnrollment
        {
            ProgramId = courseOffering.ProgramId,
            LevelId = courseOffering.LevelId,
            UserId = studentId,
            AcademicSessionId = courseOffering.AcademicSessionId,
            CurriculumId = Guid.Empty,
            EnrolledAtUtc = DateTime.UtcNow
        };

        try
        {
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync(ct);

            await LogActionAsync("RegisterStudent", "ProgramEnrollment", enrollment.Id.ToString(),
                $"Student {studentId} registered for program {courseOffering.ProgramId}", ct);

            return new CourseRegistrationDto(
                enrollment.Id,
                enrollment.UserId,
                courseOffering.Id,
                courseOffering.Course?.Code ?? "Unknown",
                courseOffering.Course?.Title ?? "Unknown",
                enrollment.EnrolledAtUtc,
                "Registered");
        }
        catch (Exception ex)
        {
            return Error.Validation("RegistrationFailed", $"Failed to register student: {ex.Message}");
        }
    }

    public async Task<ErrorOr<Deleted>> DropCourse(Guid enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await _context.Enrollments.FindAsync(new object[] { enrollmentId }, ct);
        if (enrollment == null)
        {
            return Error.NotFound("Enrollment.NotFound", "Enrollment record not found.");
        }

        // Remove the enrollment instead of soft delete
        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<CourseSwapRequestDto>> RequestCourseSwapAsync(Guid studentId, Guid currentCourseOfferingId, Guid newCourseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || currentCourseOfferingId == Guid.Empty || newCourseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID, current course offering ID, and new course offering ID must be provided.");
        }

        if (currentCourseOfferingId == newCourseOfferingId)
        {
            return Error.Validation("InvalidInput", "Current course offering and new course offering must be different.");
        }

        // Verify student is enrolled in the current course offering
        var currentProgramId = await GetProgramIdFromOffering(currentCourseOfferingId, ct);
        var currentEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.ProgramId == currentProgramId, ct);

        if (currentEnrollment == null)
        {
            return Error.NotFound("Enrollment.NotFound", "Student is not enrolled in the current course offering.");
        }

        var newProgramId = await GetProgramIdFromOffering(newCourseOfferingId, ct);
        var newEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.ProgramId == newProgramId, ct);

        if (newEnrollment != null)
        {
            return Error.Conflict("AlreadyEnrolled", "Student is already enrolled in the new course offering.");
        }

        // Check if there's already a pending swap request for this student and course combination
        var existingSwapRequest = await _context.CourseSwapRequests
            .FirstOrDefaultAsync(r => 
                r.StudentId == studentId && 
                r.CourseOfferingToDropId == currentCourseOfferingId && 
                r.CourseOfferingToAddId == newCourseOfferingId && 
                r.Status == "Pending", ct);

        if (existingSwapRequest != null)
        {
            return Error.Conflict("SwapRequestExists", "A swap request for this course combination already exists.");
        }

        var swapRequest = new CourseSwapRequest
        {
            StudentId = studentId,
            CourseOfferingToDropId = currentCourseOfferingId,
            CourseOfferingToAddId = newCourseOfferingId,
            Status = "Pending",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedById = Guid.Empty, // In a real implementation, this would be the requesting user's ID
            CreatedByUserId = Guid.Empty
        };

        _context.CourseSwapRequests.Add(swapRequest);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("RequestCourseSwap", "CourseSwapRequest", swapRequest.Id.ToString(),
            $"Student {studentId} requested to swap from course offering {currentCourseOfferingId} to {newCourseOfferingId}", ct);

        var swapStudentName = (swapRequest.Student?.FirstName + " " + swapRequest.Student?.LastName) ?? "Unknown Student";
        var swapProcessedByName = swapRequest.ProcessedById.HasValue ? (swapRequest.ProcessedBy?.DisplayName ?? swapRequest.ProcessedBy?.Email ?? "Unknown") : null;
        return new CourseSwapRequestDto(
            swapRequest.Id,
            swapRequest.StudentId,
            swapStudentName,
            swapRequest.CourseOfferingToDropId,
            swapRequest.CourseOfferingToDrop?.Course?.Code ?? "Unknown",
            swapRequest.CourseOfferingToAddId,
            swapRequest.CourseOfferingToAdd?.Course?.Code ?? "Unknown",
            swapRequest.Status,
            swapRequest.RequestedAtUtc,
            swapRequest.ProcessedAtUtc,
            swapProcessedByName,
            swapRequest.RejectionReason);
    }

    public async Task<ErrorOr<List<CourseSwapRequestDto>>> GetSwapRequestsAsync(CancellationToken ct = default)
    {
        var swapRequests = await _context.CourseSwapRequests
            .Include(r => r.Student)
            .Include(r => r.CourseOfferingToDrop)
                .ThenInclude(co => co.Course)
            .Include(r => r.CourseOfferingToAdd)
                .ThenInclude(co => co.Course)
            .Include(r => r.ProcessedBy)
            .ToListAsync(ct);

        return swapRequests.Select(r =>
        {
            var name = (r.Student?.FirstName + " " + r.Student?.LastName) ?? "Unknown Student";
            var processedByName = r.ProcessedById.HasValue ? (r.ProcessedBy?.DisplayName ?? r.ProcessedBy?.Email ?? "Unknown") : null;
            return new CourseSwapRequestDto(
                r.Id,
                r.StudentId,
                name,
                r.CourseOfferingToDropId,
                r.CourseOfferingToDrop?.Course?.Code ?? "Unknown",
                r.CourseOfferingToAddId,
                r.CourseOfferingToAdd?.Course?.Code ?? "Unknown",
                r.Status,
                r.RequestedAtUtc,
                r.ProcessedAtUtc,
                processedByName,
                r.RejectionReason);
        }).ToList();
    }

    public async Task<ErrorOr<Deleted>> ProcessSwapRequestAsync(Guid requestId, bool approved, string? adminNotes, CancellationToken ct = default)
    {
        var swapRequest = await _context.CourseSwapRequests.FindAsync(new object[] { requestId }, ct);
        if (swapRequest == null)
        {
            return Error.NotFound("CourseSwapRequest.NotFound", "Swap request not found.");
        }

        if (swapRequest.Status != "Pending")
        {
            return Error.Validation("InvalidStatus", "Only pending swap requests can be processed.");
        }

        swapRequest.Status = approved ? "Approved" : "Rejected";
        swapRequest.ProcessedAtUtc = DateTime.UtcNow;
        swapRequest.ProcessedById = Guid.Empty; // In a real implementation, this would be the admin user's ID
        swapRequest.RejectionReason = approved ? null : adminNotes;

        if (approved)
        {
            // If approved, drop the current course and add the new one
            var dropProgramId = await GetProgramIdFromOffering(swapRequest.CourseOfferingToDropId, ct);
            var currentEnrollment = await _context.Enrollments
                .FirstOrDefaultAsync(e => e.UserId == swapRequest.StudentId && e.ProgramId == dropProgramId, ct);

            if (currentEnrollment != null)
            {
                _context.Enrollments.Remove(currentEnrollment);
            }

            var newCourseOffering = await _context.CourseOfferings
                .Include(co => co.Program)
                .Include(co => co.Level)
                .Include(co => co.AcademicSession)
                .FirstOrDefaultAsync(co => co.Id == swapRequest.CourseOfferingToAddId, ct);

            if (newCourseOffering != null)
            {
                var newEnrollment = new ProgramEnrollment
                {
                    ProgramId = newCourseOffering.ProgramId,
                    LevelId = newCourseOffering.LevelId,
                    UserId = swapRequest.StudentId,
                    AcademicSessionId = newCourseOffering.AcademicSessionId,
                    CurriculumId = Guid.Empty,
                    EnrolledAtUtc = DateTime.UtcNow
                };

                _context.Enrollments.Add(newEnrollment);
            }
        }

        await _context.SaveChangesAsync(ct);

        await LogActionAsync(approved ? "ApproveSwapRequest" : "RejectSwapRequest", "CourseSwapRequest", requestId.ToString(),
            $"Swap request {requestId} was {(approved ? "approved" : "rejected")}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<List<ProgramEnrollmentDto>>> GetRegistrationHistoryAsync(Guid studentId, CancellationToken ct = default)
    {
        // For simplicity, we're returning current enrollments as history
        // In a real implementation, you'd have a separate table for historical enrollments
        var enrollments = await _context.Enrollments
            .Include(e => e.Program)
            .Include(e => e.Level)
            .Include(e => e.AcademicSession)
            .Where(e => e.UserId == studentId)
            .ToListAsync(ct);

        return enrollments.Select(e => new ProgramEnrollmentDto(
            e.Id,
            e.ProgramId,
            e.LevelId,
            e.UserId,
            e.AcademicSessionId,
            e.EnrolledAtUtc)).ToList();
    }

    private async Task<Guid> GetProgramIdFromOffering(Guid courseOfferingId, CancellationToken ct)
    {
        var offering = await _context.CourseOfferings.FirstOrDefaultAsync(co => co.Id == courseOfferingId, ct);
        return offering?.ProgramId ?? Guid.Empty;
    }
}

public record ProgramEnrollmentDto(
    Guid Id,
    Guid ProgramId,
    Guid LevelId,
    Guid UserId,
    Guid AcademicSessionId,
    DateTime EnrolledAtUtc);
