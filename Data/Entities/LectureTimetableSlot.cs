using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Data.Entities;

/// <summary>
/// Represents a lecture timetable slot - a scheduled lecture at a specific time and venue
/// </summary>
public sealed class LectureTimetableSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseOfferingId { get; set; }
    public Guid? LecturerId { get; set; }
    public string? CoLecturersJson { get; set; } // JSON array of additional lecturer IDs e.g. ["guid1","guid2"]
    public Guid? VenueId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public DateOnly CreatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly UpdatedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public Guid CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Navigation properties
    public CourseOffering CourseOffering { get; set; } = null!;
    public AppUser? Lecturer { get; set; }
    public Subject? Venue { get; set; } // Using Subject as Venue placeholder
    public AppUser CreatedByUser { get; set; } = null!;
    public AppUser? UpdatedByUser { get; set; }
}
