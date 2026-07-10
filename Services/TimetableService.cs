using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public interface ITimetableService
{
    Task<LectureTimetableSlot> CreateLectureTimetableSlotAsync(Guid courseOfferingId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, Guid? lecturerId, List<Guid>? coLecturerIds, Guid? venueId);
    Task<List<LectureTimetableSlot>> CreateLectureTimetableSlotsBulkAsync(List<Guid> courseOfferingIds, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, Guid? lecturerId, List<Guid>? coLecturerIds, Guid? venueId);
    Task<LectureTimetableSlot> UpdateLectureTimetableSlotAsync(Guid slotId, Guid? lecturerId, List<Guid>? coLecturerIds, TimeOnly? startTime, TimeOnly? endTime, Guid? venueId);
    Task DeleteLectureTimetableSlotAsync(Guid slotId);
    Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(Guid lecturerId, int dayOfWeek, Guid? academicSessionId = null);
    Task<ConflictDetectionResult> DetectConflictsAsync(Guid lecturerId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, Guid? academicSessionId = null, Guid? venueId = null, Guid? courseOfferingId = null);
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
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;

    public TimetableService(
        LmsDbContext context, 
        ILogger<TimetableService> logger, 
        ICurrentUserContext currentUserContext,
        INotificationService notificationService,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _currentUserContext = currentUserContext;
        _notificationService = notificationService;
        _emailService = emailService;
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

        var courseOffering = await _context.Set<CourseOffering>()
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId);
        if (courseOffering == null)
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
            var conflicts = await DetectConflictsAsync(lecturerId.Value, dayOfWeek, startTime, endTime, courseOffering.AcademicSessionId, venueId, courseOfferingId);
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

        if (lecturerId.HasValue)
        {
            var exists = await _context.Set<CourseOfferingLecturer>()
                .AnyAsync(col => col.CourseOfferingId == courseOfferingId && col.LecturerId == lecturerId.Value);
            
            if (!exists)
            {
                _context.Set<CourseOfferingLecturer>().Add(new CourseOfferingLecturer
                {
                    CourseOfferingId = courseOfferingId,
                    LecturerId = lecturerId.Value,
                    Role = LMS.Api.Data.Enums.CourseLecturerRole.Main
                });
            }
        }

        if (coLecturerIds != null && coLecturerIds.Any())
        {
            foreach (var coLecturerId in coLecturerIds)
            {
                var exists = await _context.Set<CourseOfferingLecturer>()
                    .AnyAsync(col => col.CourseOfferingId == courseOfferingId && col.LecturerId == coLecturerId);
                
                if (!exists)
                {
                    _context.Set<CourseOfferingLecturer>().Add(new CourseOfferingLecturer
                    {
                        CourseOfferingId = courseOfferingId,
                        LecturerId = coLecturerId,
                        Role = LMS.Api.Data.Enums.CourseLecturerRole.CoLecturer
                    });
                }
            }
        }

        _context.Set<LectureTimetableSlot>().Add(slot);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created timetable slot {SlotId}", slot.Id);

        var assignedLecturers = new List<Guid>();
        if (lecturerId.HasValue) assignedLecturers.Add(lecturerId.Value);
        if (coLecturerIds != null) assignedLecturers.AddRange(coLecturerIds);
        
        if (assignedLecturers.Any())
        {
            await NotifyLecturersAsync(courseOfferingId, assignedLecturers.Distinct().ToList());
        }

        return slot;
    }

    public async Task<List<LectureTimetableSlot>> CreateLectureTimetableSlotsBulkAsync(
        List<Guid> courseOfferingIds, int dayOfWeek, TimeOnly startTime, TimeOnly endTime,
        Guid? lecturerId, List<Guid>? coLecturerIds, Guid? venueId)
    {
        _logger.LogInformation("Creating bulk timetable slots for {Count} offerings", courseOfferingIds.Count);

        if (courseOfferingIds == null || courseOfferingIds.Count == 0)
            throw new InvalidOperationException("At least one course offering identifier is required.");

        if (startTime >= endTime)
            throw new InvalidOperationException("Start time must be before end time.");

        if (!Enum.IsDefined(typeof(DayOfWeek), dayOfWeek))
            throw new InvalidOperationException("Invalid day of week.");

        var callerUserId = await _currentUserContext.GetUserIdAsync();
        if (!callerUserId.HasValue || callerUserId == Guid.Empty)
            throw new InvalidOperationException("The current user is not identified. Ensure authentication is present.");

        var callerExists = await _context.Set<AppUser>().AnyAsync(u => u.Id == callerUserId.Value);
        if (!callerExists)
            throw new InvalidOperationException("Authenticated user not found. Ensure user account exists in the system.");

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

        var courseOfferings = await _context.Set<CourseOffering>()
            .Include(co => co.Course)
            .Where(co => courseOfferingIds.Contains(co.Id))
            .ToListAsync();

        if (courseOfferings.Count == 0)
            throw new InvalidOperationException("No valid course offerings found.");

        if (lecturerId.HasValue)
        {
            var primaryOffering = courseOfferings.First();
            var conflicts = await DetectConflictsAsync(
                lecturerId.Value, 
                dayOfWeek, 
                startTime, 
                endTime, 
                primaryOffering.AcademicSessionId, 
                venueId, 
                primaryOffering.Id);

            if (conflicts.HasConflicts)
            {
                _logger.LogWarning("Conflicts detected for lecturer {LecturerId} during bulk creation", lecturerId);
                throw new InvalidOperationException("Scheduling conflicts detected. Resolve conflicts or choose alternative times.");
            }
        }

        var slotsCreated = new List<LectureTimetableSlot>();

        foreach (var offering in courseOfferings)
        {
            var slot = new LectureTimetableSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offering.Id,
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

            if (lecturerId.HasValue)
            {
                var exists = await _context.Set<CourseOfferingLecturer>()
                    .AnyAsync(col => col.CourseOfferingId == offering.Id && col.LecturerId == lecturerId.Value);
                
                if (!exists)
                {
                    _context.Set<CourseOfferingLecturer>().Add(new CourseOfferingLecturer
                    {
                        CourseOfferingId = offering.Id,
                        LecturerId = lecturerId.Value,
                        Role = LMS.Api.Data.Enums.CourseLecturerRole.Main
                    });
                }
            }

            if (coLecturerIds != null && coLecturerIds.Any())
            {
                foreach (var coLecturerId in coLecturerIds)
                {
                    var exists = await _context.Set<CourseOfferingLecturer>()
                        .AnyAsync(col => col.CourseOfferingId == offering.Id && col.LecturerId == coLecturerId);
                    
                    if (!exists)
                    {
                        _context.Set<CourseOfferingLecturer>().Add(new CourseOfferingLecturer
                        {
                            CourseOfferingId = offering.Id,
                            LecturerId = coLecturerId,
                            Role = LMS.Api.Data.Enums.CourseLecturerRole.CoLecturer
                        });
                    }
                }
            }

            _context.Set<LectureTimetableSlot>().Add(slot);
            slotsCreated.Add(slot);
        }

        await _context.SaveChangesAsync();

        foreach (var offering in courseOfferings)
        {
            var assignedLecturers = new List<Guid>();
            if (lecturerId.HasValue) assignedLecturers.Add(lecturerId.Value);
            if (coLecturerIds != null) assignedLecturers.AddRange(coLecturerIds);

            if (assignedLecturers.Any())
            {
                try
                {
                    await NotifyLecturersAsync(offering.Id, assignedLecturers.Distinct().ToList());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send notifications for course offering {OfferingId}", offering.Id);
                }
            }
        }

        return slotsCreated;
    }

    public async Task<LectureTimetableSlot> UpdateLectureTimetableSlotAsync(
        Guid slotId, Guid? lecturerId, List<Guid>? coLecturerIds, TimeOnly? startTime, TimeOnly? endTime, Guid? venueId)
    {
        _logger.LogInformation("Updating timetable slot {SlotId}", slotId);

        var slot = await _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new InvalidOperationException($"Timetable slot not found");

        var newAssignedLecturers = new List<Guid>();

        // Update fields if provided
        if (lecturerId.HasValue && lecturerId != slot.LecturerId)
        {
            // Check conflicts with new lecturer
            var conflicts = await DetectConflictsAsync(lecturerId.Value, (int)slot.DayOfWeek,
                startTime ?? slot.StartTime, endTime ?? slot.EndTime, slot.CourseOffering.AcademicSessionId, venueId ?? slot.VenueId, slot.CourseOfferingId);

            if (conflicts.HasConflicts)
                throw new InvalidOperationException("Scheduling conflicts detected with new lecturer");

            slot.LecturerId = lecturerId.Value;
            newAssignedLecturers.Add(lecturerId.Value);
        }

        if (startTime.HasValue) slot.StartTime = startTime.Value;
        if (endTime.HasValue) slot.EndTime = endTime.Value;
        if (venueId.HasValue) slot.VenueId = venueId;

        if (coLecturerIds != null)
        {
            var oldCoLecturers = string.IsNullOrEmpty(slot.CoLecturersJson) 
                ? new List<Guid>() 
                : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(slot.CoLecturersJson) ?? new List<Guid>();

            var addedCoLecturers = coLecturerIds.Except(oldCoLecturers);
            newAssignedLecturers.AddRange(addedCoLecturers);

            slot.CoLecturersJson = coLecturerIds.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(coLecturerIds)
                : null;
        }

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

        if (newAssignedLecturers.Any())
        {
            await NotifyLecturersAsync(slot.CourseOfferingId, newAssignedLecturers.Distinct().ToList());
        }

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

    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(Guid lecturerId, int dayOfWeek, Guid? academicSessionId = null)
    {
        _logger.LogInformation("Getting available time slots for lecturer {LecturerId}", lecturerId);

        var query = _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
            .Where(s => s.LecturerId == lecturerId && s.DayOfWeek == (DayOfWeek)dayOfWeek);

        if (academicSessionId.HasValue)
        {
            query = query.Where(s => s.CourseOffering.AcademicSessionId == academicSessionId.Value);
        }

        var existingSlots = await query
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
        Guid lecturerId, int dayOfWeek, TimeOnly startTime, TimeOnly endTime, Guid? academicSessionId = null, Guid? venueId = null, Guid? courseOfferingId = null)
    {
        _logger.LogInformation("Detecting conflicts for lecturer {LecturerId} on day {DayOfWeek}", lecturerId, dayOfWeek);

        var result = new ConflictDetectionResult();

        string? targetCourseCode = null;
        if (courseOfferingId.HasValue)
        {
            var offering = await _context.Set<CourseOffering>()
                .Include(co => co.Course)
                .FirstOrDefaultAsync(co => co.Id == courseOfferingId.Value);
            targetCourseCode = offering?.Course?.Code;
        }

        var query = _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
                .ThenInclude(co => co.Course)
            .Where(s => s.LecturerId == lecturerId && s.DayOfWeek == (DayOfWeek)dayOfWeek);

        if (academicSessionId.HasValue)
        {
            query = query.Where(s => s.CourseOffering.AcademicSessionId == academicSessionId.Value);
        }

        var existingSlots = await query.ToListAsync();

        // Check for time overlaps
        foreach (var slot in existingSlots)
        {
            if ((startTime >= slot.StartTime && startTime < slot.EndTime) ||
                (endTime > slot.StartTime && endTime <= slot.EndTime) ||
                (startTime <= slot.StartTime && endTime >= slot.EndTime))
            {
                // Ignore conflict if it's the exact same course code and same venue
                if (targetCourseCode != null &&
                    slot.CourseOffering?.Course?.Code == targetCourseCode &&
                    slot.VenueId == venueId)
                {
                    continue;
                }

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
            .Include(s => s.CourseOffering)
            .FirstOrDefaultAsync(s => s.Id == conflictingSlotId)
            ?? throw new InvalidOperationException("Slot not found");

        if (replacementLecturerId == Guid.Empty)
            throw new InvalidOperationException("Replacement lecturer is required.");

        var replacementExists = await _context.Set<AppUser>()
            .AnyAsync(u => u.Id == replacementLecturerId);
        if (!replacementExists)
            throw new InvalidOperationException("Replacement lecturer not found.");

        var conflicts = await DetectConflictsAsync(
            replacementLecturerId,
            (int)slot.DayOfWeek,
            slot.StartTime,
            slot.EndTime,
            slot.CourseOffering.AcademicSessionId,
            slot.VenueId,
            slot.CourseOfferingId);

        if (conflicts.HasConflicts)
            throw new InvalidOperationException("Replacement lecturer has a scheduling conflict for this slot.");

        var callerUserId = await _currentUserContext.GetUserIdAsync();
        if (!callerUserId.HasValue || callerUserId == Guid.Empty)
            throw new InvalidOperationException("The current user is not identified. Ensure authentication is present.");

        slot.LecturerId = replacementLecturerId;
        slot.UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        slot.UpdatedBy = callerUserId.Value;
        slot.UpdatedByUserId = callerUserId.Value;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Resolved conflict for slot {SlotId}", conflictingSlotId);
        return slot;
    }

    public async Task<IEnumerable<LectureTimetableSlot>> GetLecturerTimetableAsync(Guid lecturerId)
    {
        _logger.LogInformation("Getting timetable for lecturer {LecturerId}", lecturerId);

        var lecturerIdStr = lecturerId.ToString();
        return await _context.Set<LectureTimetableSlot>()
            .Include(s => s.CourseOffering)
            .Include(s => s.Lecturer)
            .Include(s => s.Venue)
            .Where(s => s.LecturerId == lecturerId || (s.CoLecturersJson != null && s.CoLecturersJson.Contains(lecturerIdStr)))
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
                    .ThenInclude(c => c.Program)
                        .ThenInclude(p => p.Department)
            .Include(s => s.Lecturer)
            .Where(s => s.CourseOffering.AcademicSessionId == sessionId);

        if (lecturerId.HasValue)
        {
            var lecturerIdStr = lecturerId.Value.ToString();
            query = query.Where(s => s.LecturerId == lecturerId.Value || (s.CoLecturersJson != null && s.CoLecturersJson.Contains(lecturerIdStr)));
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

    private async Task NotifyLecturersAsync(Guid courseOfferingId, List<Guid> lecturerIds)
    {
        if (!lecturerIds.Any()) return;

        var offering = await _context.Set<CourseOffering>()
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .FirstOrDefaultAsync(co => co.Id == courseOfferingId);

        if (offering == null) return;

        string sessionName = offering.AcademicSession?.Name ?? "the academic session";

        var lecturers = await _context.Set<AppUser>()
            .Where(u => lecturerIds.Contains(u.Id))
            .ToListAsync();

        foreach (var lecturer in lecturers)
        {
            await _notificationService.CreateAsync(new CreateNotificationRequest(
                lecturer.Id,
                null,
                "New Timetable Slot Assignment",
                $"You have been scheduled for a class in {offering.Course.Code}.",
                "System",
                $"/dashboard/lecturer/courses"
            ));

            if (!string.IsNullOrEmpty(lecturer.Email))
            {
                await _emailService.SendCourseAssignmentEmailAsync(
                    lecturer.Email, 
                    lecturer.DisplayName ?? "Lecturer", 
                    offering.Course.Code, 
                    offering.Course.Title, 
                    sessionName);
            }
        }
    }
}
