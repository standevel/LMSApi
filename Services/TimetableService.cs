using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public interface ITimetableService
{
    Task<LectureTimetableSlot> CreateLectureTimetableSlotAsync(Guid courseOfferingId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, Guid? lecturerId, List<Guid>? coLecturerIds, Guid? venueId);
    Task<LectureTimetableSlot> UpdateLectureTimetableSlotAsync(Guid slotId, Guid? lecturerId, List<Guid>? coLecturerIds, TimeOnly? startTime, TimeOnly? endTime, Guid? venueId);
    Task DeleteLectureTimetableSlotAsync(Guid slotId);
    Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(Guid lecturerId, int dayOfWeek);
    Task<ConflictDetectionResult> DetectConflictsAsync(Guid lecturerId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime);
    Task<LectureTimetableSlot> AutoResolveConflictAsync(Guid conflictingSlotId, Guid replacementLecturerId);
    Task<IEnumerable<LectureTimetableSlot>> GetLecturerTimetableAsync(Guid lecturerId);
    Task<IEnumerable<LectureTimetableSlot>> GetWeekViewAsync(DateOnly weekStart);
    Task<IEnumerable<LectureTimetableSlot>> GetWeekViewAsync(Guid sessionId, int weekNumber, Guid? lecturerId = null);
    Task<IEnumerable<LectureTimetableSlot>> GetCourseOfferingTimetableAsync(Guid courseOfferingId);
}

public class TimeSlot
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

public class ConflictDetectionResult
{
    public bool HasConflicts { get; set; }
    public List<string> ConflictingSlots { get; set; } = [];
    public List<AlternativeSlot> Suggestions { get; set; } = [];
}

