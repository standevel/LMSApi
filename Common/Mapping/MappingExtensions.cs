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
        p.Faculty?.ToDto() ?? new FacultyDto(p.FacultyId, "N/A", "N/A", DateOnly.MinValue, DateOnly.MinValue),
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
}
