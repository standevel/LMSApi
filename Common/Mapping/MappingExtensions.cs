using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using System.Linq;

namespace LMS.Api.Common.Mapping;

public static class MappingExtensions
{
    public static AcademicProgramDto ToDto(this AcademicProgram p) => new(
        p.Id,
        p.Name,
        p.Code,
        p.Description,
        p.DegreeAwarded,
        p.Department?.ToDto() ?? new DepartmentDto(p.DepartmentId, "N/A", "N/A", new FacultyDto(Guid.Empty, "N/A", "N/A", DateOnly.MinValue, DateOnly.MinValue), DateOnly.MinValue, DateOnly.MinValue),
        p.Type,
        p.DurationYears,
        p.IsActive,
        p.Levels.OrderBy(l => l.Order).Select(l => l.ToDto()).ToList(),
        p.MinJambScore,
        p.MaxAdmissions,
        p.RequiredJambSubjectsJson,
        p.RequiredOLevelSubjectsJson);

    public static FacultyDto ToDto(this Faculty f) => new(
        f.Id,
        f.Name,
        f.Label,
        f.CreatedDate,
        f.UpdatedDate);

    public static DepartmentDto ToDto(this Department d) => new(
        d.Id,
        d.Name,
        d.Code,
        d.Faculty?.ToDto() ?? new FacultyDto(d.FacultyId, "N/A", "N/A", DateOnly.MinValue, DateOnly.MinValue),
        d.CreatedDate,
        d.UpdatedDate);

    public static AcademicLevelDto ToDto(this AcademicLevel l) => new(
        l.Id,
        l.ProgramId,
        l.Name,
        l.Order,
        l.Semesters.OrderBy(s => (int)s.Semester).Select(s => s.ToDto()).ToList());

    public static LevelSemesterConfigDto ToDto(this LevelSemesterConfig s) => new(
        s.Id,
        s.Semester,
        s.MaxCreditLoad);

    public static AcademicSessionDto ToDto(this AcademicSession s) => new(
        s.Id,
        s.Name,
        s.StartDate,
        s.EndDate,
        s.IsActive);

    public static CourseDto ToDto(this Course course) => new(
        course.Id,
        course.Code,
        course.Title,
        course.Description,
        course.CreditUnits,
        course.IsActive,
        course.Offerings.Select(o => o.ToDto()).ToList());

    public static CourseOfferingDto ToDto(this CourseOffering o) => new(
        o.Id,
        o.ProgramId,
        o.Program?.Name ?? "N/A",
        o.LevelId,
        o.Level?.Name ?? "N/A",
        o.AcademicSessionId,
        o.AcademicSession?.Name ?? "N/A",
        o.LecturerId,
        o.Lecturer?.DisplayName,
        (int)o.Semester);

    public static CurriculumDto ToDto(this Curriculum c) => new(
        c.Id,
        c.ProgramId,
        c.Program?.Name ?? string.Empty,
        c.AdmissionSessionId,
        c.AdmissionSession?.Name ?? string.Empty,
        c.Name,
        c.MinCreditUnitsForGraduation,
        c.Status,
        c.IsActive,
        c.Courses.OrderBy(cc => cc.Level.Order).ThenBy(cc => (int)cc.Semester).Select(cc => cc.ToDto()).ToList());

    public static CurriculumCourseDto ToDto(this CurriculumCourse cc) => new(
        cc.Id,
        cc.LevelId,
        cc.Level?.Name ?? string.Empty,
        cc.CourseId,
        cc.Course?.Code ?? string.Empty,
        cc.Course?.Title ?? string.Empty,
        cc.CreditUnits,
        cc.Semester,
        cc.Category);

    public static CurriculumSummaryDto ToSummaryDto(this Curriculum x) => new(
        x.Id,
        x.Name,
        x.AdmissionSession?.Name ?? string.Empty,
        x.Status,
        x.IsActive);

    // New ToDto methods for Communication entities
    public static AnnouncementDto ToDto(this Announcement a) => new(
        a.Id,
        a.Title,
        a.Content,
        a.AuthorId,
        a.Author?.DisplayName ?? a.Author?.Email ?? "N/A",
        a.IsGlobal,
        a.CreatedAt,
        a.UpdatedAt,
        a.IsActive,
        a.ExpiresAt
    );

    public static DiscussionThreadDto ToDto(this DiscussionThread t) => new(
        t.Id,
        t.Title,
        t.AuthorId,
        t.Author?.DisplayName ?? t.Author?.Email ?? "N/A",
        t.CourseOfferingId,
        t.CourseOffering?.Course?.Title ?? "N/A",
        t.IsPinned,
        t.IsLocked,
        t.CreatedAt,
        t.UpdatedAt,
        t.IsActive,
        t.Posts.Count(p => p.IsActive) // Only count active posts
    );

    public static DiscussionPostDto ToDto(this DiscussionPost p) => new(
        p.Id,
        p.DiscussionThreadId,
        p.AuthorId,
        p.Author?.DisplayName ?? p.Author?.Email ?? "N/A",
        p.Content,
        p.CreatedAt,
        p.UpdatedAt,
        p.IsActive
    );

    public static NotificationDto ToDto(this Notification n) => new(
        n.Id,
        n.RecipientId,
        n.Recipient?.DisplayName ?? n.Recipient?.Email ?? "N/A",
        n.SenderId,
        n.Sender?.DisplayName ?? n.Sender?.Email ?? "N/A",
        n.Title,
        n.Message,
        n.NotificationType,
        n.IsRead,
        n.CreatedAt,
        n.ReadAt,
        n.RelatedUrl
    );

    public static MessageDto ToDto(this Message m) => new(
        m.Id,
        m.SenderId,
        m.Sender?.DisplayName ?? m.Sender?.Email ?? "N/A",
        m.RecipientId,
        m.Recipient?.DisplayName ?? m.Recipient?.Email ?? "N/A",
        m.Content,
        m.SentAt,
        m.IsRead,
        m.ReadAt
    );
}