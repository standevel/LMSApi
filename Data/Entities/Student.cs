using System;

namespace LMS.Api.Data.Entities;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Link to Admission Application
    public Guid AdmissionApplicationId { get; set; }
    public AdmissionApplication AdmissionApplication { get; set; } = null!;
    
    // Entra ID / Microsoft Account
    public string EntraObjectId { get; set; } = string.Empty;
    public string OfficialEmail { get; set; } = string.Empty;
    
    // Personal Info (copied from application)
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string PersonalEmail { get; set; } = string.Empty; // Original application email
    public string Phone { get; set; } = string.Empty;
    
    // Academic Info
    public Guid AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    public Guid? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
    public Guid? AcademicProgramId { get; set; }
    public AcademicProgram? AcademicProgram { get; set; }
    public Guid? LevelId { get; set; }
    public AcademicLevel? Level { get; set; }
    public string? StudentNumber { get; set; } // Matric number - assigned by Registrar after admission
    
    // Status
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Enrollment tracking
    public DateTime? EnrollmentDate { get; set; }
    public DateTime? GraduationDate { get; set; }
    
    // Fee Records
    public ICollection<StudentFeeRecord> FeeRecords { get; set; } = new List<StudentFeeRecord>();
}

public enum StudentStatus
{
    Active,
    Inactive,
    Suspended,
    Graduated,
    Withdrawn
}
