using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class LectureSessionService : ILectureSessionService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<LectureSessionService> _logger;

    public LectureSessionService(
        LmsDbContext context,
        ILogger<LectureSessionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SessionGenerationResult> GenerateSessionsFromTimetableAsync(
        List<Guid> timetableSlotIds,
        DateOnly endDate,
        Guid userId)
    {
        var totalCreated = 0;
        var sessionsPerSlot = new Dictionary<Guid, int>();
        var allConflicts = new List<ConflictWarning>();

        // Fetch timetable slots with related data
        var slots = await _context.LectureTimetableSlots
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Where(s => timetableSlotIds.Contains(s.Id))
            .ToListAsync();

        foreach (var slot in slots)
        {
            var sessionDates = CalculateSessionDates(
                slot,
                DateOnly.FromDateTime(slot.CourseOffering.AcademicSession.StartDate),
                endDate);

            var createdCount = 0;

            foreach (var date in sessionDates)
            {
                // Get lecturer IDs from slot (remove duplicates)
                var lecturerIds = GetLecturerIdsFromSlot(slot).Distinct().ToList();

                // Detect conflicts
                var conflicts = await DetectConflictsAsync(
                    date,
                    slot.StartTime,
                    slot.EndTime,
                    lecturerIds,
                    slot.VenueId);

                allConflicts.AddRange(conflicts);

                // Create session
                var session = new LectureSession
                {
                    CourseOfferingId = slot.CourseOfferingId,
                    TimetableSlotId = slot.Id,
                    SessionDate = date,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    VenueId = slot.VenueId,
                    IsManuallyCreated = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                _context.LectureSessions.Add(session);

                // Create lecturer associations
                foreach (var lecturerId in lecturerIds)
                {
                    var sessionLecturer = new LectureSessionLecturer
                    {
                        LectureSessionId = session.Id,
                        LecturerId = lecturerId
                    };
                    _context.LectureSessionLecturers.Add(sessionLecturer);
                }

                createdCount++;
                totalCreated++;
            }

            sessionsPerSlot[slot.Id] = createdCount;
        }

        // Save all changes at once for better performance
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Generated {TotalSessions} lecture sessions from {SlotCount} timetable slots",
            totalCreated,
            slots.Count);

        return new SessionGenerationResult(
            totalCreated,
            sessionsPerSlot,
            allConflicts);
    }

    public async Task<BulkSessionGenerationResult> GenerateBulkSessionsForSemesterAsync(
        Guid academicSessionId,
        DateOnly endDate,
        Guid userId)
    {
        var totalCreated = 0;
        var sessionsPerCourse = new Dictionary<Guid, CourseGenerationSummary>();
        var allConflicts = new List<ConflictWarning>();

        // Fetch academic session
        var academicSession = await _context.AcademicSessions
            .FirstOrDefaultAsync(s => s.Id == academicSessionId);

        if (academicSession == null)
        {
            throw new InvalidOperationException("Academic session not found");
        }

        // Fetch all timetable slots for the session
        var slots = await _context.LectureTimetableSlots
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.AcademicSession)
            .Where(s => s.CourseOffering.AcademicSessionId == academicSessionId)
            .ToListAsync();

        // Group by course offering
        var slotsByCourse = slots.GroupBy(s => s.CourseOfferingId);

        foreach (var courseGroup in slotsByCourse)
        {
            var courseOfferingId = courseGroup.Key;
            var courseSlots = courseGroup.ToList();
            var courseName = courseSlots.First().CourseOffering.Course.Title;

            var courseTotalSessions = 0;
            var courseSessionsPerSlot = new Dictionary<Guid, int>();

            foreach (var slot in courseSlots)
            {
                var sessionDates = CalculateSessionDates(
                    slot,
                    DateOnly.FromDateTime(academicSession.StartDate),
                    endDate);

                var createdCount = 0;

                foreach (var date in sessionDates)
                {
                    // Get lecturer IDs from slot (remove duplicates)
                    var lecturerIds = GetLecturerIdsFromSlot(slot).Distinct().ToList();

                    var conflicts = await DetectConflictsAsync(
                        date,
                        slot.StartTime,
                        slot.EndTime,
                        lecturerIds,
                        slot.VenueId);

                    allConflicts.AddRange(conflicts);

                    var session = new LectureSession
                    {
                        CourseOfferingId = slot.CourseOfferingId,
                        TimetableSlotId = slot.Id,
                        SessionDate = date,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        VenueId = slot.VenueId,
                        IsManuallyCreated = false,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userId
                    };

                    _context.LectureSessions.Add(session);

                    foreach (var lecturerId in lecturerIds)
                    {
                        var sessionLecturer = new LectureSessionLecturer
                        {
                            LectureSessionId = session.Id,
                            LecturerId = lecturerId
                        };
                        _context.LectureSessionLecturers.Add(sessionLecturer);
                    }

                    createdCount++;
                    courseTotalSessions++;
                    totalCreated++;
                }

                courseSessionsPerSlot[slot.Id] = createdCount;
            }

            sessionsPerCourse[courseOfferingId] = new CourseGenerationSummary(
                courseName,
                courseTotalSessions,
                courseSessionsPerSlot);
        }

        // Save all changes at once for better performance
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Generated {TotalSessions} lecture sessions for {CourseCount} courses in session {SessionId}",
            totalCreated,
            sessionsPerCourse.Count,
            academicSessionId);

        return new BulkSessionGenerationResult(
            totalCreated,
            sessionsPerCourse,
            allConflicts);
    }

    public async Task<LectureSession> CreateManualSessionAsync(
        CreateManualSessionRequest request,
        Guid userId)
    {
        // Validate date within academic session
        var courseOffering = await _context.CourseOfferings
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == request.CourseOfferingId);

        if (courseOffering == null)
        {
            throw new InvalidOperationException("Course offering not found");
        }

        if (request.SessionDate < DateOnly.FromDateTime(courseOffering.AcademicSession.StartDate) ||
            request.SessionDate > DateOnly.FromDateTime(courseOffering.AcademicSession.EndDate))
        {
            throw new InvalidOperationException(
                "Session date must fall within the academic session period");
        }

        // Validate time range
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("End time must be after start time");
        }

        // Detect conflicts
        var conflicts = await DetectConflictsAsync(
            request.SessionDate,
            request.StartTime,
            request.EndTime,
            request.LecturerIds,
            request.VenueId);

        // Create session
        var session = new LectureSession
        {
            CourseOfferingId = request.CourseOfferingId,
            TimetableSlotId = null,
            SessionDate = request.SessionDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            VenueId = request.VenueId,
            Notes = request.Notes,
            IsManuallyCreated = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.LectureSessions.Add(session);
        await _context.SaveChangesAsync();

        // Create lecturer associations
        foreach (var lecturerId in request.LecturerIds)
        {
            var sessionLecturer = new LectureSessionLecturer
            {
                LectureSessionId = session.Id,
                LecturerId = lecturerId
            };
            _context.LectureSessionLecturers.Add(sessionLecturer);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created manual lecture session {SessionId} for course offering {CourseOfferingId}",
            session.Id,
            request.CourseOfferingId);

        return session;
    }

    public async Task<List<ConflictWarning>> DetectConflictsAsync(
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        List<Guid> lecturerIds,
        Guid? venueId)
    {
        var conflicts = new List<ConflictWarning>();

        // Check lecturer conflicts
        if (lecturerIds.Any())
        {
            var lecturerConflicts = await _context.LectureSessions
                .Where(s => s.SessionDate == date
                    && s.StartTime < endTime
                    && s.EndTime > startTime
                    && s.SessionLecturers.Any(sl => lecturerIds.Contains(sl.LecturerId)))
                .ToListAsync();

            conflicts.AddRange(lecturerConflicts.Select(s =>
                new ConflictWarning(
                    ConflictType.LecturerConflict,
                    "One or more lecturers are already scheduled for another session",
                    s.Id,
                    s.SessionDate,
                    s.StartTime,
                    s.EndTime)));
        }

        // Check venue conflicts
        if (venueId.HasValue)
        {
            var venueConflicts = await _context.LectureSessions
                .Where(s => s.SessionDate == date
                    && s.VenueId == venueId
                    && s.StartTime < endTime
                    && s.EndTime > startTime)
                .ToListAsync();

            conflicts.AddRange(venueConflicts.Select(s =>
                new ConflictWarning(
                    ConflictType.VenueConflict,
                    "Venue is already scheduled for another session",
                    s.Id,
                    s.SessionDate,
                    s.StartTime,
                    s.EndTime)));
        }

        return conflicts;
    }

    public async Task<List<LectureTimetableSlot>> GetTimetableSlotsForOfferingAsync(
        Guid courseOfferingId)
    {
        return await _context.LectureTimetableSlots
            .Include(s => s.Lecturer)
            .Include(s => s.Venue)
            .Where(s => s.CourseOfferingId == courseOfferingId)
            .ToListAsync();
    }

    public async Task<List<CourseOfferingWithSlotCount>> GetCourseOfferingsWithTimetableSlotsAsync(
        Guid academicSessionId)
    {
        var offerings = await _context.CourseOfferings
            .Include(co => co.Course)
            .Where(co => co.AcademicSessionId == academicSessionId)
            .Select(co => new
            {
                co.Id,
                CourseName = co.Course.Title,
                SlotCount = _context.LectureTimetableSlots.Count(s => s.CourseOfferingId == co.Id)
            })
            .Where(x => x.SlotCount > 0)
            .ToListAsync();

        return offerings.Select(o => new CourseOfferingWithSlotCount(
            o.Id,
            o.CourseName,
            o.SlotCount)).ToList();
    }

    // Private helper methods

    private List<DateOnly> CalculateSessionDates(
        LectureTimetableSlot slot,
        DateOnly startDate,
        DateOnly endDate)
    {
        var dates = new List<DateOnly>();
        var current = startDate;

        // Find first occurrence of the slot's day of week
        while (current.DayOfWeek != slot.DayOfWeek && current <= endDate)
        {
            current = current.AddDays(1);
        }

        // Add all occurrences until end date
        while (current <= endDate)
        {
            dates.Add(current);
            current = current.AddDays(7); // Next week
        }

        return dates;
    }

    private List<Guid> GetLecturerIdsFromSlot(LectureTimetableSlot slot)
    {
        var lecturerIds = new List<Guid>();

        if (slot.LecturerId.HasValue)
        {
            lecturerIds.Add(slot.LecturerId.Value);
        }

        // Parse co-lecturers from JSON if present
        if (!string.IsNullOrEmpty(slot.CoLecturersJson))
        {
            try
            {
                var coLecturers = System.Text.Json.JsonSerializer.Deserialize<List<string>>(slot.CoLecturersJson);
                if (coLecturers != null)
                {
                    foreach (var coLecturerId in coLecturers)
                    {
                        if (Guid.TryParse(coLecturerId, out var guid))
                        {
                            lecturerIds.Add(guid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse co-lecturers JSON for slot {SlotId}", slot.Id);
            }
        }

        return lecturerIds;
    }
}
