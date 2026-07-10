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
