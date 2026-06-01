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

public class ScheduleService : BaseService, IScheduleService
{
    private readonly LmsDbContext _context;

    public ScheduleService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<List<ScheduleDto>>> GetStudentScheduleAsync(Guid studentId, Guid academicSessionId, CancellationToken ct = default)
    {
        // For now, returning an empty list as a placeholder
        // In a real implementation, you'd query the LectureSessions and related entities
        // to build the student's schedule based on their enrollments
        return new List<ScheduleDto>();
    }

    public async Task<ErrorOr<ScheduleAdjustmentRequestDto>> RequestScheduleAdjustmentAsync(Guid studentId, string reason, string desiredSlotDetails, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID must be provided.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("InvalidInput", "Reason is required.");
        }

        if (string.IsNullOrWhiteSpace(desiredSlotDetails))
        {
            return Error.Validation("InvalidInput", "Desired slot details are required.");
        }

        var adjustmentRequest = new ScheduleAdjustmentRequest
        {
            StudentId = studentId,
            Reason = reason,
            DesiredSlotDetails = desiredSlotDetails,
            Status = "Pending",
            RequestedDate = DateTime.UtcNow,
            CreatedById = Guid.Empty, // In a real implementation, this would be the requesting user's ID
            CreatedByUserId = Guid.Empty
        };

        _context.ScheduleAdjustmentRequests.Add(adjustmentRequest);
        await _context.SaveChangesAsync(ct);

        await LogActionAsync("RequestScheduleAdjustment", "ScheduleAdjustmentRequest", adjustmentRequest.Id.ToString(),
            $"Student {studentId} requested schedule adjustment: {reason}", ct);

        var createdAt = DateTime.UtcNow;
        return new ScheduleAdjustmentRequestDto(
            adjustmentRequest.Id,
            adjustmentRequest.StudentId,
            adjustmentRequest.Student?.FirstName + " " + adjustmentRequest.Student?.LastName ?? "Unknown Student",
            adjustmentRequest.Reason,
            adjustmentRequest.DesiredSlotDetails,
            adjustmentRequest.Status,
            adjustmentRequest.RequestedDate,
            createdAt);
    }
}

