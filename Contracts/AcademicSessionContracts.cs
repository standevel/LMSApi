using System;

namespace LMS.Api.Contracts;

public sealed class AcademicSessionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }

    public AcademicSessionDto() { }

    public AcademicSessionDto(Guid id, string name, DateTime startDate, DateTime endDate, bool isActive)
    {
        Id = id;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
    }
}

public sealed class CreateAcademicSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpdateAcademicSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}
