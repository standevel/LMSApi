namespace LMS.Api.Contracts;

/// <summary>
/// Summary of a student for list views.
/// </summary>
public sealed class StudentSummaryDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? StudentNumber { get; set; } // Matric number
    public string PersonalEmail { get; set; } = string.Empty;
    public string OfficialEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ProgramName { get; set; }
    public string? DepartmentName { get; set; }
    public string? FacultyName { get; set; }
    public string? LevelName { get; set; }
    public string? SessionName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? EnrollmentDate { get; set; }
    public DateTime? GraduationDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class StudentListResponse
{
    public IEnumerable<StudentSummaryDto> Students { get; set; } = [];
    public int TotalCount { get; set; }
}

/// <summary>
/// Full student profile details.
/// </summary>
public sealed class StudentDetailDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string? StudentNumber { get; set; }
    public string PersonalEmail { get; set; } = string.Empty;
    public string OfficialEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactEmail { get; set; }
    public string? ProgramName { get; set; }
    public string? FacultyName { get; set; }
    public string? LevelName { get; set; }
    public string? SessionName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? EnrollmentDate { get; set; }
    public DateTime? GraduationDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? JambRegistrationNumber { get; set; }
    public int? JambScore { get; set; }
    public string? AdmissionApplicationId { get; set; }
}

/// <summary>
/// A single assignment/assessment result for a student.
/// </summary>
public sealed class StudentAssignmentDto
{
    public Guid Id { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public string AssessmentTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal MaxMarks { get; set; }
    public decimal? MarksObtained { get; set; }
    public string? Grade { get; set; }
    public string? Remarks { get; set; }
    public DateTime? AssessmentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsLocked { get; set; }
}

/// <summary>
/// Grade summary for a course offering (for results/transcript).
/// </summary>
public sealed class StudentCourseResultDto
{
    public Guid CourseOfferingId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public decimal CreditUnits { get; set; }
    public string Semester { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal TotalCA { get; set; }
    public decimal TotalExam { get; set; }
    public decimal TotalMarks { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string Point { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
}

/// <summary>
/// Fee record summary for a student.
/// </summary>
public sealed class StudentFeeRecordDto
{
    public Guid Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// A single fee payment.
/// </summary>
public sealed class StudentFeePaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? GatewayReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime PaidAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
}

public sealed class StudentFeesResponse
{
    public IEnumerable<StudentFeeRecordDto> Records { get; set; } = [];
    public IEnumerable<StudentFeePaymentDto> Payments { get; set; } = [];
}

/// <summary>
/// Parent/guardian linked to a student.
/// </summary>
public sealed class StudentParentDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; }
}

/// <summary>
/// Enrollment history entry.
/// </summary>
public sealed class StudentEnrollmentDto
{
    public Guid Id { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}
