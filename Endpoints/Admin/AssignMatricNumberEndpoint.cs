using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admin;

public sealed record AssignMatricNumberRequest(Guid StudentId, string MatricNumber);
public sealed record AssignMatricNumberResponse(Guid StudentId, string MatricNumber, string Message);

/// <summary>
/// Endpoint for Registrar to assign matric numbers to students after admission.
/// Matric number format: WU{YY}{PROGRAM}{####} e.g., WU25CSC0001
/// </summary>
public sealed class AssignMatricNumberEndpoint(LmsDbContext dbContext, ILogger<AssignMatricNumberEndpoint> logger)
    : ApiEndpoint<AssignMatricNumberRequest, AssignMatricNumberResponse>
{
    public override void Configure()
    {
        Post("/api/admin/students/{StudentId}/matric-number");
        Roles("SuperAdmin", "Admin", "Registry");
    }

    public override async Task HandleAsync(AssignMatricNumberRequest req, CancellationToken ct)
    {
        var studentId = Route<Guid>("StudentId");
        
        logger.LogInformation("[MATRIC-ASSIGN] Starting matric number assignment for student {StudentId}", studentId);

        // Validate matric number format
        if (string.IsNullOrWhiteSpace(req.MatricNumber))
        {
            await SendFailureAsync(400, "Matric number is required", "INVALID_MATRIC", "Matric number cannot be empty", ct);
            return;
        }

        // Normalize matric number (uppercase, trim)
        var normalizedMatric = req.MatricNumber.Trim().ToUpperInvariant();

        // Basic format validation: should start with WU followed by digits and letters
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedMatric, @"^WU\d{2}[A-Z]{3}\d{4}$"))
        {
            await SendFailureAsync(400, "Invalid matric number format", "INVALID_FORMAT", 
                "Matric number must follow format: WU{YY}{PROGRAM}{####} (e.g., WU25CSC0001)", ct);
            return;
        }

        // Check if student exists
        var student = await dbContext.Students
            .Include(s => s.AcademicProgram)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
        {
            logger.LogWarning("[MATRIC-ASSIGN] Student not found: {StudentId}", studentId);
            await SendFailureAsync(404, "Student not found", "NOT_FOUND", $"No student found with ID {studentId}", ct);
            return;
        }

        // Check if matric number already assigned to another student
        var existingStudent = await dbContext.Students
            .FirstOrDefaultAsync(s => s.StudentNumber == normalizedMatric && s.Id != studentId, ct);

        if (existingStudent != null)
        {
            logger.LogWarning("[MATRIC-ASSIGN] Matric number {MatricNumber} already assigned to student {ExistingStudentId}", 
                normalizedMatric, existingStudent.Id);
            await SendFailureAsync(409, "Matric number already exists", "DUPLICATE_MATRIC", 
                $"Matric number '{normalizedMatric}' is already assigned to another student", ct);
            return;
        }

        // Check if student already has a matric number
        if (!string.IsNullOrEmpty(student.StudentNumber))
        {
            logger.LogWarning("[MATRIC-ASSIGN] Student {StudentId} already has matric number {ExistingMatric}", 
                studentId, student.StudentNumber);
            await SendFailureAsync(409, "Student already has matric number", "ALREADY_ASSIGNED", 
                $"Student already has matric number '{student.StudentNumber}'. Contact system administrator to change it.", ct);
            return;
        }

        // Assign matric number
        student.StudentNumber = normalizedMatric;
        student.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("[MATRIC-ASSIGN] Successfully assigned matric number {MatricNumber} to student {StudentId}", 
            normalizedMatric, studentId);

        await SendSuccessAsync(new AssignMatricNumberResponse(
            studentId, 
            normalizedMatric, 
            $"Matric number '{normalizedMatric}' successfully assigned to {student.FirstName} {student.LastName}"
        ), ct);
    }
}
