using System.ComponentModel;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services.AI.Tools;

public class AdmissionAgentTools
{
    private readonly LmsDbContext _dbContext;
    private readonly ILogger<AdmissionAgentTools> _logger;

    public AdmissionAgentTools(LmsDbContext dbContext, ILogger<AdmissionAgentTools> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Description("Returns a high-level summary of admission statistics for a given session: total applications, admitted count, rejected, pending, and accepted-offer counts.")]
    public async Task<string> GetAdmissionStatisticsAsync(Guid? sessionId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("AdmissionAgentTool querying admission statistics for session {SessionId}", sessionId);

        var query = _dbContext.AdmissionApplications.AsQueryable();

        if (sessionId.HasValue)
        {
            query = query.Where(a => a.AcademicSessionId == sessionId.Value);
        }
        else
        {
            // Use the active session
            var activeSession = await _dbContext.AcademicSessions.Where(s => s.IsActive).FirstOrDefaultAsync(ct);
            if (activeSession != null)
            {
                query = query.Where(a => a.AcademicSessionId == activeSession.Id);
            }
        }

        var total = await query.CountAsync(ct);
        var admitted = await query.CountAsync(a => a.Status == AdmissionStatus.Admitted, ct);
        var accepted = await query.CountAsync(a => a.Status == AdmissionStatus.OfferAccepted, ct);
        var feePaid = await query.CountAsync(a => a.Status == AdmissionStatus.FeePaid, ct);
        var rejected = await query.CountAsync(a => a.Status == AdmissionStatus.Rejected, ct);
        var pending = await query.CountAsync(a => a.Status == AdmissionStatus.UnderReview || a.Status == AdmissionStatus.Submitted, ct);
        var draft = await query.CountAsync(a => a.Status == AdmissionStatus.Draft, ct);
        var waitlisted = await query.CountAsync(a => a.Status == AdmissionStatus.Waitlisted, ct);

        return $"Admission Summary: {total} total applications — " +
               $"{admitted} Admitted, {accepted} Offer Accepted, {feePaid} Fee Paid (Enrolled), " +
               $"{pending} Under Review, {waitlisted} Waitlisted, {rejected} Rejected, {draft} Drafts.";
    }

    [Description("Returns a list of recently admitted or offer-accepted applicants with their names, programs and application numbers.")]
    public async Task<string> GetRecentlyAdmittedApplicantsAsync(int count = 10, CancellationToken ct = default)
    {
        _logger.LogInformation("AdmissionAgentTool fetching recently admitted applicants (top {Count})", count);

        var admitted = await _dbContext.AdmissionApplications
            .Where(a => a.Status == AdmissionStatus.Admitted || a.Status == AdmissionStatus.OfferAccepted || a.Status == AdmissionStatus.FeePaid)
            .Include(a => a.AcademicProgram)
            .OrderByDescending(a => a.UpdatedAt)
            .Take(count)
            .ToListAsync(ct);

        if (admitted.Count == 0)
        {
            return "No admitted applicants found in the current session.";
        }

        var list = admitted.Select(a =>
            $"- **{$"{a.FirstName} {a.LastName}".ToTitleCase()}** ({a.ApplicationNumber}) → {a.AcademicProgram?.Name ?? "N/A"} | Status: {a.Status} | {a.UpdatedAt:yyyy-MM-dd}");

        return $"Recently Admitted Applicants ({admitted.Count}):\n" + string.Join("\n", list);
    }

    [Description("Looks up a specific applicant by their application number or JAMB registration number and returns their full admission status.")]
    public async Task<string> GetApplicantStatusByNumberAsync(string searchQuery, CancellationToken ct = default)
    {
        _logger.LogInformation("AdmissionAgentTool looking up applicant by query '{Query}'", searchQuery);

        var app = await _dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .Include(a => a.Faculty)
            .Include(a => a.StartingLevel)
            .FirstOrDefaultAsync(a =>
                a.ApplicationNumber.ToLower() == searchQuery.ToLower() ||
                a.JambRegNumber.ToLower() == searchQuery.ToLower() ||
                a.StudentEmail.ToLower() == searchQuery.ToLower(), ct);

        if (app == null)
        {
            return $"No applicant found matching '{searchQuery}'. Please check the application number or JAMB registration number.";
        }

        return $"Applicant Found: **{$"{app.FirstName} {app.LastName}".ToTitleCase()}** — Application No: {app.ApplicationNumber}\n" +
               $"Email: {app.StudentEmail} | JAMB Reg: {app.JambRegNumber}\n" +
               $"Program: {app.AcademicProgram?.Name ?? "N/A"} ({app.Faculty?.Name ?? "N/A"})\n" +
               $"Type: {app.ApplicantType} | Level: {app.StartingLevel?.Name ?? "N/A"}\n" +
               $"Status: **{app.Status}** | Submitted: {app.SubmittedAt?.ToString("yyyy-MM-dd") ?? "Not yet"} | Updated: {app.UpdatedAt:yyyy-MM-dd}\n" +
               (app.OfferExpiresAt.HasValue ? $"Offer Expires: {app.OfferExpiresAt:yyyy-MM-dd}" : "");
    }

    [Description("Returns a breakdown of applications by program/faculty — useful for Admissions Officers to see which programs are most popular.")]
    public async Task<string> GetApplicationsByProgramAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("AdmissionAgentTool fetching application breakdown by program");

        var breakdown = await _dbContext.AdmissionApplications
            .Include(a => a.AcademicProgram)
            .GroupBy(a => a.AcademicProgram != null ? a.AcademicProgram.Name : "Unspecified Program")
            .Select(g => new { ProgramName = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(15)
            .ToListAsync(ct);

        if (breakdown.Count == 0)
        {
            return "No applications found in the database.";
        }

        var lines = breakdown.Select(b => $"- **{b.ProgramName}**: {b.Count} application(s)");

        return $"Applications Breakdown by Program ({breakdown.Sum(b => b.Count)} Total Applications):\n" + string.Join("\n", lines);
    }

    [Description("Returns a list of pending applications that are awaiting review, offer letters, or documents submission.")]
    public async Task<string> GetPendingApplicationsReviewAsync(int count = 8, CancellationToken ct = default)
    {
        _logger.LogInformation("AdmissionAgentTool fetching pending applications");

        var pending = await _dbContext.AdmissionApplications
            .Where(a => a.Status == AdmissionStatus.UnderReview || a.Status == AdmissionStatus.Submitted)
            .Include(a => a.AcademicProgram)
            .OrderBy(a => a.SubmittedAt)
            .Take(count)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return "No applications are currently awaiting review. All submissions have been processed!";
        }

        var list = pending.Select(a =>
            $"- **{$"{a.FirstName} {a.LastName}".ToTitleCase()}** ({a.ApplicationNumber}) → {a.AcademicProgram?.Name ?? "N/A"} | {a.ApplicantType} | Submitted: {a.SubmittedAt?.ToString("yyyy-MM-dd") ?? "N/A"}");

        return $"Applications Pending Review ({pending.Count}):\n" + string.Join("\n", list);
    }
}
