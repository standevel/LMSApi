using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class SubmitCopilotApplicationRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? AcademicProgramId { get; set; }
    public string? ApplicantType { get; set; } = "UTME";
    public string? JambRegNumber { get; set; }
    public int? JambYear { get; set; } = DateTime.UtcNow.Year;
    public int? JambScore { get; set; }
}

public sealed class SubmitCopilotApplicationResponse
{
    public bool Success { get; set; }
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class SubmitCopilotApplicationEndpoint(
    LmsDbContext dbContext,
    IEmailService emailService)
    : ApiEndpoint<SubmitCopilotApplicationRequest, SubmitCopilotApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/copilot/apply");
        AllowAnonymous();
        Tags("Admissions");
        Description(d => d
            .WithName("Submit Copilot Admission Application")
            .WithTags("Admissions")
            .WithSummary("Directly creates and submits an admission application via the AI Copilot assistant"));
    }

    public override async Task HandleAsync(SubmitCopilotApplicationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
        {
            await SendSuccessAsync(new SubmitCopilotApplicationResponse
            {
                Success = false,
                Message = "First Name and Last Name are required."
            }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.StudentEmail) || !req.StudentEmail.Contains('@'))
        {
            await SendSuccessAsync(new SubmitCopilotApplicationResponse
            {
                Success = false,
                Message = "A valid Email address is required."
            }, ct);
            return;
        }

        // Active Academic Session
        var activeSession = await dbContext.AcademicSessions.FirstOrDefaultAsync(s => s.IsActive, ct)
                            ?? await dbContext.AcademicSessions.OrderByDescending(s => s.StartDate).FirstOrDefaultAsync(ct);

        if (activeSession == null)
        {
            await SendSuccessAsync(new SubmitCopilotApplicationResponse
            {
                Success = false,
                Message = "No active academic session found for admission."
            }, ct);
            return;
        }

        var normalizedEmail = req.StudentEmail.Trim().ToLowerInvariant();
        var normalizedJamb = (req.JambRegNumber ?? string.Empty).Trim().ToUpperInvariant();

        // Check if existing application exists
        var existing = await dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .FirstOrDefaultAsync(a => a.StudentEmail.ToLower() == normalizedEmail ||
                                      (!string.IsNullOrEmpty(normalizedJamb) && a.JambRegNumber.ToUpper() == normalizedJamb), ct);

        ApplicantType parsedApplicantType = ApplicantType.UTME;
        if (!string.IsNullOrWhiteSpace(req.ApplicantType) && Enum.TryParse<ApplicantType>(req.ApplicantType, true, out var at))
        {
            parsedApplicantType = at;
        }

        AcademicProgram? selectedProgram = null;
        if (req.AcademicProgramId.HasValue)
        {
            selectedProgram = await dbContext.Programs.FirstOrDefaultAsync(p => p.Id == req.AcademicProgramId.Value, ct);
        }

        var qualData = JsonSerializer.Serialize(new
        {
            JambYear = req.JambYear ?? DateTime.UtcNow.Year,
            JambScore = req.JambScore,
            SubmittedVia = "Copilot AI Assistant"
        });

        AdmissionApplication app;

        if (existing != null)
        {
            app = existing;
            app.FirstName = req.FirstName.Trim();
            app.LastName = req.LastName.Trim();
            app.StudentEmail = normalizedEmail;
            if (!string.IsNullOrWhiteSpace(req.Phone)) app.Phone = req.Phone.Trim();
            if (req.FacultyId.HasValue) app.FacultyId = req.FacultyId;
            if (req.AcademicProgramId.HasValue) app.AcademicProgramId = req.AcademicProgramId;
            app.ApplicantType = parsedApplicantType;
            if (!string.IsNullOrEmpty(normalizedJamb)) app.JambRegNumber = normalizedJamb;
            app.QualificationsJson = qualData;
            app.Status = AdmissionStatus.Submitted;
            app.SubmittedAt = DateTime.UtcNow;
            app.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var year = DateTime.UtcNow.Year;
            var seq = (await dbContext.AdmissionApplications.CountAsync(ct)) + 101;
            var appNo = $"WU{year}/{seq:D4}";

            app = new AdmissionApplication
            {
                Id = Guid.NewGuid(),
                ApplicationNumber = appNo,
                AcademicSessionId = activeSession.Id,
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                StudentEmail = normalizedEmail,
                Phone = req.Phone?.Trim() ?? string.Empty,
                FacultyId = req.FacultyId,
                AcademicProgramId = req.AcademicProgramId,
                ApplicantType = parsedApplicantType,
                JambRegNumber = !string.IsNullOrEmpty(normalizedJamb) ? normalizedJamb : $"PENDING-{seq}",
                QualificationsJson = qualData,
                Status = AdmissionStatus.Submitted,
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.AdmissionApplications.Add(app);
        }

        await dbContext.SaveChangesAsync(ct);

        var programName = selectedProgram?.Name ?? "Wigwe University Academic Program";
        var applicantName = $"{app.FirstName} {app.LastName}";

        // Send confirmation email asynchronously
        try
        {
            await emailService.SendApplicationReminderEmailAsync(app.StudentEmail, applicantName, app.ApplicationNumber, app.Status);
        }
        catch
        {
            // Ignore email errors in endpoint response
        }

        var response = new SubmitCopilotApplicationResponse
        {
            Success = true,
            ApplicationId = app.Id,
            ApplicationNumber = app.ApplicationNumber,
            ApplicantName = applicantName,
            ProgramName = programName,
            Status = app.Status.ToString(),
            Message = $"Congratulations {app.FirstName}! Your admission application #{app.ApplicationNumber} for {programName} has been submitted successfully."
        };

        await SendSuccessAsync(response, ct);
    }
}
