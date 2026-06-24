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
        if (studentId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID must be provided.");
        }

        // If academicSessionId is empty, find the active academic session
        if (academicSessionId == Guid.Empty)
        {
            var activeSession = await _context.AcademicSessions
                .FirstOrDefaultAsync(s => s.IsActive, ct);
            if (activeSession == null)
            {
                return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");
            }
            academicSessionId = activeSession.Id;
        }

        var enrollments = await _context.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered" &&
                        e.CourseOffering.AcademicSessionId == academicSessionId)
            .Include(e => e.CourseOffering).ThenInclude(co => co.Course)
            .Include(e => e.CourseOffering).ThenInclude(co => co.Lecturer)
            .ToListAsync(ct);

        if (enrollments.Count == 0)
        {
            return new List<ScheduleDto>();
        }

        var offerings = enrollments.Select(e => e.CourseOffering).ToList();

        var offeringIds = offerings.Select(co => co.Id).ToList();

        // Get all timetable slots for these course offerings
        var slots = await _context.LectureTimetableSlots
            .Include(slot => slot.Lecturer)
            .Include(slot => slot.Venue)
            .Where(slot => offeringIds.Contains(slot.CourseOfferingId))
            .ToListAsync(ct);

        // Get all active/upcoming online lecture sessions for these offerings
        var sessions = await _context.LectureSessions
            .Where(s => offeringIds.Contains(s.CourseOfferingId) && s.OnlineMeetingJoinUrl != null && !s.IsCompleted)
            .ToListAsync(ct);

        var scheduleDtos = new List<ScheduleDto>();

        foreach (var enrollment in enrollments)
        {
            var offering = enrollment.CourseOffering;
                var offeringSlots = slots.Where(slot => slot.CourseOfferingId == offering.Id).ToList();
                
                if (offeringSlots.Count > 0)
                {
                    foreach (var slot in offeringSlots)
                    {
                        var lecturerName = slot.Lecturer?.DisplayName 
                            ?? offering.Lecturer?.DisplayName 
                            ?? "Unknown Lecturer";

                        var session = sessions.FirstOrDefault(s => s.TimetableSlotId == slot.Id)
                                      ?? sessions.FirstOrDefault(s => s.CourseOfferingId == offering.Id);
                        var isOnline = session?.OnlineMeetingJoinUrl != null;
                        var joinUrl = session?.OnlineMeetingJoinUrl;

                        scheduleDtos.Add(new ScheduleDto(
                            enrollment.Id,
                            studentId,
                            academicSessionId,
                            offering.Id,
                            offering.Course?.Code ?? "Unknown",
                            offering.Course?.Title ?? "Unknown",
                            (int)slot.DayOfWeek,
                            slot.StartTime.ToString("HH:mm:ss"),
                            slot.EndTime.ToString("HH:mm:ss"),
                            isOnline ? "Online" : (slot.Venue?.Name ?? "TBD"),
                            slot.LecturerId ?? offering.LecturerId,
                            lecturerName,
                            isOnline,
                            joinUrl
                        ));
                    }
                }
                else
                {
                    // No slot scheduled yet, return placeholder slot details
                    var lecturerName = offering.Lecturer?.DisplayName ?? "Unknown Lecturer";

                    var session = sessions.FirstOrDefault(s => s.CourseOfferingId == offering.Id);
                    var isOnline = session?.OnlineMeetingJoinUrl != null;
                    var joinUrl = session?.OnlineMeetingJoinUrl;

                    scheduleDtos.Add(new ScheduleDto(
                        enrollment.Id,
                        studentId,
                        academicSessionId,
                        offering.Id,
                        offering.Course?.Code ?? "Unknown",
                        offering.Course?.Title ?? "Unknown",
                        null,
                        null,
                        null,
                        isOnline ? "Online" : null,
                        offering.LecturerId,
                        lecturerName,
                        isOnline,
                        joinUrl
                    ));
                }
        }

        return scheduleDtos;
    }

    public async Task<ErrorOr<List<StudentExamDto>>> GetStudentExamsAsync(Guid studentId, Guid academicSessionId, CancellationToken ct = default)
    {
        if (studentId == Guid.Empty)
        {
            return Error.Validation("InvalidInput", "Student ID must be provided.");
        }

        if (academicSessionId == Guid.Empty)
        {
            var activeSession = await _context.AcademicSessions
                .FirstOrDefaultAsync(s => s.IsActive, ct);
            if (activeSession == null)
            {
                return Error.NotFound("AcademicSession.ActiveNotFound", "No active academic session was found.");
            }
            academicSessionId = activeSession.Id;
        }

        var enrollments = await _context.CourseEnrollments
            .Where(e => e.StudentId == studentId && e.Status == "Registered" &&
                        e.CourseOffering.AcademicSessionId == academicSessionId)
            .Include(e => e.CourseOffering).ThenInclude(co => co.Course)
            .ToListAsync(ct);

        if (enrollments.Count == 0)
        {
            return new List<StudentExamDto>();
        }

        var offeringIds = enrollments.Select(e => e.CourseOfferingId).ToList();

        // Fetch assessments in exam category
        var exams = await _context.Assessments
            .Include(a => a.CourseOffering).ThenInclude(co => co.Course)
            .Include(a => a.AssessmentCategory)
            .Where(a => offeringIds.Contains(a.CourseOfferingId) && 
                        (a.AssessmentCategory.IsExamCategory || a.AssessmentCategory.CategoryType == AssessmentCategoryType.Exam))
            .ToListAsync(ct);

        // Fetch quizzes for these course offerings to see if any matches
        var quizzes = await _context.Quizzes
            .Where(q => offeringIds.Contains(q.CourseOfferingId))
            .ToListAsync(ct);

        var examDtos = new List<StudentExamDto>();

        foreach (var exam in exams)
        {
            // Try to find a quiz matching by title (case-insensitive)
            var matchingQuiz = quizzes.FirstOrDefault(q => q.CourseOfferingId == exam.CourseOfferingId && 
                                                           q.Title.Equals(exam.Title, StringComparison.OrdinalIgnoreCase));
            
            bool isOnline = matchingQuiz != null;
            Guid? quizId = matchingQuiz?.Id;

            examDtos.Add(new StudentExamDto(
                exam.Id,
                exam.CourseOfferingId,
                exam.CourseOffering.Course?.Code ?? "Unknown",
                exam.CourseOffering.Course?.Title ?? "Unknown",
                exam.Title,
                exam.Description,
                exam.AssessmentDate,
                isOnline ? "Online" : "Main Hall", // Venue fallback
                exam.MaxMarks,
                isOnline,
                quizId
            ));
        }

        // Include any quiz matching "exam" keywords in the title that is not already matched
        foreach (var quiz in quizzes)
        {
            var isAlreadyMatched = examDtos.Any(e => e.QuizId == quiz.Id);
            bool isExamQuiz = quiz.Title.Contains("exam", StringComparison.OrdinalIgnoreCase) || 
                              quiz.Description.Contains("exam", StringComparison.OrdinalIgnoreCase);

            if (!isAlreadyMatched && isExamQuiz)
            {
                var enrollment = enrollments.FirstOrDefault(e => e.CourseOfferingId == quiz.CourseOfferingId);
                var code = enrollment?.CourseOffering?.Course?.Code ?? "Unknown";
                var title = enrollment?.CourseOffering?.Course?.Title ?? "Unknown";

                examDtos.Add(new StudentExamDto(
                    quiz.Id,
                    quiz.CourseOfferingId,
                    code,
                    title,
                    quiz.Title,
                    quiz.Description,
                    null, // Date TBD
                    "Online",
                    100m,
                    true,
                    quiz.Id
                ));
            }
        }

        return examDtos;
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
