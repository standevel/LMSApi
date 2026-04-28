using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
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
    ILogger<AdmissionService> logger) : IAdmissionService
{
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
                .FirstOrDefaultAsync(a => a.StudentEmail == email || a.JambRegNumber == jambRegNumber);
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
                    .FirstOrDefaultAsync(a => a.StudentEmail == email || a.JambRegNumber == jambRegNumber);
                
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
            var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
            await emailService.SendApplicationSubmittedEmailAsync(app.StudentEmail, fullName);
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

    public async Task<AcademicSession?> GetActiveAdmissionSessionAsync()
    {
        return await dbContext.AcademicSessions
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

    private async Task HandleStatusChangeNotificationsAsync(AdmissionApplication app, AdmissionStatus newStatus)
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
                        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
                        await emailService.SendAdmissionOfferEmailAsync(
                            app.StudentEmail,
                            fullName,
                            app.AcademicProgram?.Name ?? "Selected Program",
                            $"{offerDecisionUrl}?decision=accept",
                            $"{offerDecisionUrl}?decision=reject",
                            pdf,
                            "Admission_Letter.pdf");
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
                    // O-Level subject validation: check required subjects have credit pass (A1-C6)
                    var creditGrades = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        { "A1", "B2", "B3", "C4", "C5", "C6" };

                    bool oLevelPass = true;
                    string? oLevelFailReason = null;

                    try
                    {
                        var requiredSubjects = System.Text.Json.JsonSerializer
                            .Deserialize<List<string>>(program.RequiredOLevelSubjectsJson ?? "[]")
                            ?? new List<string>();

                        if (requiredSubjects.Count > 0 &&
                            candidate.Quals.ValueKind == System.Text.Json.JsonValueKind.Object &&
                            candidate.Quals.TryGetProperty("results", out var resultsEl) &&
                            resultsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            // Build a dict of subject -> grade from applicant's results
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
                                if (!applicantResults.TryGetValue(required, out var grade) ||
                                    !creditGrades.Contains(grade))
                                {
                                    oLevelPass = false;
                                    oLevelFailReason = $"Missing credit pass in required O-Level subject: {required}.";
                                    break;
                                }
                            }
                        }
                    }
                    catch { /* If parsing fails, proceed without O-Level filter */ }

                    if (!oLevelPass)
                    {
                        reason = oLevelFailReason;
                    }
                    else
                    {
                        isAdmitted = true;
                        admittedCount++;
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

    /// <summary>
    /// Creates a student account for an accepted admission application.
    /// This is called by the Registrar to manually trigger account creation.
    /// </summary>
    public async Task<StudentAccountCreationResult> CreateStudentAccountAsync(Guid applicationId, CancellationToken ct = default)
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
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("[REGISTRAR-STUDENT-CREATION] Student record created: StudentId={StudentId}", student.Id);

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
            .Where(a => a.Status == AdmissionStatus.OfferAccepted)
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
}
