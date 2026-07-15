using System;

namespace LMS.Api.Contracts;

public sealed class AcademicSessionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmissionOpen { get; set; }
    public bool IsAdmissionActive { get; set; }
    public LMS.Api.Data.Enums.Semester ActiveSemester { get; set; }

    public AcademicSessionDto() { }

    public AcademicSessionDto(Guid id, string name, DateTime startDate, DateTime endDate, bool isActive, LMS.Api.Data.Enums.Semester activeSemester, bool isAdmissionOpen, bool isAdmissionActive)
    {
        Id = id;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
        ActiveSemester = activeSemester;
        IsAdmissionOpen = isAdmissionOpen;
        IsAdmissionActive = isAdmissionActive;
    }
}

public sealed class CreateAcademicSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmissionOpen { get; set; }
    public bool IsAdmissionActive { get; set; }
    public LMS.Api.Data.Enums.Semester ActiveSemester { get; set; }
}

public sealed class UpdateAcademicSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmissionOpen { get; set; }
    public bool IsAdmissionActive { get; set; }
    public LMS.Api.Data.Enums.Semester ActiveSemester { get; set; }
}

public sealed class SessionRolloverRequest
{
    public Guid SourceSessionId { get; set; }
    public Guid TargetSessionId { get; set; }
    public bool RollOverCourses { get; set; }
    public bool RollOverLecturers { get; set; }
    public bool RollOverTimetable { get; set; }
    public bool RollOverFinancials { get; set; }
    public bool RollOverScholarships { get; set; }
    public bool CloneCurriculums { get; set; }
    public bool PromoteStudents { get; set; }
    public bool OnlyPromoteGoodStanding { get; set; }
    public bool RollOverCourseRegistrations { get; set; }
    public bool MakeTargetSessionActive { get; set; }
}

public sealed class SessionRolloverResultDto
{
    public int CoursesRolledOver { get; set; }
    public int LecturersAssigned { get; set; }
    public int TimetableSlotsCopied { get; set; }
    public int FeeTemplatesCloned { get; set; }
    public int FeeAssignmentsCopied { get; set; }
    public int ScholarshipsRolledOver { get; set; }
    public int CurriculumsCloned { get; set; }
    public int StudentsPromoted { get; set; }
    public int StudentsNotPromoted { get; set; }
    public List<string> Logs { get; set; } = new();
}

