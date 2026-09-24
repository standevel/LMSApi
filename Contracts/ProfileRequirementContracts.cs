using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public sealed class ProfileRequirementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? AcademicSessionId { get; set; }
    public string? AcademicSessionName { get; set; }
    public Guid? AcademicLevelId { get; set; }
    public string? AcademicLevelName { get; set; }
    public Guid? FacultyId { get; set; }
    public string? FacultyName { get; set; }
    public Guid? AcademicProgramId { get; set; }
    public string? AcademicProgramName { get; set; }
    public List<string> RequiredFields { get; set; } = [];
    public bool IsActive { get; set; }
    public bool IsBlocking { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CreateProfileRequirementRequest
{
    public string Title { get; set; } = "Mandatory Student Profile Update";
    public string Description { get; set; } = "Please complete the missing information in your student file.";
    public Guid? AcademicSessionId { get; set; }
    public Guid? AcademicLevelId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? AcademicProgramId { get; set; }
    public List<string> RequiredFields { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool IsBlocking { get; set; } = true;
    public DateTime? Deadline { get; set; }
}

public sealed class UpdateProfileRequirementRequest
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public Guid? AcademicSessionId { get; set; }
    public Guid? AcademicLevelId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? AcademicProgramId { get; set; }
    public List<string>? RequiredFields { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsBlocking { get; set; }
    public DateTime? Deadline { get; set; }
}

public sealed class DeleteProfileRequirementRequest
{
    public Guid Id { get; set; }
}

public sealed class MissingFieldDescriptor
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // "text", "select", "date", "tel", "email"
    public List<string>? Options { get; set; }
    public bool Required { get; set; } = true;
    public string? Placeholder { get; set; }
}

public sealed class StudentProfileCompletionStatusResponse
{
    public bool IsRequired { get; set; }
    public Guid? RequirementId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsBlocking { get; set; }
    public DateTime? Deadline { get; set; }
    public List<MissingFieldDescriptor> MissingFields { get; set; } = [];
}

public sealed class SubmitProfileCompletionRequest
{
    public Guid? RequirementId { get; set; }
    public string? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactEmail { get; set; }
    public string? EmergencyRelationship { get; set; }
}

public sealed class SubmitProfileCompletionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public List<string> MissingFieldsRemaining { get; set; } = [];
}
