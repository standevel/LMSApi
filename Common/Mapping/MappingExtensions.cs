using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Common.Mapping;

public static class MappingExtensions
{
    public static AcademicProgramDto ToDto(this AcademicProgram p) => new(
        p.Id,
        p.Name,
        p.Code,
        p.Description,
        p.DegreeAwarded,
        p.Department?.ToDto() ?? new DepartmentDto(p.DepartmentId, "N/A", "N/A", Guid.Empty, "N/A", null, null, new FacultyDto(Guid.Empty, "N/A", "N/A", null, null, DateOnly.MinValue, DateOnly.MinValue), DateOnly.MinValue, DateOnly.MinValue),
        p.Type,
        p.DurationYears,
        p.IsActive,
        p.Levels.OrderBy(l => l.Order).Select(l => l.ToDto()).ToList(),
        p.MinJambScore,
        p.MaxAdmissions,
        p.RequiredJambSubjectsJson,
        p.RequiredOLevelSubjectsJson,
        p.ParentProgramId,
        p.SpecializationStartYear);

    public static FacultyDto ToDto(this Faculty f) => new(
        f.Id,
        f.Name,
        f.Label,
        f.DeanId,
        f.Dean?.DisplayName ?? f.Dean?.Email,
        f.CreatedDate,
        f.UpdatedDate);

    public static DepartmentDto ToDto(this Department d) => new(
        d.Id,
        d.Name,
        d.Code,
        d.FacultyId,
        d.Faculty?.Name ?? "N/A",
        d.HeadId,
        d.Head?.DisplayName ?? d.Head?.Email,
        d.Faculty?.ToDto() ?? new FacultyDto(d.FacultyId, "N/A", "N/A", null, null, DateOnly.MinValue, DateOnly.MinValue),
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
        s.IsActive,
        s.ActiveSemester,
        s.IsAdmissionOpen,
        s.IsAdmissionActive);

    public static CourseDto ToDto(this Course course, IEnumerable<CurriculumCourse>? extraCurriculumCourses = null)
    {
        var currCourses = extraCurriculumCourses?.ToList();

        var offeringsList = course.Offerings ?? Array.Empty<CourseOffering>();
        var offerings = offeringsList.Select(o => {
            var dto = o.ToDto();
            if (dto.Programs == null || dto.Programs.Count == 0)
            {
                var fallbackPrograms = new List<OfferingProgramDto>();

                // 1. Fallback from CurriculumCourses matching offering's semester
                if (currCourses != null && currCourses.Count > 0)
                {
                    var ccMatches = currCourses
                        .Where(cc => cc != null && cc.Semester == o.Semester && cc.Curriculum?.Program != null)
                        .ToList();

                    foreach (var cc in ccMatches)
                    {
                        var progId = cc.Curriculum?.ProgramId ?? Guid.Empty;
                        if (progId != Guid.Empty &&
                            !fallbackPrograms.Any(fp => fp.ProgramId == progId && fp.LevelId == cc.LevelId))
                        {
                            fallbackPrograms.Add(new OfferingProgramDto(
                                progId,
                                cc.Curriculum?.Program?.Name ?? "N/A",
                                cc.LevelId,
                                cc.Level?.Name ?? "N/A"));
                        }
                    }

                    // If no match for exact semester, take any CurriculumCourse mapping
                    if (fallbackPrograms.Count == 0)
                    {
                        foreach (var cc in currCourses)
                        {
                            if (cc == null) continue;
                            var progId = cc.Curriculum?.ProgramId ?? Guid.Empty;
                            if (progId != Guid.Empty &&
                                !fallbackPrograms.Any(fp => fp.ProgramId == progId && fp.LevelId == cc.LevelId))
                            {
                                fallbackPrograms.Add(new OfferingProgramDto(
                                    progId,
                                    cc.Curriculum?.Program?.Name ?? "N/A",
                                    cc.LevelId,
                                    cc.Level?.Name ?? "N/A"));
                            }
                        }
                    }
                }

                // 2. Fallback to course's top-level Program & Level
                if (fallbackPrograms.Count == 0 && course.ProgramId != Guid.Empty)
                {
                    fallbackPrograms.Add(new OfferingProgramDto(
                        course.ProgramId,
                        course.Program?.Name ?? "N/A",
                        course.LevelId ?? Guid.Empty,
                        course.Level?.Name ?? "N/A"));
                }

                if (fallbackPrograms.Count > 0)
                {
                    return dto with { Programs = fallbackPrograms };
                }
            }
            return dto;
        }).ToList();

        return new(
            course.Id,
            course.ProgramId,
            course.Program?.Name,
            course.Code,
            course.Title,
            course.Description,
            course.CreditUnits,
            course.LevelId,
            course.Level?.Name,
            course.Semester,
            course.IsActive,
            offerings);
    }

    public static CourseOfferingDto ToDto(this CourseOffering o)
    {
        var progs = o.Programs ?? Array.Empty<CourseOfferingProgram>();
        var programs = progs.Select(p => new OfferingProgramDto(
            p.ProgramId,
            p.Program?.Name ?? "N/A",
            p.LevelId,
            p.Level?.Name ?? "N/A")).ToList();

        if (programs.Count == 0 && o.Course != null && o.Course.ProgramId != Guid.Empty)
        {
            programs.Add(new OfferingProgramDto(
                o.Course.ProgramId,
                o.Course.Program?.Name ?? "N/A",
                o.Course.LevelId ?? Guid.Empty,
                o.Course.Level?.Name ?? "N/A"));
        }

        var lecs = o.Lecturers ?? Array.Empty<CourseOfferingLecturer>();
        var lecturers = lecs.Select(l => new OfferingLecturerDto(
            l.LecturerId,
            l.Lecturer?.DisplayName,
            l.Role)).ToList();

        return new(
            o.Id,
            o.CourseId,
            o.Course?.Code ?? string.Empty,
            o.Course?.Title ?? string.Empty,
            o.AcademicSessionId,
            o.AcademicSession?.Name ?? "N/A",
            (int)o.Semester,
            programs,
            lecturers);
    }

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
