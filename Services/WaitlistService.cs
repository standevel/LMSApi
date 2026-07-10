using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class WaitlistService : BaseService, IWaitlistService
{
    private readonly LmsDbContext _context;

    public WaitlistService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<WaitlistDto>> JoinWaitlistAsync(Guid studentId, Guid courseOfferingId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty || courseOfferingId == Guid.Empty)
            return Error.Validation("InvalidInput", "Student ID and Course Offering ID must be provided.");

        var existingWaitlist = await _context.Set<Waitlist>()
            .FirstOrDefaultAsync(w => w.StudentId == studentId && w.CourseOfferingId == courseOfferingId, ct);

        if (existingWaitlist != null)
            return Error.Conflict("AlreadyWaitlisted", "You are already on the waitlist for this course.");

        var waitlist = new Waitlist
        {
            StudentId = studentId,
            CourseOfferingId = courseOfferingId,
            Status = "Active",
            WaitlistRank = 1,
            CreatedById = Guid.Empty,
            CreatedByUserId = Guid.Empty
        };

        _context.Set<Waitlist>().Add(waitlist);
        await _context.SaveChangesAsync(ct);
        return new WaitlistDto(
            waitlist.Id,
            waitlist.StudentId,
            waitlist.Student?.FirstName + " " + waitlist.Student?.LastName ?? "Unknown Student",
            waitlist.CourseOfferingId,
            waitlist.CourseOffering?.Course?.Code ?? "Unknown",
            waitlist.CourseOffering?.Course?.Title ?? "Unknown",
            waitlist.WaitlistRank,
            waitlist.Status,
            waitlist.JoinedAtUtc);
    }

    public async Task<ErrorOr<Deleted>> LeaveWaitlistAsync(Guid waitlistId, CancellationToken ct = default)
    {
        var waitlist = await _context.Set<Waitlist>().FindAsync(new object[] { waitlistId }, ct);
        if (waitlist == null)
            return Error.NotFound("Waitlist.NotFound", "Waitlist entry not found.");

        waitlist.Status = "Left";
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<WaitlistDto>>> GetStudentWaitlistsAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default)
    {
        var waitlistsQuery = _context.Set<Waitlist>()
            .Include(w => w.CourseOffering!).ThenInclude(co => co.Course)
            .Include(w => w.Student)
            .Where(w => w.StudentId == studentId && w.Status != "Left");

        if (academicSessionId.HasValue)
            waitlistsQuery = waitlistsQuery.Where(w => w.CourseOffering != null && w.CourseOffering.AcademicSessionId == academicSessionId.Value);

        var waitlists = await waitlistsQuery.ToListAsync(ct);

        return waitlists.Select(w => new WaitlistDto(
            w.Id,
            w.StudentId,
            w.Student?.FirstName + " " + w.Student?.LastName ?? "Unknown Student",
            w.CourseOfferingId,
            w.CourseOffering?.Course?.Code ?? "Unknown",
            w.CourseOffering?.Course?.Title ?? "Unknown",
            w.WaitlistRank,
            w.Status,
            w.JoinedAtUtc)).ToList();
    }
}
