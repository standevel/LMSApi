using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AdmissionService(
    LmsDbContext dbContext,
    IEmailService emailService,
    IActiveDirectoryService adService,
    IPdfService pdfService) : IAdmissionService
{
    public async Task<AdmissionApplication?> VerifyIdentityAsync(string email, string jambRegNumber)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(jambRegNumber))
        {
            return null;
        }

        // Check for existing applications in the latest session or any session
        return await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.StudentEmail == email || a.JambRegNumber == jambRegNumber);
    }

    public async Task<AdmissionApplication> SaveApplicationAsync(AdmissionApplication application, IEnumerable<Guid>? documentIds = null)
    {
        var existing = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == application.Id);

        if (existing == null)
        {
            application.CreatedAt = DateTime.UtcNow;
            if (documentIds?.Any() == true)
            {
                var docs = await dbContext.DocumentRecords
                    .Where(d => documentIds.Contains(d.Id))
                    .ToListAsync();
                foreach (var doc in docs) application.Documents.Add(doc);
            }
            dbContext.AdmissionApplications.Add(application);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(application);
            existing.UpdatedAt = DateTime.UtcNow;

            if (documentIds != null)
            {
                existing.Documents.Clear();
                var docs = await dbContext.DocumentRecords
                    .Where(d => documentIds.Contains(d.Id))
                    .ToListAsync();
                foreach (var doc in docs) existing.Documents.Add(doc);
            }
        }

        await dbContext.SaveChangesAsync();

        if (existing == null)
        {
            // For new applications, reload with navigation properties
            return await dbContext.AdmissionApplications
                .Include(a => a.AcademicSession)
                .Include(a => a.Faculty)
                .Include(a => a.AcademicProgram)
                .Include(a => a.Documents)
                    .ThenInclude(d => d.DocumentType)
                .FirstAsync(a => a.Id == application.Id);
        }

        return existing;
    }

    public async Task<AdmissionApplication> SubmitApplicationAsync(Guid applicationId)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null) throw new KeyNotFoundException("Application not found.");

        // Validation logic
        // Check if all compulsory documents are present
        var requiredDocs = await dbContext.DocumentTypes
            .Where(t => t.Category == DocumentCategory.Admission && t.IsCompulsory && t.IsActive)
            .ToListAsync();

        foreach (var req in requiredDocs)
        {
            if (!app.Documents.Any(d => d.DocumentTypeId == req.Id))
            {
                throw new InvalidOperationException($"Compulsory document '{req.Name}' is missing.");
            }
        }

        if (string.IsNullOrEmpty(app.ApplicationNumber))
        {
            var year = app.AcademicSession?.StartDate.Year ?? DateTime.UtcNow.Year;
            var count = await dbContext.AdmissionApplications
                .CountAsync(a => a.AcademicSessionId == app.AcademicSessionId && !string.IsNullOrEmpty(a.ApplicationNumber));
            app.ApplicationNumber = $"WU-{year}-{(count + 1):D3}";
        }

        app.Status = AdmissionStatus.Submitted;
        app.SubmittedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        // Notification: Application Received
        try
        {
            await emailService.SendApplicationSubmittedEmailAsync(app.StudentEmail, app.StudentName);
        }
        catch { /* Log and continue */ }

        return app;
    }

    public async Task<IEnumerable<AdmissionApplication>> GetHistoryByEmailAsync(string email)
    {
        return await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Where(a => a.StudentEmail == email)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AdmissionApplication>> GetHistoryByJambAsync(string jambRegNumber)
    {
        return await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Where(a => a.JambRegNumber == jambRegNumber)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Faculty>> GetFacultiesAsync()
    {
        return await dbContext.Faculties
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AcademicProgram>> GetProgramsByFacultyAsync(Guid facultyId)
    {
        return await dbContext.Programs
            .AsNoTracking()
            .Where(p => p.FacultyId == facultyId)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AcademicSession>> GetAdmissionSessionsAsync()
    {
        return await dbContext.AcademicSessions
            .AsNoTracking()
            .Where(s => s.IsActive || s.IsAdmissionOpen)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SponsorOrganization>> GetAdmissionSponsorsAsync()
    {
        return await dbContext.SponsorOrganizations
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Subject>> GetAdmissionSubjectsAsync()
    {
        return await dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    // Admin Methods

    public async Task<AdmissionApplication?> GetApplicationByIdAsync(Guid id)
    {
        return await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<AdmissionApplication>> GetApplicationsAsync(AdmissionStatus? status = null, Guid? sessionId = null)
    {
        var query = dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .AsQueryable();

        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (sessionId.HasValue) query = query.Where(a => a.AcademicSessionId == sessionId.Value);

        return await query
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync();
    }

    public async Task<AdmissionApplication> UpdateApplicationStatusAsync(Guid id, AdmissionStatus status)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) throw new KeyNotFoundException("Application not found.");

        var oldStatus = app.Status;
        app.Status = status;
        app.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Workflow Notifications
        if (oldStatus != status)
        {
            await HandleStatusChangeNotificationsAsync(app, status);
        }

        return app;
    }

    private async Task HandleStatusChangeNotificationsAsync(AdmissionApplication app, AdmissionStatus newStatus)
    {
        try
        {
            switch (newStatus)
            {
                case AdmissionStatus.Admitted:
                    var templateType = app.AcademicProgram?.Type switch
                    {
                        LMS.Api.Data.Enums.ProgramType.Postgraduate => "Postgraduate",
                        _ => "Undergraduate"
                    };
                    var pdf = await pdfService.GenerateOfferLetterAsync(app, templateType);
                    await emailService.SendAdmissionOfferEmailAsync(app.StudentEmail, app.StudentName, app.AcademicProgram?.Name ?? "Selected Program", pdf, "Admission_Letter.pdf");
                    break;

                case AdmissionStatus.OfferAccepted:
                    await emailService.SendPaymentInstructionsEmailAsync(app.StudentEmail, app.StudentName);
                    break;

                case AdmissionStatus.FeePaid:
                    // Create AD Account
                    var (officialEmail, tempPassword) = await adService.CreateStudentAccountAsync(app);
                    // Send Credentials
                    await emailService.SendStudentCredentialsEmailAsync(app.StudentEmail, app.StudentName, officialEmail, tempPassword);
                    break;
            }
        }
        catch { /* Log and continue */ }
    }

    public async Task<AcademicProgram> UpdateProgramCriteriaAsync(Guid programId, int minScore, int maxAdmissions, string jambSubjectsJson, string oLevelSubjectsJson)
    {
        var program = await dbContext.Programs.FindAsync(programId);
        if (program == null) throw new KeyNotFoundException("Program not found.");

        program.MinJambScore = minScore;
        program.MaxAdmissions = maxAdmissions;
        program.RequiredJambSubjectsJson = jambSubjectsJson;
        program.RequiredOLevelSubjectsJson = oLevelSubjectsJson;

        await dbContext.SaveChangesAsync();
        return program;
    }

    public async Task<IEnumerable<AutoAdmitResult>> AutoAdmitAsync(Guid sessionId, bool isDryRun)
    {
        var adminResults = new List<AutoAdmitResult>();

        // 1. Get all submitted applications for this session
        var apps = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
            .Where(a => a.AcademicSessionId == sessionId && a.Status == AdmissionStatus.Submitted)
            .ToListAsync();

        // 2. Group by program to respect quotas
        var appsByProgramId = apps.Where(a => a.AcademicProgramId.HasValue).GroupBy(a => a.AcademicProgramId!.Value);

        foreach (var group in appsByProgramId)
        {
            var program = await dbContext.Programs.FindAsync(group.Key);
            if (program == null) continue;

            // Parse applicants and their scores
            var candidates = group.Select(a =>
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(a.QualificationsJson);
                        var score = doc.RootElement.TryGetProperty("jambScore", out var s) ? (s.ValueKind == System.Text.Json.JsonValueKind.Number ? s.GetInt32() : 0) : 0;
                        return new { App = a, Score = score, Quals = doc.RootElement.Clone() };
                    }
                    catch { return new { App = a, Score = 0, Quals = default(System.Text.Json.JsonElement) }; }
                })
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.App.SubmittedAt)
                .ToList();

            int admittedCount = 0;
            foreach (var candidate in candidates)
            {
                bool isAdmitted = false;
                string? reason = null;

                if (admittedCount >= program.MaxAdmissions)
                {
                    reason = "Program quota reached.";
                }
                else if (candidate.Score < program.MinJambScore)
                {
                    reason = $"Below minimum JAMB score of {program.MinJambScore}.";
                }
                else
                {
                    // Basic subject validation placeholder - to be expanded with actual subject logic
                    isAdmitted = true;
                    admittedCount++;
                }

                if (isAdmitted && !isDryRun)
                {
                    var oldStatus = candidate.App.Status;
                    candidate.App.Status = AdmissionStatus.Admitted;
                    candidate.App.UpdatedAt = DateTime.UtcNow;
                    
                    if (oldStatus != AdmissionStatus.Admitted)
                    {
                        await HandleStatusChangeNotificationsAsync(candidate.App, AdmissionStatus.Admitted);
                    }
                }

                adminResults.Add(new AutoAdmitResult(
                    candidate.App.Id,
                    candidate.App.StudentName,
                    program.Name,
                    candidate.Score,
                    isAdmitted,
                    reason
                ));
            }
        }

        if (!isDryRun)
        {
            await dbContext.SaveChangesAsync();
        }

        return adminResults;
    }
}
