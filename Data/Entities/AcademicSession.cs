using System;

namespace LMS.Api.Data.Entities;

public sealed class AcademicSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmissionOpen { get; set; }

}
