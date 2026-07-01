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

public class PrerequisiteValidationService : BaseService, IPrerequisiteValidationService
{
    private readonly LmsDbContext _context;
    private const decimal PassingPercent = 40m;

    public PrerequisiteValidationService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<bool>> CheckPrerequisitesAsync(Guid studentId, Guid courseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || courseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID and Course Offering ID must be provided.");
        }

        var courseOffering = await _context.CourseOfferings
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId, ct);

        if (courseOffering == null)
        {
            return Error.NotFound("CourseOffering.NotFound", "Course offering not found.");
        }

        var prerequisites = await _context.CoursePrerequisites
            .AsNoTracking()
            .Where(c => c.CourseId == courseOffering.CourseId && c.Type == Data.Enums.PrerequisiteType.HardPrerequisite)
            .ToListAsync(ct);

        if (!prerequisites.Any())
        {
            return true; // No prerequisites required
        }

        var approvedOverride = await _context.PrerequisiteOverrides
            .AnyAsync(o =>
                o.StudentId == studentId &&
                o.CourseOfferingId == courseOfferingId &&
                o.Status == "Approved", ct);

        if (approvedOverride)
        {
            return true;
        }

        var prerequisiteCourseIds = prerequisites.Select(p => p.PrerequisiteCourseId).Distinct().ToList();
        var passedCourseIds = await GetPassedCourseIdsAsync(studentId, prerequisiteCourseIds, ct);

        return prerequisiteCourseIds.All(passedCourseIds.Contains);
    }

    public async Task<ErrorOr<Deleted>> ProcessOverrideRequestAsync(Guid requestId, bool approvalGranted, string adminNotes, CancellationToken ct = default)
    {
        var overrideRequest = await _context.PrerequisiteOverrides.FindAsync(new object[] { requestId }, ct);
        if (overrideRequest == null)
        {
            return Error.NotFound("PrerequisiteOverride.NotFound", "Prerequisite override request not found.");
        }

        if (overrideRequest.Status != "Pending")
        {
            return Error.Validation("InvalidStatus", "Only pending prerequisite override requests can be processed.");
        }

        overrideRequest.Status = approvalGranted ? "Approved" : "Rejected";
        overrideRequest.ApprovedAtUtc = approvalGranted ? DateTime.UtcNow : (DateTime?)null;
        overrideRequest.ApprovedById = Guid.Empty; // In a real implementation, this would be the approving admin's ID
        overrideRequest.RejectionReason = approvalGranted ? null : adminNotes;

        await _context.SaveChangesAsync(ct);

        await LogActionAsync(approvalGranted ? "ApprovePrerequisiteOverride" : "RejectPrerequisiteOverride", "PrerequisiteOverride", requestId.ToString(),
            $"Prerequisite override request {requestId} was {(approvalGranted ? "approved" : "rejected")}", ct);

        return Result.Deleted;
    }

    public async Task<ErrorOr<PrerequisiteOverrideDto>> CreateOverrideRequestAsync(Guid studentId, Guid courseOfferingId, string reason, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || courseOfferingId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID and Course Offering ID must be provided.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("InvalidInput", "Reason is required.");
        }

        // Check if there's already a pending override request for this student and course
        var existingOverride = await _context.PrerequisiteOverrides
            .FirstOrDefaultAsync(o => 
                o.StudentId == studentId && 
                o.CourseOfferingId == courseOfferingId && 
                o.Status == "Pending", ct);

        if (existingOverride != null)
        {
            return Error.Conflict("OverrideExists", "A prerequisite override request for this student and course already exists.");
        }

        var overrideRequest = new PrerequisiteOverride
        {
            StudentId = studentId,
            CourseOfferingId = courseOfferingId,
            Reason = reason,
            Status = "Pending",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedById = studentId,
            CreatedByUserId = studentId
        };

        _context.PrerequisiteOverrides.Add(overrideRequest);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("CreatePrerequisiteOverride", "PrerequisiteOverride", overrideRequest.Id.ToString(),
            $"Student {studentId} requested prerequisite override for course offering {courseOfferingId}", ct);

        var studentName = (overrideRequest.Student?.FirstName + " " + overrideRequest.Student?.LastName) ?? "Unknown Student";
        var approvedByName = overrideRequest.ApprovedById.HasValue ? (overrideRequest.ApprovedBy?.DisplayName ?? overrideRequest.ApprovedBy?.Email ?? "Unknown") : null;
        return new PrerequisiteOverrideDto(
            overrideRequest.Id,
            overrideRequest.StudentId,
            studentName,
            overrideRequest.CourseOfferingId,
            overrideRequest.CourseOffering?.Course?.Code ?? "Unknown",
            overrideRequest.Reason,
            overrideRequest.Status,
            overrideRequest.RequestedAtUtc,
            overrideRequest.ApprovedAtUtc,
            approvedByName,
            overrideRequest.RejectionReason);
    }

    private async Task<HashSet<Guid>> GetPassedCourseIdsAsync(Guid studentId, IReadOnlyCollection<Guid> courseIds, CancellationToken ct)
    {
        if (courseIds.Count == 0)
        {
            return [];
        }

        var gradeRows = await _context.Grades
            .AsNoTracking()
            .Where(g =>
                g.StudentId == studentId &&
                courseIds.Contains(g.Assessment.CourseOffering.CourseId) &&
                _context.GradePublications.Any(p =>
                    p.CourseOfferingId == g.Assessment.CourseOfferingId &&
                    p.IsVisibleToStudents))
            .Select(g => new
            {
                g.Assessment.CourseOffering.CourseId,
                g.Assessment.CourseOfferingId,
                g.MarksObtained,
                g.Assessment.MaxMarks
            })
            .ToListAsync(ct);

        return gradeRows
            .GroupBy(g => new { g.CourseId, g.CourseOfferingId })
            .Where(g => g.Sum(x => x.MaxMarks) > 0 &&
                        g.Sum(x => x.MarksObtained) / g.Sum(x => x.MaxMarks) * 100m >= PassingPercent)
            .Select(g => g.Key.CourseId)
            .ToHashSet();
    }
}
