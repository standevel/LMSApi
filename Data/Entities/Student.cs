using System;
using System.Text.Json.Serialization;
using LMS.Api.Extensions;

namespace LMS.Api.Data.Entities;

public sealed class Student
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Link to Admission Application (null for bulk-imported students)
    public Guid? AdmissionApplicationId { get; set; }
    [JsonIgnore]
    public AdmissionApplication? AdmissionApplication { get; set; }
    
    // Entra ID / Microsoft Account (null until provisioned via registrar flow)
    public string? EntraObjectId { get; set; }
    public string OfficialEmail { get; set; } = string.Empty;
    
    // Personal Info (copied from application)
    private string _firstName = string.Empty;
    public string FirstName 
    { 
        get => _firstName; 
        set => _firstName = value.ToTitleCase() ?? string.Empty; 
    }

    private string _lastName = string.Empty;
    public string LastName 
    { 
        get => _lastName; 
        set => _lastName = value.ToTitleCase() ?? string.Empty; 
    }

    private string? _middleName;
    public string? MiddleName 
    { 
        get => _middleName; 
        set => _middleName = value.ToTitleCase(); 
    }
    
    public string PersonalEmail { get; set; } = string.Empty; // Original application email
    public string Phone { get; set; } = string.Empty;
    public string? Gender { get; set; }
    
    // Emergency Contact (copied from AdmissionApplication)
    private string? _emergencyContactName;
    public string? EmergencyContactName 
    { 
        get => _emergencyContactName; 
        set => _emergencyContactName = value.ToTitleCase(); 
    }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactEmail { get; set; }
    
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
    
    // JAMB / UTME Info (from admission import)
    public string? JambRegistrationNumber { get; set; }
    public int? JambScore { get; set; }
    
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
