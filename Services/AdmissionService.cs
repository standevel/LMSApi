using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class AdmissionService(
    LmsDbContext dbContext,
    IEmailService emailService,
    IActiveDirectoryService adService,
    IPdfService pdfService,
    IConfiguration configuration,
    ILogger<AdmissionService> logger,
    ICreditTransferService creditTransferService,
    IGradeConversionService gradeConversionService,
    ICourseEquivalencyService courseEquivalencyService,
    IGuardianProvisioningService guardianProvisioningService) : IAdmissionService
{
    private readonly ICreditTransferService _creditTransferService = creditTransferService;
    private readonly IGradeConversionService _gradeConversionService = gradeConversionService;
    private readonly ICourseEquivalencyService _courseEquivalencyService = courseEquivalencyService;
    public async Task<AdmissionApplication?> VerifyIdentityAsync(string email, string jambRegNumber)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(jambRegNumber))
        {
            return null;
        }

        try
        {
            // Check for existing applications in the latest session or any session
             return await dbContext.AdmissionApplications
                 .Include(a => a.AcademicSession)
                 .Include(a => a.Faculty)
                 .Include(a => a.AcademicProgram)
                 .Include(a => a.Documents)
                     .ThenInclude(d => d.DocumentType)
                 .OrderByDescending(a => a.CreatedAt)
                 .FirstOrDefaultAsync(a => a.StudentEmail == email || a.JambRegNumber == jambRegNumber.ToUpperInvariant());
        }
        catch (Exception ex)
        {
            // If there's an error loading related entities (likely due to invalid foreign keys),
            // try to load the application without the problematic includes
            logger.LogWarning(ex, "Error loading application with includes, trying without includes for email: {Email}, jamb: {Jamb}", email, jambRegNumber);
            
            try
            {
                 var app = await dbContext.AdmissionApplications
                     .Include(a => a.AcademicSession)
                     .OrderByDescending(a => a.CreatedAt)
                     .FirstOrDefaultAsync(a => a.StudentEmail == email || a.JambRegNumber == jambRegNumber.ToUpperInvariant());
                
                if (app != null)
                {
                    // Clear the invalid foreign key references
                    app.FacultyId = null;
                    app.AcademicProgramId = null;
                    
                    // Save the corrected application
                    await dbContext.SaveChangesAsync();
                    
                    logger.LogInformation("Cleared invalid faculty/program references for application {ApplicationId}", app.Id);
                }
                
                return app;
            }
            catch (Exception innerEx)
            {
                logger.LogError(innerEx, "Failed to load application even without includes for email: {Email}, jamb: {Jamb}", email, jambRegNumber);
                return null;
            }
        }
    }

    public async Task<AdmissionApplication> SaveApplicationAsync(AdmissionApplication application, IEnumerable<Guid>? documentIds = null)
    {
        // Validate FacultyId if provided
        if (application.FacultyId.HasValue)
        {
            var facultyExists = await dbContext.Faculties.AnyAsync(f => f.Id == application.FacultyId.Value);
            if (!facultyExists)
            {
                throw new ArgumentException($"Faculty with ID {application.FacultyId.Value} does not exist.");
            }
        }

        // Validate AcademicProgramId if provided
        if (application.AcademicProgramId.HasValue)
        {
            var programExists = await dbContext.Programs.AnyAsync(p => p.Id == application.AcademicProgramId.Value);
            if (!programExists)
            {
                throw new ArgumentException($"Academic program with ID {application.AcademicProgramId.Value} does not exist.");
            }
        }

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
            // Parse emergency contact JSON into individual fields
            ParseEmergencyContactJson(application);
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
            // Parse emergency contact JSON into individual fields
            ParseEmergencyContactJson(existing);

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

        // Get required documents based on applicant type
        var requiredDocs = await GetRequiredDocumentsAsync(app.ApplicantType, app.AcademicProgramId);

        // Check if all required documents are present
        foreach (var req in requiredDocs)
        {
            if (!app.Documents.Any(d => d.DocumentTypeId == req.Id))
            {
                throw new InvalidOperationException($"Compulsory document '{req.Name}' is missing for {app.ApplicantType} applicants.");
            }
        }

        // Validate applicant-specific requirements (non-document)
        ValidateApplicantSpecificRequirements(app);

        // Validate emergency contact information
        if (string.IsNullOrEmpty(app.EmergencyContactName))
            throw new InvalidOperationException("Emergency contact name is required.");
        if (string.IsNullOrEmpty(app.EmergencyContactPhone))
            throw new InvalidOperationException("Emergency contact phone is required.");
        if (string.IsNullOrEmpty(app.EmergencyContactEmail))
            throw new InvalidOperationException("Emergency contact email is required.");

        if (string.IsNullOrEmpty(app.ApplicationNumber))
        {
            var yearValue = app.AcademicSession?.StartDate.Year ?? DateTime.UtcNow.Year;
            var yearSuffix = (yearValue % 100).ToString("D2");
            var count = await dbContext.AdmissionApplications
                .CountAsync(a => a.AcademicSessionId == app.AcademicSessionId && !string.IsNullOrEmpty(a.ApplicationNumber));
            app.ApplicationNumber = $"WU-{yearSuffix}-{(count + 1):D3}";
        }

        app.Status = AdmissionStatus.Submitted;
        app.SubmittedAt = DateTime.UtcNow;
        app.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        // Notification: Application Received
        try
        {
            var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
            logger.LogInformation("[EMAIL] Attempting to send application submitted email to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
            await emailService.SendApplicationSubmittedEmailAsync(app.StudentEmail, fullName);
            logger.LogInformation("[EMAIL] Application submitted email sent to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[EMAIL-ERROR] Failed to send application submitted email to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
        }

        return app;
    }

    private async Task<IEnumerable<DocumentType>> GetRequiredDocumentsAsync(ApplicantType applicantType, Guid? programId)
    {
        var baseRequired = await dbContext.DocumentTypes
            .Where(t => t.Category == DocumentCategory.Admission && t.IsActive)
            .ToListAsync();

        var required = new List<DocumentType>();

        foreach (var doc in baseRequired)
        {
            bool isRequired = false;

            switch (applicantType)
            {
                case ApplicantType.UTME:
                    isRequired = doc.IsCompulsory && !doc.InternationalOnly && !doc.TransferOnly && !doc.DirectEntryOnly && !doc.ExchangeOnly;
                    break;

                case ApplicantType.DirectEntry:
                    isRequired = (doc.IsCompulsory && !doc.InternationalOnly && !doc.TransferOnly && !doc.ExchangeOnly) || doc.DirectEntryOnly;
                    break;

                case ApplicantType.Transfer:
                    isRequired = (doc.IsCompulsory && !doc.InternationalOnly && !doc.DirectEntryOnly) || doc.TransferOnly || doc.ExchangeOnly;
                    break;

                case ApplicantType.International:
                    isRequired = (doc.IsCompulsory && !doc.NigeriaOnly && !doc.TransferOnly && !doc.DirectEntryOnly && !doc.ExchangeOnly) || doc.InternationalOnly;
                    break;
            }

            if (isRequired)
            {
                required.Add(doc);
            }
        }

        return required;
    }

    private void ValidateApplicantSpecificRequirements(AdmissionApplication app)
    {
        switch (app.ApplicantType)
        {
            case ApplicantType.International:
                if (string.IsNullOrEmpty(app.PassportNumber))
                    throw new InvalidOperationException("Passport number is required for international applicants.");
                if (string.IsNullOrEmpty(app.EnglishProficiencyScore) || !app.EnglishProficiencyType.HasValue)
                    throw new InvalidOperationException("English proficiency test score and type are required for international applicants.");
                if (string.IsNullOrEmpty(app.Nationality))
                    throw new InvalidOperationException("Nationality is required for international applicants.");
                
                // Enhancement: Visa and financial proof validation for international students
                if (app.VisaRequired == true && string.IsNullOrEmpty(app.VisaApplicationNumber))
                    throw new InvalidOperationException("Visa application number is required when visa is required.");
                if (app.FinancialProofProvided == true && 
                    (!app.FinancialProofAmount.HasValue || string.IsNullOrEmpty(app.FinancialProofCurrency)))
                    throw new InvalidOperationException("Financial proof amount and currency are required when financial proof is provided.");
                break;

            case ApplicantType.Transfer:
                if (!app.PreviousCGPA.HasValue)
                    throw new InvalidOperationException("Previous CGPA is required for transfer applicants.");
                if (!app.CreditsEarned.HasValue)
                    throw new InvalidOperationException("Credits earned is required for transfer applicants.");
                if (string.IsNullOrEmpty(app.PreviousInstitutionName))
                    throw new InvalidOperationException("Previous institution name is required for transfer applicants.");
                break;

            case ApplicantType.DirectEntry:
                if (!app.StartingLevelId.HasValue)
                    throw new InvalidOperationException("Starting level is required for direct entry applicants.");
                break;
        }
    }

    /// <summary>
    /// Validates O-Level subjects from qualifications JSON.
    /// </summary>
    private bool ValidateOLevelSubjects(System.Text.Json.JsonElement quals, string? requiredSubjectsJson)
    {
        try
        {
            var requiredSubjects = System.Text.Json.JsonSerializer
                .Deserialize<List<string>>(requiredSubjectsJson ?? "[]")
                ?? new List<string>();

            if (requiredSubjects.Count == 0)
                return true;

            if (quals.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;

            if (!quals.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;

            var creditGrades = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A1", "B2", "B3", "C4", "C5", "C6" };
            var applicantResults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in resultsEl.EnumerateArray())
            {
                var subj = r.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "";
                var grade = r.TryGetProperty("grade", out var g) ? g.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(subj))
                    applicantResults[subj] = grade;
            }

            foreach (var required in requiredSubjects)
            {
                if (!applicantResults.TryGetValue(required, out var grade) || !creditGrades.Contains(grade))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if direct entry qualification data exists in qualifications JSON.
    /// </summary>
    private bool HasDirectEntryQualification(System.Text.Json.JsonElement quals)
    {
        try
        {
            if (quals.ValueKind != System.Text.Json.JsonValueKind.Object)
                return false;

            var hasALevel = quals.TryGetProperty("aLevelResults", out var aLevel) && aLevel.ValueKind == System.Text.Json.JsonValueKind.Array;
            var hasIJMB = quals.TryGetProperty("ijmbResults", out var ijmb) && ijmb.ValueKind == System.Text.Json.JsonValueKind.Array;
            var hasNAPLEX = quals.TryGetProperty("naplexResults", out var naplex) && naplex.ValueKind == System.Text.Json.JsonValueKind.Array;

            return hasALevel || hasIJMB || hasNAPLEX;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses English proficiency test score to a decimal for comparison.
    /// </summary>
    private decimal ParseEnglishProficiencyScore(string? scoreStr, EnglishProficiencyType? type)
    {
        if (string.IsNullOrWhiteSpace(scoreStr) || !type.HasValue)
            return 0;

        if (decimal.TryParse(scoreStr, out var score))
        {
            return score;
        }

        return 0;
    }

    /// <summary>
    /// Validates English proficiency score meets minimum requirements.
    /// </summary>
    private bool ValidateEnglishProficiency(string? scoreStr, EnglishProficiencyType? type, decimal minTOEFL, decimal minIELTS)
    {
        if (!type.HasValue || string.IsNullOrWhiteSpace(scoreStr))
            return false;

        if (!decimal.TryParse(scoreStr, out var score))
            return false;

        return type.Value switch
        {
            EnglishProficiencyType.TOEFL => score >= minTOEFL,
            EnglishProficiencyType.IELTS => score >= minIELTS,
            EnglishProficiencyType.PTE => score >= 50,
            EnglishProficiencyType.Other => true,
            _ => false
        };
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
             .Where(a => a.JambRegNumber == jambRegNumber.ToUpperInvariant())
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
            .Where(p => p.Department.FacultyId == facultyId && p.ParentProgramId == null)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Department>> GetDepartmentsByFacultyAsync(Guid facultyId)
    {
        return await dbContext.Departments
            .AsNoTracking()
            .Include(d => d.Faculty)
            .Where(d => d.FacultyId == facultyId)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AcademicProgram>> GetProgramsByDepartmentAsync(Guid departmentId)
    {
        return await dbContext.Programs
            .AsNoTracking()
            .Where(p => p.DepartmentId == departmentId && p.ParentProgramId == null)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AcademicSession>> GetAdmissionSessionsAsync()
    {
        return await dbContext.AcademicSessions
            .AsNoTracking()
            .Where(s => s.IsAdmissionOpen)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();
    }

    public async Task<AcademicSession?> GetActiveAdmissionSessionAsync()
    {
        return await dbContext.AcademicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsAdmissionActive)
            ?? await dbContext.AcademicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive);
    }

    public async Task<IEnumerable<SponsorOrganization>> GetAdmissionSponsorsAsync()
    {
        return await dbContext.SponsorOrganizations
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<SponsorOrganization> CreateSponsorAsync(
        string name,
        string? email = null,
        string? phone = null,
        CancellationToken ct = default)
    {
        // Normalize name: title-case, trim, collapse whitespace
        var normalized = Regex.Replace(name.Trim(), @"\s+", " ");
        var titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());

        // Check for existing (case-insensitive)
        var existing = await dbContext.SponsorOrganizations
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.Name, titleCase), ct);

        if (existing is not null)
            return existing;

        // Generate code: uppercase initials/abbreviation
        var words = titleCase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = words.Length switch
        {
            1 => words[0].ToUpper(),
            2 => string.Concat(words.Select(w => w[0])).ToUpper(),
            _ => string.Concat(words.Select(w => w[0])).ToUpper()
        };

        // Ensure uniqueness
        var finalCode = code;
        var idx = 1;
        while (await dbContext.SponsorOrganizations.AnyAsync(s => s.Code == finalCode, ct))
        {
            finalCode = $"{code}{idx}";
            idx++;
        }

        var org = new SponsorOrganization
        {
            Name = titleCase,
            Code = finalCode,
            Email = email,
            Phone = phone,
            IsActive = true
        };

        dbContext.SponsorOrganizations.Add(org);
        await dbContext.SaveChangesAsync(ct);
        return org;
    }

    public async Task<IEnumerable<Subject>> GetAdmissionSubjectsAsync()
    {
        return await dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<AcademicLevel>> GetAcademicLevelsAsync()
    {
        return await dbContext.Levels
            .AsNoTracking()
            .Include(l => l.Program)
            .OrderBy(l => l.Program.Name)
            .ThenBy(l => l.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentType>> GetRequiredDocumentTypesAsync(ApplicantType applicantType, Guid? programId = null)
    {
        var baseRequired = await dbContext.DocumentTypes
            .Where(t => t.Category == DocumentCategory.Admission && t.IsActive)
            .ToListAsync();

        var required = new List<DocumentType>();

        foreach (var doc in baseRequired)
        {
            bool isRequired = false;

            switch (applicantType)
            {
                case ApplicantType.UTME:
                    isRequired = doc.IsCompulsory && !doc.InternationalOnly && !doc.TransferOnly && !doc.DirectEntryOnly && !doc.ExchangeOnly;
                    break;

                case ApplicantType.DirectEntry:
                    isRequired = (doc.IsCompulsory && !doc.InternationalOnly && !doc.TransferOnly && !doc.ExchangeOnly) || doc.DirectEntryOnly;
                    break;

                case ApplicantType.Transfer:
                    isRequired = (doc.IsCompulsory && !doc.InternationalOnly && !doc.DirectEntryOnly) || doc.TransferOnly || doc.ExchangeOnly;
                    break;

                case ApplicantType.International:
                    isRequired = (doc.IsCompulsory && !doc.NigeriaOnly && !doc.TransferOnly && !doc.DirectEntryOnly && !doc.ExchangeOnly) || doc.InternationalOnly;
                    break;
            }

            if (isRequired)
            {
                required.Add(doc);
            }
        }

        return required;
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

    public async Task<AdmissionApplication> UpdateApplicationStatusAsync(Guid id, AdmissionStatus status, Guid? updatedBy = null)
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
        
        if (oldStatus != status)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "Update Status",
                EntityName = nameof(AdmissionApplication),
                EntityId = app.Id.ToString(),
                Changes = $"Status changed from {oldStatus} to {status}",
                UserId = updatedBy
            });
        }
        
        await dbContext.SaveChangesAsync();

        // Workflow Notifications
        if (oldStatus != status)
        {
            await HandleStatusChangeNotificationsAsync(app, status, updatedBy);
        }

        return app;
    }

    public async Task<AdmissionApplication> RespondToOfferAsync(Guid id, bool acceptOffer)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents)
                .ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) throw new KeyNotFoundException("Application not found.");

        var targetStatus = acceptOffer ? AdmissionStatus.OfferAccepted : AdmissionStatus.Rejected;
        if (app.Status == targetStatus)
        {
            return app;
        }

        if (app.Status != AdmissionStatus.Admitted)
        {
            throw new InvalidOperationException("Only admitted applications can accept or reject an admission offer.");
        }

        app.Status = targetStatus;
        app.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        await HandleStatusChangeNotificationsAsync(app, targetStatus);
        return app;
    }

    public async Task<AdmissionApplication> ResendOfferLetterAsync(Guid applicationId, Guid? updatedBy = null, CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents).ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
            throw new KeyNotFoundException("Application not found.");

        if (app.Status != AdmissionStatus.Admitted && app.Status != AdmissionStatus.OfferAccepted)
            throw new InvalidOperationException($"Offer letter can only be resent for applications with status 'Admitted' or 'OfferAccepted'. Current status: {app.Status}.");

        var templateType = app.AcademicProgram?.Type switch
        {
            LMS.Api.Data.Enums.ProgramType.Postgraduate => "Postgraduate",
            _ => "Undergraduate"
        };

        var pdf = await pdfService.GenerateOfferLetterAsync(app, templateType);
        var memoPdf = await pdfService.GenerateAdvancePaymentMemoAsync();
        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();

        await emailService.SendAdmissionOfferEmailAsync(
            toEmail: app.StudentEmail,
            studentName: fullName,
            programName: app.AcademicProgram?.Name ?? "Selected Program",
            pdfAttachment: pdf,
            fileName: "Admission_Letter.pdf",
            secondAttachment: memoPdf,
            secondFileName: "Advance_Payment_Memo.pdf"
        );

        // Refresh offer expiry to give another 14 days from resend
        app.OfferExpiresAt = DateTime.UtcNow.AddDays(14);
        app.UpdatedAt = DateTime.UtcNow;

        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "Resend Offer Letter",
            EntityName = nameof(AdmissionApplication),
            EntityId = app.Id.ToString(),
            Changes = $"Offer letter resent to {app.StudentEmail}. New expiry: {app.OfferExpiresAt:yyyy-MM-dd}",
            UserId = updatedBy
        });

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("[RESEND-OFFER] Offer letter resent to {Email} for application {Id}", app.StudentEmail, app.Id);

        return app;
    }

    public async Task<AdmissionApplication> UndoRejectionAsync(Guid applicationId, Guid? updatedBy = null, CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .Include(a => a.AcademicProgram)
            .Include(a => a.Documents).ThenInclude(d => d.DocumentType)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
            throw new KeyNotFoundException("Application not found.");

        if (app.Status != AdmissionStatus.Rejected)
            throw new InvalidOperationException($"Only rejected applications can be restored. Current status: {app.Status}.");

        var previousStatus = AdmissionStatus.UnderReview;
        app.Status = previousStatus;
        app.UpdatedAt = DateTime.UtcNow;

        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "Undo Rejection",
            EntityName = nameof(AdmissionApplication),
            EntityId = app.Id.ToString(),
            Changes = $"Application restored from Rejected → {previousStatus}",
            UserId = updatedBy
        });

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("[UNDO-REJECTION] Application {Id} restored from Rejected to {Status}", app.Id, previousStatus);

        return app;
    }

    private async Task HandleStatusChangeNotificationsAsync(AdmissionApplication app, AdmissionStatus newStatus, Guid? updatedBy = null)
    {
        try
        {
            switch (newStatus)
            {
                case AdmissionStatus.Admitted:
                    try
                    {
                        // Set offer expiration (e.g., 14 days from admission)
                        app.OfferExpiresAt = DateTime.UtcNow.AddDays(14);
                        logger.LogInformation("[ADMITTED] Application {ApplicationId} admitted. Offer expires at {OfferExpiresAt}", app.Id, app.OfferExpiresAt);
                        
                        var applicantPortalBaseUrl = configuration["ClientApp:BaseUrl"] ?? "http://localhost:4200";
                        var offerDecisionBaseUrl = applicantPortalBaseUrl.TrimEnd('/');
                        var offerDecisionUrl = $"{offerDecisionBaseUrl}/apply/offer/{app.Id}";
                        var templateType = app.AcademicProgram?.Type switch
                        {
                            LMS.Api.Data.Enums.ProgramType.Postgraduate => "Postgraduate",
                            _ => "Undergraduate"
                        };
                        var pdf = await pdfService.GenerateOfferLetterAsync(app, templateType);
                        var memoPdf = await pdfService.GenerateAdvancePaymentMemoAsync();
                        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
                        await emailService.SendAdmissionOfferEmailAsync(
                            toEmail: app.StudentEmail,
                            studentName: fullName,
                            programName: app.AcademicProgram?.Name ?? "Selected Program",
                            pdfAttachment: pdf,
                            fileName: "Admission_Letter.pdf",
                            secondAttachment: memoPdf,
                            secondFileName: "Advance_Payment_Memo.pdf"
                        );
                        
                        dbContext.AuditLogs.Add(new AuditLog
                        {
                            Action = "Send Offer Letter",
                            EntityName = nameof(AdmissionApplication),
                            EntityId = app.Id.ToString(),
                            Changes = $"Sent offer letter for program {app.AcademicProgram?.Name}",
                            UserId = updatedBy
                        });
                        await dbContext.SaveChangesAsync();
                        
                        logger.LogInformation("[ADMITTED] Admission offer email sent to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[ADMITTED-ERROR] Failed to send admission offer email to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
                        throw;
                    }
                    break;

                case AdmissionStatus.OfferAccepted:
                    logger.LogInformation("[OFFER-ACCEPTED] Student accepted offer for application {ApplicationId}, student {StudentEmail}. Account creation pending Registrar action.",
                        app.Id, app.StudentEmail);

                    // Check offer expiration
                    if (app.OfferExpiresAt.HasValue && DateTime.UtcNow > app.OfferExpiresAt.Value)
                    {
                        logger.LogWarning("[OFFER-ACCEPTED-EXPIRED] Offer for application {ApplicationId} has expired. Expired at {ExpiredAt}, Current time {CurrentTime}",
                            app.Id, app.OfferExpiresAt.Value, DateTime.UtcNow);
                        throw new InvalidOperationException($"The admission offer has expired on {app.OfferExpiresAt.Value:yyyy-MM-dd}. Please contact admissions office.");
                    }

                    // Record acceptance timestamp
                    app.OfferAcceptedAt = DateTime.UtcNow;

                    // Send confirmation email to student
                    try
                    {
                        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
                        await emailService.SendOfferAcceptedConfirmationAsync(app.StudentEmail, fullName, app.AcademicProgram?.Name ?? "Selected Program");
                        logger.LogInformation("[OFFER-ACCEPTED] Confirmation email sent to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[OFFER-ACCEPTED] Failed to send confirmation email to {Email} for application {ApplicationId}", app.StudentEmail, app.Id);
                        // Don't throw - email can be resent manually
                    }

                    // Note: Entra ID account creation and Student record creation are now handled
                    // separately by the Registrar via CreateStudentAccount endpoint
                    logger.LogInformation("[OFFER-ACCEPTED] Offer acceptance recorded for application {ApplicationId} at {AcceptedAt}. Student account will be created by Registrar.", 
                        app.Id, app.OfferAcceptedAt);
                    break;

                case AdmissionStatus.FeePaid:
                    // Payment confirmed - update student records if needed
                    logger.LogInformation("Fee payment confirmed for application {ApplicationId}", app.Id);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing status change notification for application {ApplicationId} to status {NewStatus}", app.Id, newStatus);
            throw;
        }
    }

    private async Task<decimal> CalculateProgramFeeAsync(Guid programId, Guid sessionId)
    {
        // Get all active fee assignments for this program and session
        var assignments = await dbContext.FeeAssignments
            .Include(a => a.FeeTemplate).ThenInclude(t => t.LineItems)
            .Where(a => a.IsActive &&
                (a.SessionId == null || a.SessionId == sessionId) &&
                (a.Scope == LMS.Api.Data.Enums.FeeScope.University ||
                 (a.Scope == LMS.Api.Data.Enums.FeeScope.Program && a.ProgramId == programId)))
            .ToListAsync();

        // Calculate total: sum of line items or amount override (program-level assignment takes precedence)
        var programAssignment = assignments.FirstOrDefault(a => a.Scope == LMS.Api.Data.Enums.FeeScope.Program);
        if (programAssignment != null)
        {
            return programAssignment.AmountOverride ?? programAssignment.FeeTemplate.LineItems.Sum(li => li.Amount);
        }

        // Fall back to university-level assignments
        decimal total = 0;
        foreach (var assignment in assignments.Where(a => a.Scope == LMS.Api.Data.Enums.FeeScope.University))
        {
            total += assignment.AmountOverride ?? assignment.FeeTemplate.LineItems.Sum(li => li.Amount);
        }

        return total;
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

    public async Task<TransferValidationResult> ValidateTransferEligibilityAsync(Guid applicationId)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
        {
            return new TransferValidationResult(false, "Application not found.", null, null, null, null);
        }

        if (app.ApplicantType != ApplicantType.Transfer)
        {
            return new TransferValidationResult(false, "Application is not a transfer application.", null, null, null, null);
        }

        // Thresholds from configuration (fallback to defaults if missing)
        var minCGPA4Scale = configuration.GetValue<decimal>("AdmissionSettings:Transfer:MinCGPA4Scale", 2.5m);
        var minCGPA5Scale = configuration.GetValue<decimal>("AdmissionSettings:Transfer:MinCGPA5Scale", 3.0m);
        var minCredits = configuration.GetValue<int>("AdmissionSettings:Transfer:MinCredits", 30);

        var errors = new List<string>();

        if (!app.PreviousCGPA.HasValue)
        {
            errors.Add("Previous CGPA is required.");
        }
        else
        {
            // Assume 4.0 scale if CGPA <= 4.0, else assume 5.0 scale
            if (app.PreviousCGPA <= 4.0m && app.PreviousCGPA < minCGPA4Scale)
            {
                errors.Add($"Previous CGPA {app.PreviousCGPA} is below minimum requirement of {minCGPA4Scale}/4.0.");
            }
            else if (app.PreviousCGPA > 4.0m && app.PreviousCGPA < minCGPA5Scale)
            {
                errors.Add($"Previous CGPA {app.PreviousCGPA} is below minimum requirement of {minCGPA5Scale}/5.0.");
            }
        }

        if (!app.CreditsEarned.HasValue)
        {
            errors.Add(" Credits earned is required.");
        }
        else if (app.CreditsEarned < minCredits)
        {
            errors.Add($"Credits earned ({app.CreditsEarned}) is below minimum requirement of {minCredits}.");
        }

        if (string.IsNullOrEmpty(app.PreviousInstitutionName))
        {
            errors.Add("Previous institution name is required.");
        }

        // Determine eligible starting level based on credits and program
        Guid? eligibleLevelId = null;
        string? eligibleLevelName = null;

        if (app.AcademicProgramId.HasValue && app.CreditsEarned.HasValue)
        {
            var levels = await dbContext.Levels
                .Where(l => l.ProgramId == app.AcademicProgramId.Value)
                .OrderBy(l => l.Order)
                .ToListAsync();

            // Map credits to level: roughly 30 credits per level
            int estimatedLevel = Math.Min((app.CreditsEarned.Value / 30) + 1, levels.Count);
            if (levels.Count >= estimatedLevel && estimatedLevel > 1)
            {
                eligibleLevelId = levels[estimatedLevel - 1].Id;
                eligibleLevelName = levels[estimatedLevel - 1].Name;
            }
        }

        if (errors.Any())
        {
            return new TransferValidationResult(
                false,
                string.Join(" ", errors),
                minCGPA4Scale,
                minCredits,
                eligibleLevelId,
                eligibleLevelName
            );
        }

        return new TransferValidationResult(
            true,
            null,
            minCGPA4Scale,
            minCredits,
            eligibleLevelId,
            eligibleLevelName
        );
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
                .ThenInclude(d => d.DocumentType)
            .Include(a => a.StartingLevel)
            .Where(a => a.AcademicSessionId == sessionId && a.Status == AdmissionStatus.Submitted)
            .ToListAsync();

        // 2. Group by program to respect quotas
        var appsByProgramId = apps.Where(a => a.AcademicProgramId.HasValue).GroupBy(a => a.AcademicProgramId!.Value);

        foreach (var group in appsByProgramId)
        {
            var program = await dbContext.Programs.FindAsync(group.Key);
            if (program == null) continue;

            // Categorize applicants by type
            var utmeCandidates = new List<(AdmissionApplication App, decimal Score, System.Text.Json.JsonElement Quals)>();
            var directEntryCandidates = new List<(AdmissionApplication App, decimal Score, System.Text.Json.JsonElement Quals)>();
            var transferCandidates = new List<(AdmissionApplication App, decimal Score, System.Text.Json.JsonElement Quals)>();
            var internationalCandidates = new List<(AdmissionApplication App, decimal Score, System.Text.Json.JsonElement Quals)>();

            foreach (var app in group)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(app.QualificationsJson);
                    var root = doc.RootElement.Clone();

                    decimal score = 0;
                    switch (app.ApplicantType)
                    {
                        case ApplicantType.UTME:
                            score = root.TryGetProperty("jambScore", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.Number ? s.GetInt32() : 0;
                            utmeCandidates.Add((app, score, root));
                            break;

                        case ApplicantType.DirectEntry:
                            // Use A-Level points or equivalent; default to 0 if not present
                            score = root.TryGetProperty("aLevelPoints", out var pts) && pts.ValueKind == System.Text.Json.JsonValueKind.Number ? pts.GetDecimal() : 0;
                            directEntryCandidates.Add((app, score, root));
                            break;

                        case ApplicantType.Transfer:
                            // Use PreviousCGPA as score (higher is better)
                            score = app.PreviousCGPA ?? 0;
                            transferCandidates.Add((app, score, root));
                            break;

                        case ApplicantType.International:
                            // Use English proficiency score (convert if needed); higher is better
                            score = ParseEnglishProficiencyScore(app.EnglishProficiencyScore, app.EnglishProficiencyType);
                            internationalCandidates.Add((app, score, root));
                            break;
                    }
                }
                catch
                {
                    // If parsing fails, assign zero score and continue
                    switch (app.ApplicantType)
                    {
                        case ApplicantType.UTME: utmeCandidates.Add((app, 0, default)); break;
                        case ApplicantType.DirectEntry: directEntryCandidates.Add((app, 0, default)); break;
                        case ApplicantType.Transfer: transferCandidates.Add((app, 0, default)); break;
                        case ApplicantType.International: internationalCandidates.Add((app, 0, default)); break;
                    }
                }
            }

            // Define thresholds
            const decimal minEnglishTOEFL = 80m;
            const decimal minEnglishIELTS = 6.5m;

            // Process each category separately but respect overall program quota
            int totalAdmitted = 0;

            // Combine all candidates with their score and validation logic
            var allCandidates = new List<(AdmissionApplication App, decimal Score, System.Text.Json.JsonElement Quals, string ApplicantType)>();
            allCandidates.AddRange(utmeCandidates.Select(c => (c.App, c.Score, c.Quals, "UTME")));
            allCandidates.AddRange(directEntryCandidates.Select(c => (c.App, c.Score, c.Quals, "DirectEntry")));
            allCandidates.AddRange(transferCandidates.Select(c => (c.App, c.Score, c.Quals, "Transfer")));
            allCandidates.AddRange(internationalCandidates.Select(c => (c.App, c.Score, c.Quals, "International")));

            // Sort by score descending, then by submission date
            var sortedCandidates = allCandidates
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.App.SubmittedAt)
                .ToList();

            foreach (var candidate in sortedCandidates)
            {
                bool isAdmitted = false;
                string? reason = null;

                if (totalAdmitted >= program.MaxAdmissions)
                {
                    reason = "Program quota reached.";
                }
                else
                {
                    switch (candidate.ApplicantType)
                    {
                        case "UTME":
                            {
                                // JAMB score check
                                if (candidate.Score < program.MinJambScore)
                                {
                                    reason = $"Below minimum JAMB score of {program.MinJambScore}.";
                                }
                                else
                                {
                                    // O-Level subject validation
                                    bool oLevelPass = ValidateOLevelSubjects(candidate.Quals, program.RequiredOLevelSubjectsJson);
                                    if (!oLevelPass)
                                    {
                                        reason = "Missing required O-Level subjects or grades.";
                                    }
                                    else
                                    {
                                        isAdmitted = true;
                                    }
                                }
                                break;
                            }

                        case "DirectEntry":
                            {
                                // Check StartingLevelId is set and valid
                                if (!candidate.App.StartingLevelId.HasValue)
                                {
                                    reason = "Starting level is required for direct entry.";
                                }
                                else
                                {
                                    // Validate A-Level/IB/NAPLEX results exist in Qualifications
                                    bool hasQuals = HasDirectEntryQualification(candidate.Quals);
                                    if (!hasQuals)
                                    {
                                        reason = "Missing required A-Level/IJMB/NAPLEX results.";
                                    }
                                    else if (candidate.Score < 10) // Arbitrary minimum points threshold
                                    {
                                        reason = "Direct entry qualification score below minimum threshold.";
                                    }
                                    else
                                    {
                                        isAdmitted = true;
                                    }
                                }
                                break;
                            }

                        case "Transfer":
                            {
                                // Transfer eligibility already validated on submission, but double-check
                                var validation = await ValidateTransferEligibilityAsync(candidate.App.Id);
                                if (!validation.IsEligible)
                                {
                                    reason = validation.Reason ?? "Transfer eligibility not met.";
                                }
                                else
                                {
                                    isAdmitted = true;
                                }
                                break;
                            }

                        case "International":
                            {
                                // Validate English proficiency
                                bool englishOk = ValidateEnglishProficiency(candidate.App.EnglishProficiencyScore, candidate.App.EnglishProficiencyType, minEnglishTOEFL, minEnglishIELTS);
                                if (!englishOk)
                                {
                                    reason = "English proficiency score does not meet minimum requirement.";
                                }
                                else if (string.IsNullOrEmpty(candidate.App.PassportNumber))
                                {
                                    reason = "Passport number is required.";
                                }
                                else
                                {
                                    isAdmitted = true;
                                }
                                break;
                            }
                    }
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
                    candidate.App.FirstName,
                    candidate.App.LastName,
                    candidate.App.MiddleName,
                    program.Name,
                    (int)candidate.Score,
                    isAdmitted,
                    reason
                ));

                if (isAdmitted) totalAdmitted++;
            }
        }

        if (!isDryRun)
        {
            await dbContext.SaveChangesAsync();
        }

        return adminResults;
    }

    /// <summary>
    /// Creates a student account for an accepted admission application.
    /// This is called by the Registrar to manually trigger account creation.
    /// </summary>
    public async Task<StudentAccountCreationResult> CreateStudentAccountAsync(Guid applicationId, Guid? updatedBy = null, CancellationToken ct = default)
    {
        logger.LogInformation("[REGISTRAR-ACCOUNT-CREATION] Starting account creation for application {ApplicationId}", applicationId);

        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .Include(a => a.AcademicSession)
            .Include(a => a.Faculty)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            logger.LogWarning("[REGISTRAR-ACCOUNT-CREATION] Application not found: {ApplicationId}", applicationId);
            return new StudentAccountCreationResult { Success = false, ErrorMessage = "Application not found" };
        }

        if (app.Status != AdmissionStatus.OfferAccepted)
        {
            logger.LogWarning("[REGISTRAR-ACCOUNT-CREATION] Application {ApplicationId} has invalid status: {Status}. Expected: OfferAccepted", 
                applicationId, app.Status);
            return new StudentAccountCreationResult { Success = false, ErrorMessage = $"Application status is {app.Status}. Only OfferAccepted applications can have accounts created." };
        }

        // Check if already processed (either Entra ID or Student record exists)
        if (!string.IsNullOrEmpty(app.EntraObjectId) || app.StudentId.HasValue)
        {
            logger.LogInformation("[REGISTRAR-ACCOUNT-CREATION] Application {ApplicationId} already has account. EntraId={EntraId}, StudentId={StudentId}",
                applicationId, app.EntraObjectId, app.StudentId);
            return new StudentAccountCreationResult { Success = false, ErrorMessage = "Student account already exists for this application", 
                StudentId = app.StudentId, OfficialEmail = app.OfficialEmail };
        }

        try
        {
            // Note: Manual transactions are not supported with SqlServerRetryingExecutionStrategy.
            // We rely on idempotency checks and multiple SaveChanges calls for data consistency.
            
            // Create AD Account
            logger.LogInformation("[REGISTRAR-USER-CREATION] Creating Entra ID account for {StudentEmail}", app.StudentEmail);
            var (entraObjectId, officialEmail, tempPassword, isExisting) = await adService.CreateStudentAccountAsync(app);
            logger.LogInformation("[REGISTRAR-USER-CREATION] Account created: ObjectId={EntraId}, Email={OfficialEmail}, IsExisting={IsExisting}",
                entraObjectId, officialEmail, isExisting);

            // Update application
            app.EntraObjectId = entraObjectId;
            app.OfficialEmail = officialEmail;
            app.AccountCreatedAt = DateTime.UtcNow;

            // Create Student record
            logger.LogInformation("[REGISTRAR-STUDENT-CREATION] Creating Student record for application {ApplicationId}", app.Id);
            var student = new Student
            {
                Id = Guid.NewGuid(),
                AdmissionApplicationId = app.Id,
                EntraObjectId = entraObjectId,
                OfficialEmail = officialEmail,
                FirstName = app.FirstName,
                LastName = app.LastName,
                MiddleName = app.MiddleName,
                PersonalEmail = app.StudentEmail,
                Phone = app.Phone,
                EmergencyContactName = app.EmergencyContactName,
                EmergencyContactPhone = app.EmergencyContactPhone,
                EmergencyContactEmail = app.EmergencyContactEmail,
                AcademicSessionId = app.AcademicSessionId,
                FacultyId = app.FacultyId,
                AcademicProgramId = app.AcademicProgramId,
                StudentNumber = null, // Matric number assigned by Registrar later
                Status = StudentStatus.Active,
                EnrollmentDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Students.Add(student);
            await dbContext.SaveChangesAsync(ct);

            app.StudentId = student.Id;
            
            dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "Create Student Account",
                EntityName = nameof(AdmissionApplication),
                EntityId = app.Id.ToString(),
                Changes = $"Created Entra ID account and Student record (StudentId: {student.Id})",
                UserId = updatedBy
            });
            
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("[REGISTRAR-STUDENT-CREATION] Student record created: StudentId={StudentId}", student.Id);

            if (await guardianProvisioningService.AutoCreateGuardianAccountsEnabledAsync(ct)
                && !string.IsNullOrWhiteSpace(student.EmergencyContactEmail))
            {
                await guardianProvisioningService.ProvisionForStudentAsync(student, ct: ct);
                logger.LogInformation("[REGISTRAR-GUARDIAN-CREATION] Guardian provisioning completed for student {StudentId}", student.Id);
            }

            // Calculate fees
            var amountDue = await CalculateProgramFeeAsync(app.AcademicProgramId ?? Guid.Empty, app.AcademicSessionId);

            // Get payment page URL
            var paymentPortalBaseUrl = configuration["ClientApp:BaseUrl"] ?? "http://localhost:4200";
            var paymentPageUrl = $"{paymentPortalBaseUrl.TrimEnd('/')}/student/payment";

            // Send emails
            var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();

            // Send credentials for new accounts, or notification for existing accounts
            try
            {
                if (!isExisting && !string.IsNullOrEmpty(tempPassword))
                {
                    await emailService.SendStudentCredentialsEmailAsync(app.StudentEmail, fullName, officialEmail, tempPassword);
                    logger.LogInformation("[REGISTRAR-EMAIL] Credentials email sent to {Email}", app.StudentEmail);
                }
                else if (isExisting)
                {
                    // Account already exists - send notification with password reset info
                    await emailService.SendExistingAccountNotificationAsync(app.StudentEmail, fullName, officialEmail);
                    logger.LogInformation("[REGISTRAR-EMAIL] Existing account notification sent to {Email}", app.StudentEmail);
                }
                else
                {
                    logger.LogWarning("[REGISTRAR-EMAIL] No credentials email sent - unknown state. isExisting={IsExisting}, hasPassword={HasPassword}",
                        isExisting, !string.IsNullOrEmpty(tempPassword));
                }
            }
            catch (Exception emailEx)
            {
                logger.LogError(emailEx, "[REGISTRAR-EMAIL] Failed to send credentials/notification email to {Email}", app.StudentEmail);
            }

            try
            {
                await emailService.SendPaymentInstructionsEmailAsync(app.StudentEmail, fullName, amountDue, paymentPageUrl);
                logger.LogInformation("[REGISTRAR-EMAIL] Payment instructions sent to {Email}", app.StudentEmail);
            }
            catch (Exception emailEx)
            {
                logger.LogError(emailEx, "[REGISTRAR-EMAIL] Failed to send payment email to {Email}", app.StudentEmail);
            }

            logger.LogInformation("[REGISTRAR-ACCOUNT-CREATION] Successfully completed for application {ApplicationId}. StudentId={StudentId}",
                applicationId, student.Id);

            return new StudentAccountCreationResult
            {
                Success = true,
                StudentId = student.Id,
                OfficialEmail = officialEmail,
                TemporaryPassword = tempPassword,
                IsExistingAccount = isExisting,
                AmountDue = amountDue
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[REGISTRAR-ACCOUNT-CREATION] Failed for application {ApplicationId}", applicationId);
            return new StudentAccountCreationResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Gets list of accepted applications that don't have student accounts yet.
    /// For Registrar dashboard.
    /// </summary>
    public async Task<List<PendingStudentAccountDto>> GetPendingStudentAccountsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("[REGISTRAR-PENDING] Fetching pending student accounts");

        var pending = await dbContext.AdmissionApplications
            .Where(a => a.Status == AdmissionStatus.Admitted)
            .Where(a => string.IsNullOrEmpty(a.EntraObjectId) || !a.StudentId.HasValue)
            .Include(a => a.AcademicProgram)
            .Include(a => a.AcademicSession)
            .OrderBy(a => a.OfferAcceptedAt)
            .Select(a => new PendingStudentAccountDto
            {
                ApplicationId = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                FirstName = a.FirstName,
                LastName = a.LastName,
                MiddleName = a.MiddleName,
                Email = a.StudentEmail,
                Phone = a.Phone,
                ProgramName = a.AcademicProgram != null ? a.AcademicProgram.Name : "Unknown",
                SessionName = a.AcademicSession != null ? a.AcademicSession.Name : "Unknown",
                OfferAcceptedAt = a.OfferAcceptedAt
            })
            .ToListAsync(ct);

        logger.LogInformation("[REGISTRAR-PENDING] Found {Count} pending accounts", pending.Count);
        return pending;
    }

    public async Task<DocumentSuggestionResult> GetSuggestedDocumentsAsync(ApplicantType applicantType, string? nationality = null, Guid? programId = null)
    {
        var requiredDocs = await GetRequiredDocumentsAsync(applicantType, programId);
        var recommendedDocs = new List<DocumentType>();
        
        var allActiveDocs = await dbContext.DocumentTypes
            .Where(t => t.Category == DocumentCategory.Admission && t.IsActive)
            .ToListAsync();

        string? reason = null;

        switch (applicantType)
        {
            case ApplicantType.International:
                reason = "International students require passport and English proficiency documentation.";
                var countrySpecificDocs = allActiveDocs.Where(d => 
                    d.NigeriaOnly == false && 
                    d.TransferOnly == false && 
                    d.DirectEntryOnly == false &&
                    d.Code.Contains(nationality ?? "", StringComparison.OrdinalIgnoreCase));
                recommendedDocs.AddRange(countrySpecificDocs);
                break;

            case ApplicantType.Transfer:
                reason = "Transfer students need transcript and previous institution documentation.";
                var transferRecommended = allActiveDocs.Where(d => 
                    d.Code.Contains("transcript", StringComparison.OrdinalIgnoreCase) ||
                    d.Code.Contains("credit", StringComparison.OrdinalIgnoreCase));
                recommendedDocs.AddRange(transferRecommended);
                break;

            case ApplicantType.DirectEntry:
                reason = "Direct entry students should provide A-Level/IJMB results and academic records.";
                var directEntryRecommended = allActiveDocs.Where(d => 
                    d.Code.Contains("a-level", StringComparison.OrdinalIgnoreCase) ||
                    d.Code.Contains("ijmb", StringComparison.OrdinalIgnoreCase));
                recommendedDocs.AddRange(directEntryRecommended);
                break;

            case ApplicantType.UTME:
                reason = "UTME students require O-Level results and JAMB scores.";
                var utmeRecommended = allActiveDocs.Where(d => 
                    d.Code.Contains("olevel", StringComparison.OrdinalIgnoreCase) ||
                    d.Code.Contains("jamb", StringComparison.OrdinalIgnoreCase));
                recommendedDocs.AddRange(utmeRecommended);
                break;
        }

        return new DocumentSuggestionResult(requiredDocs, recommendedDocs.Distinct(), reason);
    }

    // --- Transfer Student Enhancement Methods ---

    public async Task<TransferCreditResult> CalculateTransferableCreditsAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new TransferCreditResult(0, 0, 0, 0, false, "Application not found.");
        }

        if (app.ApplicantType != ApplicantType.Transfer)
        {
            return new TransferCreditResult(0, 0, 0, 0, false, "Not a transfer application.");
        }

        if (!app.AcademicProgramId.HasValue)
        {
            return new TransferCreditResult(0, 0, 0, 0, false, "No program selected.");
        }

        var result = await _creditTransferService.CalculateTransferableCreditsAsync(
            app.AcademicProgramId.Value,
            app.PreviousInstitutionCountry,
            app.CreditsEarned ?? 0,
            app.PreviousCGPA ?? 0,
            ct);

        // Update application with calculated values
        app.TransferableCredits = result.TransferableCredits;
        app.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return result;
    }

    public async Task<GradeConversionResult> ConvertCGPAAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new GradeConversionResult(0, null, null, null, false, "Application not found.");
        }

        if (!app.PreviousCGPA.HasValue)
        {
            return new GradeConversionResult(0, null, null, null, false, "No CGPA to convert.");
        }

        // Determine scale from context: if PreviousInstitutionCountry is set, use that; otherwise infer from CGPA value
        var scaleMax = app.CGPAScaleMax ?? (app.PreviousCGPA <= 5m ? 5.0m : 10.0m);
        var scaleMin = app.CGPAScaleMin ?? 0m;

        var result = await _gradeConversionService.ConvertCGPAAsync(
            app.PreviousInstitutionCountry ?? "NG",
            app.CGPAScaleName,
            app.PreviousCGPA.Value,
            scaleMax,
            scaleMin,
            ct);

        // Update application with converted values
        app.ConvertedCGPA = result.ConvertedCGPA;
        app.CGPAScaleName = result.ScaleName ?? app.CGPAScaleName;
        app.CGPAScaleMax = result.OriginalScaleMax ?? app.CGPAScaleMax;
        app.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return result;
    }

    // --- Exchange Student Support ---

    public async Task<ExchangeEligibilityResult> ValidateExchangeEligibilityAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new ExchangeEligibilityResult(false, "Application not found.", false, false, false, false);
        }

        if (app.ExchangeProgramType == ExchangeProgramType.None)
        {
            return new ExchangeEligibilityResult(false, "Not an exchange program application.", false, false, false, false);
        }

        var errors = new List<string>();

        // Validate home institution name
        if (string.IsNullOrEmpty(app.HomeInstitutionName))
        {
            errors.Add("Home institution name is required for exchange students.");
        }

        // Validate home institution approval document
        var homeInstitutionApproved = app.HomeInstitutionApprovalDocumentId.HasValue;
        if (!homeInstitutionApproved)
        {
            errors.Add("Home institution approval document is required.");
        }

        // Validate dean's certificate
        var deansCertificateProvided = app.DeansCertificateDocumentId.HasValue;
        if (!deansCertificateProvided)
        {
            errors.Add("Dean's certificate is required.");
        }

        // Validate academic standing
        var academicStandingVerified = app.HomeInstitutionStanding.HasValue && app.HomeInstitutionStanding.Value == LMS.Api.Data.Enums.AcademicStanding.GoodStanding;
        if (!academicStandingVerified)
        {
            errors.Add("Academic standing must be 'Good Standing' for exchange eligibility.");
        }

        // Validate exchange partner agreement
        var partnerAgreementActive = app.ExchangePartnerAgreementId.HasValue;
        if (!partnerAgreementActive)
        {
            errors.Add("Active exchange partner agreement is required.");
        }

        // Validate exchange dates if provided
        if (app.ExchangeStartDate.HasValue && app.ExchangeEndDate.HasValue)
        {
            if (app.ExchangeEndDate.Value <= app.ExchangeStartDate.Value)
            {
                errors.Add("Exchange end date must be after start date.");
            }
        }

        return new ExchangeEligibilityResult(
            errors.Count == 0,
            errors.Count > 0 ? string.Join(" ", errors) : null,
            homeInstitutionApproved,
            deansCertificateProvided,
            academicStandingVerified,
            partnerAgreementActive);
    }

    // --- Direct Entry Prerequisite Validation ---

    public async Task<PrerequisiteValidationResult> ValidateDirectEntryPrerequisitesAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new PrerequisiteValidationResult(false, "Application not found.", Enumerable.Empty<RequiredSubject>());
        }

        if (app.ApplicantType != ApplicantType.DirectEntry)
        {
            return new PrerequisiteValidationResult(false, "Not a direct entry application.", Enumerable.Empty<RequiredSubject>());
        }

        if (app.AcademicProgramId == null)
        {
            return new PrerequisiteValidationResult(false, "No program selected.", Enumerable.Empty<RequiredSubject>());
        }

        // Get required prerequisites for the program
        var prerequisites = await dbContext.ProgramPrerequisites
            .Where(p => p.ProgramId == app.AcademicProgramId && p.IsActive)
            .ToListAsync(ct);

        if (!prerequisites.Any())
        {
            return new PrerequisiteValidationResult(true, "No prerequisites configured for this program.", Enumerable.Empty<RequiredSubject>());
        }

        // Parse direct entry subjects from application
        var applicantSubjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(app.DirectEntrySubject1)) applicantSubjects["subject1"] = app.DirectEntrySubject1;
        if (!string.IsNullOrEmpty(app.DirectEntrySubject2)) applicantSubjects["subject2"] = app.DirectEntrySubject2;
        if (!string.IsNullOrEmpty(app.DirectEntrySubject3)) applicantSubjects["subject3"] = app.DirectEntrySubject3;

        // Also parse from qualifications JSON
        try
        {
            if (!string.IsNullOrEmpty(app.QualificationsJson))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(app.QualificationsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("subjects", out var subjectsEl) && subjectsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var subj in subjectsEl.EnumerateArray())
                    {
                        var code = subj.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                        var grade = subj.TryGetProperty("grade", out var g) ? g.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(code))
                            applicantSubjects[code] = grade;
                    }
                }
            }
        }
        catch { /* Ignore parse errors */ }

        var missingSubjects = new List<RequiredSubject>();

        foreach (var prereq in prerequisites.Where(p => p.IsCore))
        {
            var isMet = applicantSubjects.Any(kv =>
                kv.Key.Equals(prereq.RequiredSubjectCode, StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals(prereq.RequiredSubjectName, StringComparison.OrdinalIgnoreCase));

            if (!isMet)
            {
                missingSubjects.Add(new RequiredSubject(
                    prereq.RequiredSubjectCode,
                    prereq.RequiredSubjectName,
                    prereq.MinGrade,
                    false));
            }
            else
            {
                missingSubjects.Add(new RequiredSubject(
                    prereq.RequiredSubjectCode,
                    prereq.RequiredSubjectName,
                    prereq.MinGrade,
                    true));
            }
        }

        return new PrerequisiteValidationResult(
            missingSubjects.Count == 0,
            missingSubjects.Count > 0 ? "Missing required prerequisite subjects." : null,
            missingSubjects.Where(s => !s.Met));
    }

    // --- Visa & Immigration Validation ---

    public async Task<VisaValidationResult> ValidateVisaRequirementsAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new VisaValidationResult(false, "Application not found.", false, false, false, false);
        }

        var errors = new List<string>();
        bool visaRequired = false;
        bool visaApplied = false;
        bool financialProofProvided = false;
        bool passportValid = false;

        // Passport validation
        if (!string.IsNullOrEmpty(app.PassportNumber))
            passportValid = true;
        else
            errors.Add("Passport number is required.");

        // Determine if visa is required based on country/region
        var region = app.Region;
        if (app.ApplicantType == ApplicantType.International || !string.IsNullOrEmpty(app.CountryOfOrigin))
        {
            visaRequired = true;

            // Visa application validation
            if (!string.IsNullOrEmpty(app.VisaApplicationNumber))
                visaApplied = true;
            else
                errors.Add("Visa application number is required for international applicants.");

            if (app.VisaExpiryDate.HasValue && app.VisaExpiryDate.Value < DateTime.UtcNow.AddMonths(6))
                errors.Add("Visa must be valid for at least 6 months from now.");

            // Financial proof validation
            if (app.FinancialProofAmount.HasValue && app.FinancialProofAmount.Value > 0)
                financialProofProvided = true;
            else
                errors.Add("Financial proof is required for international applicants.");
        }

        // Immigration status check
        if (app.ImmigrationStatus.HasValue && app.ImmigrationStatus.Value != ImmigrationStatus.NotApplicable)
        {
            // Immigration status is set but can be validated separately
        }

        return new VisaValidationResult(
            errors.Count == 0,
            errors.Count > 0 ? string.Join(" ", errors) : null,
            visaRequired,
            visaApplied,
            financialProofProvided,
            passportValid);
    }

    public async Task<HomeInstitutionValidationResult> ValidateHomeInstitutionRequirementsAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new HomeInstitutionValidationResult(false, "Application not found.", false, false, false, false);
        }

        var errors = new List<string>();
        bool homeInstitutionApproved = false;
        bool deansCertificateProvided = false;
        bool academicStandingVerified = false;
        bool academicStandingGood = false;

        // Only validate for transfer or exchange applicants
        var isTransfer = app.ApplicantType == ApplicantType.Transfer;
        var isExchange = app.ApplicantType == ApplicantType.Exchange;
        if (!isTransfer && !isExchange)
        {
            return new HomeInstitutionValidationResult(false, "Home institution verification only applies to transfer or exchange applicants.", false, false, false, false);
        }

        // Home institution approval document check
        if (app.HomeInstitutionApprovalDocumentId.HasValue)
        {
            homeInstitutionApproved = true;
        }
        else
        {
            errors.Add("Home institution approval document is required.");
        }

        // Dean's certificate check
        if (app.DeansCertificateDocumentId.HasValue)
        {
            deansCertificateProvided = true;
        }
        else
        {
            errors.Add("Dean's certificate is required.");
        }

        // Academic standing check
        if (app.HomeInstitutionStanding.HasValue)
        {
            academicStandingVerified = true;
            if (app.HomeInstitutionStanding.Value == LMS.Api.Data.Enums.AcademicStanding.GoodStanding)
            {
                academicStandingGood = true;
            }
            else if (app.HomeInstitutionStanding.Value == LMS.Api.Data.Enums.AcademicStanding.Probation)
            {
                errors.Add("Academic standing is 'Probation' — additional review required.");
            }
            else if (app.HomeInstitutionStanding.Value == LMS.Api.Data.Enums.AcademicStanding.Suspension)
            {
                errors.Add("Academic standing is 'Suspended' — applicant is not eligible.");
            }
        }
        else
        {
            errors.Add("Academic standing from home institution is required.");
        }

        // For exchange students, also verify partner agreement
        if (isExchange)
        {
            if (app.ExchangePartnerAgreementId.HasValue)
            {
                // Could add additional validation to check agreement status
            }
            else
            {
                errors.Add("Exchange partner agreement is required.");
            }
        }

        return new HomeInstitutionValidationResult(
            errors.Count == 0,
            errors.Count > 0 ? string.Join(" ", errors) : null,
            homeInstitutionApproved,
            deansCertificateProvided,
            academicStandingVerified,
            academicStandingGood);
    }

    // --- Direct Entry Enhancement Methods ---

    public async Task<DirectEntryPointsResult> CalculateDirectEntryPointsAsync(
        DirectEntryQualification qualification,
        DirectEntryGrade grade,
        CancellationToken ct = default)
    {
        // Try to get points from GradingScale table first
        var gradingScale = await dbContext.GradingScales
            .FirstOrDefaultAsync(s => s.QualificationType == qualification.ToString() && s.IsActive, ct);

        if (gradingScale != null)
        {
            try
            {
                var grades = System.Text.Json.JsonSerializer.Deserialize<List<GradingScaleGradeEntry>>(gradingScale.GradesJson);
                if (grades != null)
                {
                    var gradeName = grade switch
                    {
                        DirectEntryGrade.AStar => "A*",
                        DirectEntryGrade.A => "A",
                        DirectEntryGrade.B => "B",
                        DirectEntryGrade.C => "C",
                        DirectEntryGrade.D => "D",
                        DirectEntryGrade.E => "E",
                        DirectEntryGrade.U => "U",
                        DirectEntryGrade.FirstClass => "First Class",
                        DirectEntryGrade.SecondClassUpper => "Second Class Upper",
                        DirectEntryGrade.SecondClassLower => "Second Class Lower",
                        DirectEntryGrade.ThirdClass => "Third Class",
                        DirectEntryGrade.Pass => "Pass",
                        DirectEntryGrade.DistinctionStar => "Distinction*",
                        DirectEntryGrade.Distinction => "Distinction",
                        DirectEntryGrade.Merit => "Merit",
                        DirectEntryGrade.PassBTEC => "Pass",
                        DirectEntryGrade.IB7 => "7",
                        DirectEntryGrade.IB6 => "6",
                        DirectEntryGrade.IB5 => "5",
                        DirectEntryGrade.IB4 => "4",
                        DirectEntryGrade.IB3 => "3",
                        DirectEntryGrade.IB2 => "2",
                        DirectEntryGrade.IB1 => "1",
                        DirectEntryGrade.APlus => "A+",
                        DirectEntryGrade.BPlus => "B+",
                        DirectEntryGrade.CPlus => "C+",
                        DirectEntryGrade.DPlus => "D+",
                        DirectEntryGrade.EPlus => "E+",
                        DirectEntryGrade.A1 => "A1",
                        DirectEntryGrade.B2 => "B2",
                        DirectEntryGrade.B3 => "B3",
                        DirectEntryGrade.C4 => "C4",
                        DirectEntryGrade.C5 => "C5",
                        DirectEntryGrade.C6 => "C6",
                        DirectEntryGrade.D7 => "D7",
                        DirectEntryGrade.E8 => "E8",
                        DirectEntryGrade.F => "F",
                        _ => "Other"
                    };

                    var matchedGrade = grades.FirstOrDefault(g => g.Grade.Equals(gradeName, StringComparison.OrdinalIgnoreCase));
                    if (matchedGrade != null)
                    {
                        return new DirectEntryPointsResult(matchedGrade.Points, true, $"Found in GradingScale: {gradeName}");
                    }
                }
            }
            catch { /* Fall back to configuration */ }
        }

        // Fall back to appsettings DirectEntryGrading configuration
        var qualificationKey = qualification.ToString();
        var gradeKey = grade switch
        {
            DirectEntryGrade.AStar => "AStar",
            DirectEntryGrade.A => "A",
            DirectEntryGrade.B => "B",
            DirectEntryGrade.C => "C",
            DirectEntryGrade.D => "D",
            DirectEntryGrade.E => "E",
            DirectEntryGrade.U => "U",
            DirectEntryGrade.FirstClass => "FirstClass",
            DirectEntryGrade.SecondClassUpper => "SecondClassUpper",
            DirectEntryGrade.SecondClassLower => "SecondClassLower",
            DirectEntryGrade.ThirdClass => "ThirdClass",
            DirectEntryGrade.Pass => "Pass",
            DirectEntryGrade.DistinctionStar => "DistinctionStar",
            DirectEntryGrade.Distinction => "Distinction",
            DirectEntryGrade.Merit => "Merit",
            DirectEntryGrade.PassBTEC => "PassBTEC",
            DirectEntryGrade.IB7 => "IB7",
            DirectEntryGrade.IB6 => "IB6",
            DirectEntryGrade.IB5 => "IB5",
            DirectEntryGrade.IB4 => "IB4",
            DirectEntryGrade.IB3 => "IB3",
            DirectEntryGrade.IB2 => "IB2",
            DirectEntryGrade.IB1 => "IB1",
            DirectEntryGrade.APlus => "APlus",
            DirectEntryGrade.BPlus => "BPlus",
            DirectEntryGrade.CPlus => "CPlus",
            DirectEntryGrade.DPlus => "DPlus",
            DirectEntryGrade.EPlus => "EPlus",
            DirectEntryGrade.A1 => "A1",
            DirectEntryGrade.B2 => "B2",
            DirectEntryGrade.B3 => "B3",
            DirectEntryGrade.C4 => "C4",
            DirectEntryGrade.C5 => "C5",
            DirectEntryGrade.C6 => "C6",
            DirectEntryGrade.D7 => "D7",
            DirectEntryGrade.E8 => "E8",
            DirectEntryGrade.F => "F",
            _ => "Other"
        };

        var points = GetPointsFromConfig(qualificationKey, gradeKey);
        var defaultConfig = configuration.GetSection($"DirectEntryGrading:Default");
        var minPassing = defaultConfig.GetValue<double?>("MinPassingPoints") ?? 1.0;

        var isPassing = points >= minPassing;

        return new DirectEntryPointsResult(points, isPassing, $"Calculated from configuration: {qualificationKey} -> {gradeKey}");
    }

    private double GetPointsFromConfig(string qualificationKey, string gradeKey)
    {
        var section = configuration.GetSection($"DirectEntryGrading:{qualificationKey}:{gradeKey}");
        if (section.Exists())
        {
            return section.GetValue<double>(gradeKey);
        }

        // Fall back to Default
        var defaultSection = configuration.GetSection("DirectEntryGrading:Default");
        if (defaultSection.Exists())
        {
            return defaultSection.GetValue<double>("MinPoints");
        }

        return 0.0;
    }

    public async Task<LevelSuggestionResult> SuggestStartingLevelForQualificationAsync(
        DirectEntryQualification qualification,
        CancellationToken ct = default)
    {
        // Nigerian convention: HND -> 300, ND -> 200, Diploma -> 200
        // International: A-Level/IB -> 100, Cambridge Advanced -> 100
        int? suggestedLevel = qualification switch
        {
            DirectEntryQualification.HND => (int?)300,
            DirectEntryQualification.ND => (int?)200,
            DirectEntryQualification.Diploma => (int?)200,
            DirectEntryQualification.None => (int?)100,
            DirectEntryQualification.ALevel => (int?)100,
            DirectEntryQualification.IB => (int?)100,
            DirectEntryQualification.CambridgeAdvanced => (int?)100,
            DirectEntryQualification.AdvancedAdvanced => (int?)100,
            DirectEntryQualification.BTEC => (int?)100,
            DirectEntryQualification.IJMB => (int?)100,
            _ => null
        };

        var levelName = suggestedLevel switch
        {
            100 => "100 Level (Freshman)",
            200 => "200 Level (Sophomore)",
            300 => "300 Level (Junior)",
            400 => "400 Level (Senior)",
            _ => null
        };

        // If we have ProgramCreditMapping, refine the suggestion
        if (suggestedLevel.HasValue)
        {
            // Just return the suggestion; actual credit calculation happens elsewhere
            return new LevelSuggestionResult(
                suggestedLevel,
                levelName,
                0m,
                true,
                $"Suggested based on qualification type: {qualification}");
        }

        return new LevelSuggestionResult(
            null,
            null,
            0m,
            false,
            $"No starting level suggestion for qualification type: {qualification}");
    }

    /// <summary>
    /// Parses the EmergencyContactJson field and populates the individual contact fields.
    /// </summary>
    private void ParseEmergencyContactJson(AdmissionApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.EmergencyContactJson))
            return;

        try
        {
            var emergency = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(application.EmergencyContactJson);
            if (emergency != null)
            {
                if (emergency.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
                    application.EmergencyContactName = name;
                if (emergency.TryGetValue("phone", out var phone) && !string.IsNullOrWhiteSpace(phone))
                    application.EmergencyContactPhone = phone;
                if (emergency.TryGetValue("email", out var email) && !string.IsNullOrWhiteSpace(email))
                    application.EmergencyContactEmail = email;
            }
        }
        catch
        {
            // If parsing fails, leave the fields as-is
        }
    }

    /// <summary>
    /// Sends a reminder email to a single applicant.
    /// Used by Registry dashboard to prompt applicants to complete JAMB CAPS and O'Level steps.
    /// </summary>
    public async Task<ReminderSendResult> SendReminderAsync(Guid applicationId, CancellationToken ct = default)
    {
        var app = await dbContext.AdmissionApplications
            .Include(a => a.AcademicSession)
            .Include(a => a.AcademicProgram)
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (app == null)
        {
            return new ReminderSendResult(false, applicationId, "Application not found.", null, null);
        }

        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Replace("  ", " ").Trim();
        var emailName = string.IsNullOrWhiteSpace(fullName) ? "Applicant" : fullName;
        var resultName = string.IsNullOrWhiteSpace(fullName) ? app.StudentEmail : fullName;

        // Progressive cooldown logic
        if (app.ReminderCount == 1 && app.LastReminderSentAt.HasValue && app.LastReminderSentAt.Value > DateTime.UtcNow.AddHours(-24))
        {
            return new ReminderSendResult(false, applicationId, "Next reminder available after 24 hours.", app.StudentEmail, resultName);
        }
        if (app.ReminderCount >= 2 && app.LastReminderSentAt.HasValue && app.LastReminderSentAt.Value > DateTime.UtcNow.AddDays(-7))
        {
            return new ReminderSendResult(false, applicationId, "Next reminder available after 1 week.", app.StudentEmail, resultName);
        }

        try
        {
            await emailService.SendApplicationReminderEmailAsync(
                app.StudentEmail,
                emailName,
                app.ApplicationNumber ?? "Pending",
                app.Status);
                
            app.LastReminderSentAt = DateTime.UtcNow;
            app.ReminderCount++;
            await dbContext.SaveChangesAsync(ct);
            
            logger.LogInformation("[REMINDER] Reminder email sent to {Email} for application {ApplicationId}", app.StudentEmail, applicationId);
            return new ReminderSendResult(true, applicationId, null, app.StudentEmail, resultName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[REMINDER-ERROR] Failed to send reminder email to {Email} for application {ApplicationId}", app.StudentEmail, applicationId);
            return new ReminderSendResult(false, applicationId, ex.Message, app.StudentEmail, resultName);
        }
    }

    /// <summary>
    /// Sends reminder emails to multiple applicants.
    /// Used by Registry dashboard for bulk operations.
    /// </summary>
    public async Task<BulkReminderResult> SendBulkRemindersAsync(IEnumerable<Guid> applicationIds, CancellationToken ct = default)
    {
        var results = new List<ReminderSendResult>();
        var recipients = applicationIds.Distinct().ToList();

        foreach (var applicationId in recipients)
        {
            var result = await SendReminderAsync(applicationId, ct);
            results.Add(result);
        }

        var sentCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);

        return new BulkReminderResult(
            recipients.Count,
            sentCount,
            failedCount,
            results);
    }
}