public class AlternativeSlot
{
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public class TimetableService : ITimetableService
{
    private readonly LmsDbContext _context;
    private readonly ILogger<TimetableService> _logger;
    private readonly ICurrentUserContext _currentUserContext;

    public TimetableService(LmsDbContext context, ILogger<TimetableService> logger, ICurrentUserContext currentUserContext)
    {
        _context = context;
        _logger = logger;
        _currentUserContext = currentUserContext;
    }

    public async Task<LectureTimetableSlot> CreateLectureTimetableSlotAsync(
        Guid courseOfferingId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime,
        Guid? lecturerId, List<Guid>? coLecturerIds, Guid? venueId)
    {
        _logger.LogInformation("Creating timetable slot for course {CourseOfferingId}", courseOfferingId);

        // Validation checks
        if (startTime >= endTime)
            throw new InvalidOperationException("Start time must be before end time.");

        if (!Enum.IsDefined(typeof(DayOfWeek), dayOfWeek))
            throw new InvalidOperationException("Invalid day of week.");

        var courseOfferingExists = await _context.Set<CourseOffering>()
            .AnyAsync(co => co.Id == courseOfferingId);
        if (!courseOfferingExists)
            throw new InvalidOperationException("Course offering not found.");

        if (lecturerId.HasValue && lecturerId.Value == Guid.Empty)
            throw new InvalidOperationException("Invalid lecturer identifier.");

        if (lecturerId.HasValue)
        {
            var lecturerExists = await _context.Set<AppUser>()
                .AnyAsync(u => u.Id == lecturerId.Value);
            if (!lecturerExists)
                throw new InvalidOperationException("Lecturer not found.");
        }

        if (venueId.HasValue && venueId.Value != Guid.Empty)
        {
            var venueExists = await _context.Set<Subject>()
                .AnyAsync(v => v.Id == venueId.Value);
            if (!venueExists)
                throw new InvalidOperationException("Venue not found.");
        }

        // Check for conflicts if lecturer is assigned
        if (lecturerId.HasValue)
        {
            var conflicts = await DetectConflictsAsync(lecturerId.Value, dayOfWeek, startTime, endTime);
            if (conflicts.HasConflicts)
            {
                _logger.LogWarning("Conflicts detected for lecturer {LecturerId}", lecturerId);
                throw new InvalidOperationException("Scheduling conflicts detected. Resolve conflicts or choose alternative times.");
            }
        }

        var callerUserId = await _currentUserContext.GetUserIdAsync();
        if (!callerUserId.HasValue || callerUserId == Guid.Empty)
            throw new InvalidOperationException("The current user is not identified. Ensure authentication is present.");

        var callerExists = await _context.Set<AppUser>().AnyAsync(u => u.Id == callerUserId.Value);
        if (!callerExists)
            throw new InvalidOperationException("Authenticated user not found. Ensure user account exists in the system.");

        var slot = new LectureTimetableSlot
        {
            Id = Guid.NewGuid(),
            CourseOfferingId = courseOfferingId,
            LecturerId = lecturerId,
            CoLecturersJson = coLecturerIds is { Count: > 0 }
                ? System.Text.Json.JsonSerializer.Serialize(coLecturerIds)
                : null,
            VenueId = venueId,
            DayOfWeek = (DayOfWeek)dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            DurationMinutes = (int)(endTime - startTime).TotalMinutes,
            CreatedBy = callerUserId.Value,
            UpdatedBy = callerUserId.Value,
            CreatedByUserId = callerUserId.Value,
            UpdatedByUserId = callerUserId.Value,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _context.Set<LectureTimetableSlot>().Add(slot);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created timetable slot {SlotId}", slot.Id);
        return slot;
    }

    public async Task<LectureTimetableSlot> UpdateLectureTimetableSlotAsync(
        Guid slotId, Guid? lecturerId, List<Guid>? coLecturerIds, TimeOnly? startTime, TimeOnly? endTime, Guid? venueId)
    {
        _logger.LogInformation("Updating timetable slot {SlotId}", slotId);

        var slot = await _context.Set<LectureTimetableSlot>()
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new InvalidOperationException($"Timetable slot not found");

        // Update fields if provided
        if (lecturerId.HasValue && lecturerId != slot.LecturerId)
        {
            // Check conflicts with new lecturer
            var conflicts = await DetectConflictsAsync(lecturerId.Value, (int)slot.DayOfWeek,
                startTime ?? slot.StartTime, endTime ?? slot.EndTime);

            if (conflicts.HasConflicts)
                throw new InvalidOperationException("Scheduling conflicts detected with new lecturer");

            slot.LecturerId = lecturerId.Value;
        }

        if (startTime.HasValue) slot.StartTime = startTime.Value;
        if (endTime.HasValue) slot.EndTime = endTime.Value;
        if (venueId.HasValue) slot.VenueId = venueId;

        if (coLecturerIds != null)
            slot.CoLecturersJson = coLecturerIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(coLecturerIds)
                : null;

        if (startTime.HasValue || endTime.HasValue)
        {
            slot.DurationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
        }

        var callerUserId = await _currentUserContext.GetUserIdAsync();
        if (!callerUserId.HasValue || callerUserId == Guid.Empty)
            throw new InvalidOperationException("The current user is not identified. Ensure authentication is present.");

        slot.UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        slot.UpdatedBy = callerUserId.Value;
        slot.UpdatedByUserId = callerUserId.Value;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated timetable slot {SlotId}", slotId);

        return slot;
    }

    public async Task DeleteLectureTimetableSlotAsync(Guid slotId)
    {
        _logger.LogInformation("Deleting timetable slot {SlotId}", slotId);

        var slot = await _context.Set<LectureTimetableSlot>()
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot != null)
        {
            _context.Set<LectureTimetableSlot>().Remove(slot);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted timetable slot {SlotId}", slotId);
        }
    }

    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(Guid lecturerId, int dayOfWeek)
    {
        _logger.LogInformation("Getting available time slots for lecturer {LecturerId}", lecturerId);

        // Get existing slots for this lecturer on this day
        var existingSlots = await _context.Set<LectureTimetableSlot>()
            .Where(s => s.LecturerId == lecturerId && s.DayOfWeek == (DayOfWeek)dayOfWeek)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        // Generate time slots (e.g., every 1 hour from 08:00 to 17:00)
        var availableSlots = new List<TimeSlot>();
        var startHour = new TimeOnly(8, 0);
        var endHour = new TimeOnly(17, 0);
        var currentTime = startHour;

        while (currentTime < endHour)
        {
            var slotEnd = currentTime.AddHours(1);
            if (slotEnd > endHour) slotEnd = endHour;

            // Check if this slot has any conflicts with existing timetable
            var hasConflict = existingSlots.Any(s =>
                (currentTime >= s.StartTime && currentTime < s.EndTime) ||
                (slotEnd > s.StartTime && slotEnd <= s.EndTime) ||
                (currentTime <= s.StartTime && slotEnd >= s.EndTime));

            if (!hasConflict)
            {
                availableSlots.Add(new TimeSlot { Start = currentTime, End = slotEnd });
            }

            currentTime = slotEnd;
        }

        return availableSlots;
    }

    public async Task<ConflictDetectionResult> DetectConflictsAsync(
        Guid lecturerId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        _logger.LogInformation("Detecting conflicts for lecturer {LecturerId} on day {DayOfWeek}", lecturerId, dayOfWeek);

        var result = new ConflictDetectionResult();

        // Find all slots for this lecturer on this day
        var existingSlots = await _context.Set<LectureTimetableSlot>()
            .Where(s => s.LecturerId == lecturerId && s.DayOfWeek == (DayOfWeek)dayOfWeek)
            .ToListAsync();

        // Check for time overlaps
        foreach (var slot in existingSlots)
        {
            if ((startTime >= slot.StartTime && startTime < slot.EndTime) ||
                (endTime > slot.StartTime && endTime <= slot.EndTime) ||
                (startTime <= slot.StartTime && endTime >= slot.EndTime))
            {
                result.HasConflicts = true;
                result.ConflictingSlots.Add($"Conflicts with {slot.CourseOfferingId} from {slot.StartTime} to {slot.EndTime}");
            }
        }

        // Generate suggestions if conflicts exist
        if (result.HasConflicts)
        {
            result.Suggestions = GenerateAlternativeSlots(dayOfWeek, startTime, endTime, existingSlots);
        }

        return result;
    }

    public async Task<LectureTimetableSlot> AutoResolveConflictAsync(Guid conflictingSlotId, Guid replacementLecturerId)
    {
        _logger.LogInformation("Auto-resolving conflict for slot {SlotId} with lecturer {LecturerId}", conflictingSlotId, replacementLecturerId);

        var slot = await _context.Set<LectureTimetableSlot>()
            .FirstOrDefaultAsync(s => s.Id == conflictingSlotId)
            ?? throw new InvalidOperationException("Slot not found");

        slot.LecturerId = replacementLecturerId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resolved conflict for slot {SlotId}", conflictingSlotId);
        return slot;
    }

    public async Task<IEnumerable<LectureTimetableSlot>> GetLecturerTimetableAsync(Guid lecturerId)
    {
        _logger.LogInformation("Getting timetable for lecturer {LecturerId}", lecturerId);

        return await _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
            .Include(s => s.Lecturer)
            .Include(s => s.Venue)
            .Where(s => s.LecturerId == lecturerId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<LectureTimetableSlot>> GetWeekViewAsync(DateOnly weekStart)
    {
        _logger.LogInformation("Getting week view timetable for week starting {WeekStart}", weekStart);

        return await _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
            .Include(s => s.Lecturer)
            .Include(s => s.Venue)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<LectureTimetableSlot>> GetWeekViewAsync(Guid sessionId, int weekNumber, Guid? lecturerId = null)
    {
        _logger.LogInformation("Getting week view timetable for session {SessionId}, week {WeekNumber}, lecturer {LecturerId}", sessionId, weekNumber, lecturerId);

        var query = _context.Set<LectureTimetableSlot>()
            .AsNoTracking()
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Include(s => s.Lecturer)
            .Where(s => s.CourseOffering.AcademicSessionId == sessionId);

        if (lecturerId.HasValue)
        {
            query = query.Where(s => s.LecturerId == lecturerId.Value);
        }

        // WeekNumber is not used explicitly in this model; it can be used for week-based filtering by slot date if needed.
        return await query
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<LectureTimetableSlot>> GetCourseOfferingTimetableAsync(Guid courseOfferingId)
    {
        _logger.LogInformation("Getting timetable for course offering {CourseOfferingId}", courseOfferingId);

        return await _context.Set<LectureTimetableSlot>()
            .Include(s => s.Lecturer)
            .Include(s => s.Venue)
            .Where(s => s.CourseOfferingId == courseOfferingId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .ToListAsync();
    }

    private List<AlternativeSlot> GenerateAlternativeSlots(int dayOfWeek, TimeOnly startTime, TimeOnly endTime, List<LectureTimetableSlot> existingSlots)
    {
        var alternatives = new List<AlternativeSlot>();
        var duration = endTime - startTime;

        // Suggest alternative days or times
        for (int d = 0; d < 5; d++)
        {
            var altDay = ((dayOfWeek + d) % 5);
            var daySlots = existingSlots.Where(s => (int)s.DayOfWeek == altDay).ToList();

            var currentTime = new TimeOnly(8, 0);
            while (currentTime.AddMinutes(duration.TotalMinutes) <= new TimeOnly(17, 0))
            {
                var potentialEnd = currentTime.AddMinutes(duration.TotalMinutes);

                if (!daySlots.Any(s =>
                    (currentTime >= s.StartTime && currentTime < s.EndTime) ||
                    (potentialEnd > s.StartTime && potentialEnd <= s.EndTime)))
                {
                    alternatives.Add(new AlternativeSlot
                    {
                        DayOfWeek = altDay,
                        StartTime = currentTime,
                        EndTime = potentialEnd
                    });

                    if (alternatives.Count >= 3) return alternatives;
                }

                currentTime = currentTime.AddMinutes(30);
            }
        }

        return alternatives;
    }
}