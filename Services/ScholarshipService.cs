using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class ScholarshipService(LmsDbContext db) : IScholarshipService
{
    public async Task<ScholarshipDto> CreateScholarshipAsync(CreateScholarshipRequest req)
    {
        var s = new Scholarship
        {
            Name = req.Name,
            Description = req.Description,
            Type = req.Type,
            CoverageFlags = req.CoverageFlags,
            PercentageCovered = req.PercentageCovered,
            SponsorOrganizationId = req.SponsorOrganizationId,
            MinJambScore = req.MinJambScore,
            MaxJambScore = req.MaxJambScore,
            IsActive = true
        };
        db.Scholarships.Add(s);
        await db.SaveChangesAsync();
        return MapToDto(s);
    }

    public async Task<ScholarshipDto> UpdateScholarshipAsync(Guid id, UpdateScholarshipRequest req)
    {
        var s = await db.Scholarships.FindAsync(id)
            ?? throw new KeyNotFoundException("Scholarship not found.");
            
        s.Name = req.Name;
        s.Description = req.Description;
        s.Type = req.Type;
        s.CoverageFlags = req.CoverageFlags;
        s.PercentageCovered = req.PercentageCovered;
        s.SponsorOrganizationId = req.SponsorOrganizationId;
        s.MinJambScore = req.MinJambScore;
        s.MaxJambScore = req.MaxJambScore;
        s.IsActive = req.IsActive;
        
        await db.SaveChangesAsync();
        return MapToDto(s);
    }

    public async Task<IEnumerable<ScholarshipDto>> GetAllScholarshipsAsync(bool? activeOnly = null)
    {
        var q = db.Scholarships.Include(s => s.SponsorOrganization).AsQueryable();
        if (activeOnly.HasValue) q = q.Where(s => s.IsActive == activeOnly.Value);
        var res = await q.OrderBy(s => s.Name).ToListAsync();
        return res.Select(MapToDto);
    }

    public async Task<ScholarshipDto?> GetScholarshipByIdAsync(Guid id)
    {
        var s = await db.Scholarships.FindAsync(id);
        return s == null ? null : MapToDto(s);
    }

    public async Task<StudentScholarshipDto> AssignScholarshipAsync(AssignScholarshipRequest req)
    {
        var ss = new StudentScholarship
        {
            StudentId = req.StudentId,
            ScholarshipId = req.ScholarshipId,
            SessionId = req.SessionId
        };
        db.StudentScholarships.Add(ss);
        await db.SaveChangesAsync();
        
        // Reload with includes
        var reloaded = await db.StudentScholarships
            .Include(x => x.Student)
            .Include(x => x.Scholarship)
            .ThenInclude(s => s.SponsorOrganization)
            .FirstAsync(x => x.Id == ss.Id);
            
        return MapToStudentScholarshipDto(reloaded);
    }

    public async Task RemoveScholarshipAssignmentAsync(Guid id)
    {
        var ss = await db.StudentScholarships.FindAsync(id)
            ?? throw new KeyNotFoundException("Assignment not found.");
            
        db.StudentScholarships.Remove(ss);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<StudentScholarshipDto>> GetStudentScholarshipsAsync(Guid studentId, Guid? sessionId = null)
    {
        var query = db.StudentScholarships
            .Include(ss => ss.Scholarship)
            .Include(ss => ss.Student)
            .Where(ss => ss.StudentId == studentId);

        if (sessionId.HasValue)
        {
            query = query.Where(ss => ss.SessionId == sessionId.Value);
        }

        var res = await query.ToListAsync();
        return res.Select(MapToStudentScholarshipDto);
    }

    public async Task<IEnumerable<StudentScholarshipDto>> GetAllStudentScholarshipsAsync(int limit = 100)
    {
        var res = await db.StudentScholarships
            .Include(ss => ss.Student)
            .Include(ss => ss.Scholarship)
            .ThenInclude(s => s.SponsorOrganization)
            .OrderByDescending(ss => ss.CreatedAt)
            .Take(limit)
            .ToListAsync();
            
        return res.Select(MapToStudentScholarshipDto);
    }

    public static int ConvertDirectEntryToJambScore(DirectEntryQualification qual, decimal? points, string? gradeStr)
    {
        // 1. Points-based scale (A-Level, IJMB, IB, Cambridge)
        if (points.HasValue && (
            qual == DirectEntryQualification.ALevel ||
            qual == DirectEntryQualification.IJMB ||
            qual == DirectEntryQualification.IB ||
            qual == DirectEntryQualification.CambridgeAdvanced ||
            qual == DirectEntryQualification.AdvancedAdvanced))
        {
            var pts = points.Value;
            if (pts >= 15) return 350;
            if (pts >= 14) return 330;
            if (pts >= 13) return 310;
            if (pts >= 12) return 290;
            if (pts >= 11) return 270;
            if (pts >= 10) return 250;
            if (pts >= 9) return 230;
            if (pts >= 8) return 210;
            if (pts >= 7) return 190;
            if (pts >= 6) return 180;
            return 160;
        }

        // 2. Class-based/grade-based qualifications (HND, ND, Diploma, BTEC)
        if (!string.IsNullOrEmpty(gradeStr))
        {
            var g = gradeStr.Replace(" ", "").Replace("*", "").ToLowerInvariant();

            if (g.Contains("firstclass") || g.Contains("distinction") || g.Contains("first"))
                return 340;
            if (g.Contains("secondclassupper") || g.Contains("uppercredit") || g.Contains("merit") || g.Contains("upper"))
                return 290;
            if (g.Contains("secondclasslower") || g.Contains("lowercredit") || g.Contains("lower"))
                return 240;
            if (g.Contains("thirdclass") || g.Contains("third"))
                return 210;
            if (g.Contains("pass"))
                return 180;
        }

        // 3. Fallback based on points directly if none of the above matched
        if (points.HasValue)
        {
            var pts = points.Value;
            // ND/HND CGPA out of 4.0/5.0
            if (pts >= 4.5m) return 340;
            if (pts >= 3.5m) return 340;
            if (pts >= 3.0m) return 290;
            if (pts >= 2.5m) return 240;
            if (pts >= 2.0m) return 200;
        }

        return 0;
    }

    public async Task ApplyJambScholarshipsAsync(Guid studentId, Guid sessionId)
    {
        var student = await db.Students
            .Include(s => s.AcademicSession)
            .FirstOrDefaultAsync(s => s.Id == studentId)
            ?? throw new KeyNotFoundException("Student not found.");
            
        int? score = student.JambScore;

        if (!score.HasValue && student.AdmissionApplicationId.HasValue)
        {
            var app = await db.AdmissionApplications.FindAsync(student.AdmissionApplicationId.Value);
            if (app != null && app.ApplicantType == ApplicantType.DirectEntry)
            {
                var convertedScore = ConvertDirectEntryToJambScore(app.DirectEntryQualification, app.DirectEntryPoints, app.DirectEntryGrade);
                if (convertedScore > 0)
                {
                    score = convertedScore;
                }
            }
        }

        if (!score.HasValue) return;

        var actualScore = score.Value;

        // Find applicable JAMB scholarships based on score tiers
        var jambScholarships = await db.Scholarships
            .Where(s => s.IsActive && s.Type == ScholarshipType.JAMB)
            .ToListAsync();

        var applicable = jambScholarships.Where(s => 
            (!s.MinJambScore.HasValue || actualScore >= s.MinJambScore.Value) &&
            (!s.MaxJambScore.HasValue || actualScore <= s.MaxJambScore.Value)
        )
        .OrderByDescending(s => s.PercentageCovered)
        .Take(1)
        .ToList();

        // Get current assignments for JAMB
        var existingAssignments = await db.StudentScholarships
            .Include(ss => ss.Scholarship)
            .Where(ss => ss.StudentId == studentId && ss.SessionId == sessionId && ss.Scholarship.Type == ScholarshipType.JAMB)
            .ToListAsync();

        bool hasChanges = false;

        // Remove assignments that are no longer applicable
        var toRemove = existingAssignments.Where(ea => !applicable.Any(a => a.Id == ea.ScholarshipId)).ToList();
        if (toRemove.Any())
        {
            db.StudentScholarships.RemoveRange(toRemove);
            hasChanges = true;
        }

        // Add new assignments
        var toAdd = applicable.Where(a => !existingAssignments.Any(ea => ea.ScholarshipId == a.Id)).ToList();
        foreach (var s in toAdd)
        {
            db.StudentScholarships.Add(new StudentScholarship
            {
                StudentId = studentId,
                ScholarshipId = s.Id,
                SessionId = sessionId
            });
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync();
        }
    }

    public async Task ApplyJambScholarshipsForAdmissionSessionAsync(Guid admissionSessionId)
    {
        // Get all students admitted in this session who have a JAMB score or are Direct Entry
        var students = await db.Students
            .Include(s => s.AdmissionApplication)
            .Where(s => s.AcademicSessionId == admissionSessionId && (
                s.JambScore.HasValue ||
                (s.AdmissionApplicationId.HasValue && s.AdmissionApplication.ApplicantType == ApplicantType.DirectEntry)
            ))
            .ToListAsync();

        if (!students.Any()) return;

        // Apply scholarship for each student for the admission session
        foreach (var student in students)
        {
            await ApplyJambScholarshipsAsync(student.Id, admissionSessionId);
        }
    }

    public async Task<bool> IsFeedingFullyCoveredAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student is null) return false;

        return await db.StudentScholarships
            .AsNoTracking()
            .Where(ss => ss.StudentId == studentId && ss.SessionId == student.AcademicSessionId)
            .AnyAsync(
                ss => ss.Scholarship.IsActive
                   && ss.Scholarship.CoverageFlags.HasFlag(ScholarshipCoverageFlags.Feeding)
                   && ss.Scholarship.PercentageCovered >= 100, ct);
    }

    public async Task<StudentFeedingEntitlementDto> GetFeedingEntitlementAsync(string username, CancellationToken ct = default)
    {
        var clean = (username ?? string.Empty).Trim().ToLowerInvariant();

        var studentId = await db.Students
            .AsNoTracking()
            .Where(s => s.OfficialEmail.ToLower() == clean || (s.StudentNumber != null && s.StudentNumber.ToLower() == clean))
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (!studentId.HasValue)
        {
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => (u.Email != null && u.Email.ToLower() == clean) || (u.Username != null && u.Username.ToLower() == clean), ct);
            if (user is not null)
            {
                studentId = await db.Students
                    .AsNoTracking()
                    .Where(s => s.OfficialEmail.ToLower() == user.Email!.ToLower() || s.EntraObjectId == user.EntraObjectId)
                    .Select(s => (Guid?)s.Id)
                    .FirstOrDefaultAsync(ct);
            }
        }

        if (!studentId.HasValue)
        {
            return new StudentFeedingEntitlementDto(false, 0m, false, new Dictionary<string, bool>());
        }

        return await GetFeedingEntitlementAsync(studentId.Value, ct);
    }

    public async Task<StudentFeedingEntitlementDto> GetFeedingEntitlementAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student is null)
        {
            return new StudentFeedingEntitlementDto(false, 0m, false, new Dictionary<string, bool>());
        }

        var scholarships = await (from ss in db.StudentScholarships
                                  join s in db.Scholarships on ss.ScholarshipId equals s.Id
                                  where ss.StudentId == studentId
                                        && ss.SessionId == student.AcademicSessionId
                                        && s.IsActive
                                        && s.CoverageFlags.HasFlag(ScholarshipCoverageFlags.Feeding)
                                  select s).ToListAsync(ct);

        var maxCoverage = scholarships.Count == 0 ? 0m : scholarships.Max(s => s.PercentageCovered);
        var hasActive = scholarships.Count > 0;
        var fullyCovered = maxCoverage >= 100;

        var dailyClaimed = await BuildDailyMealWindowsClaimedAsync(student, ct);

        // Determine current session & available rollover windows
        var watZone = GetWatTimeZone();
        var watNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone);
        var (currentSession, availableWindows, rolloverSummary) = CalculateAvailableWindows(dailyClaimed, watNow.TimeOfDay);

        return new StudentFeedingEntitlementDto(
            hasActive,
            maxCoverage,
            fullyCovered,
            dailyClaimed,
            availableWindows,
            currentSession,
            rolloverSummary
        );
    }

    public async Task<ScholarshipMealClaimResult> EvaluateScholarshipMealClaimAsync(
        Guid studentId,
        int feedingTimeId,
        string? menuItemName,
        CancellationToken ct = default)
    {
        var student = await db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student is null)
        {
            return new ScholarshipMealClaimResult(false, "Student record not found.", "Unknown", false, new(), new(), "Closed");
        }

        var isCovered = await IsFeedingFullyCoveredAsync(studentId, ct);
        if (!isCovered)
        {
            return new ScholarshipMealClaimResult(
                false,
                "Student does not have full scholarship feeding coverage. Meals may be purchased directly with wallet balance.",
                "General",
                false,
                new(),
                new(),
                "General");
        }

        var dailyClaimed = await BuildDailyMealWindowsClaimedAsync(student, ct);

        var watZone = GetWatTimeZone();
        var watNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone);
        var timeOfDay = watNow.TimeOfDay;

        var (currentSession, availableWindows, rolloverSummary) = CalculateAvailableWindows(dailyClaimed, timeOfDay);
        var targetMealWindow = ResolveMealWindowFromInput(feedingTimeId, menuItemName);

        bool breakfastClaimed = dailyClaimed.GetValueOrDefault("Breakfast", false);
        bool lunchClaimed = dailyClaimed.GetValueOrDefault("Lunch", false);
        bool dinnerClaimed = dailyClaimed.GetValueOrDefault("Dinner", false);

        bool canClaim = false;
        bool isRolledOver = false;
        string reason = string.Empty;

        if (currentSession == "Closed")
        {
            canClaim = false;
            reason = "Campus cafeteria dining service opens at 07:00 WAT.";
        }
        else if (targetMealWindow == "Breakfast")
        {
            if (breakfastClaimed)
            {
                canClaim = false;
                reason = "Your scholarship breakfast entitlement was already claimed today. Additional dishes can be purchased with your wallet.";
            }
            else if (currentSession == "Breakfast")
            {
                canClaim = true;
                reason = "Breakfast window is active (07:00 - 10:00 WAT).";
            }
            else if (currentSession == "Lunch")
            {
                canClaim = true;
                isRolledOver = true;
                reason = "Breakfast was skipped earlier today and rolled over into Lunch service! You can claim both Breakfast and Lunch.";
            }
            else if (currentSession == "Dinner")
            {
                canClaim = true;
                isRolledOver = true;
                reason = "Breakfast was skipped earlier today and rolled over into Dinner service! You can claim Breakfast, Lunch, and Dinner together.";
            }
        }
        else if (targetMealWindow == "Lunch")
        {
            if (lunchClaimed)
            {
                canClaim = false;
                reason = "Your scholarship lunch entitlement was already claimed today. Additional dishes can be purchased with your wallet.";
            }
            else if (currentSession == "Breakfast")
            {
                canClaim = false;
                reason = "Lunch cannot be claimed during Breakfast time. Lunch service begins at 12:00 WAT.";
            }
            else if (currentSession == "Lunch")
            {
                canClaim = true;
                reason = "Lunch window is active (12:00 - 15:30 WAT).";
            }
            else if (currentSession == "Dinner")
            {
                canClaim = true;
                isRolledOver = true;
                reason = "Lunch was skipped earlier today and rolled over into Dinner service! You can claim Lunch and Dinner together.";
            }
        }
        else if (targetMealWindow == "Dinner")
        {
            if (dinnerClaimed)
            {
                canClaim = false;
                reason = "Your scholarship dinner entitlement was already claimed today. Additional dishes can be purchased with your wallet.";
            }
            else if (currentSession == "Dinner")
            {
                canClaim = true;
                reason = "Dinner window is active (18:00 - 21:00 WAT).";
            }
            else
            {
                canClaim = false;
                reason = $"Dinner cannot be claimed during {currentSession} service. Dinner service begins at 18:00 WAT.";
            }
        }
        else
        {
            // All-day dishes or snacks: allowed if any daily slot remains
            if (!breakfastClaimed)
            {
                canClaim = true;
                targetMealWindow = "Breakfast";
                reason = "Applied to available Breakfast slot.";
            }
            else if (!lunchClaimed && (currentSession == "Lunch" || currentSession == "Dinner"))
            {
                canClaim = true;
                targetMealWindow = "Lunch";
                reason = "Applied to available Lunch slot.";
            }
            else if (!dinnerClaimed && currentSession == "Dinner")
            {
                canClaim = true;
                targetMealWindow = "Dinner";
                reason = "Applied to available Dinner slot.";
            }
            else
            {
                canClaim = false;
                reason = "No eligible scholarship meal slots remain for this dining session.";
            }
        }

        return new ScholarshipMealClaimResult(
            canClaim,
            reason,
            targetMealWindow,
            isRolledOver,
            dailyClaimed,
            availableWindows,
            currentSession
        );
    }

    private async Task<Dictionary<string, bool>> BuildDailyMealWindowsClaimedAsync(Student student, CancellationToken ct)
    {
        var windows = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["Breakfast"] = false,
            ["Lunch"] = false,
            ["Dinner"] = false
        };

        var watZone = GetWatTimeZone();
        var watNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, watZone);
        var startOfDayUtc = TimeZoneInfo.ConvertTimeToUtc(watNow.Date, watZone);

        var cleanEmail = student.OfficialEmail.Trim().ToLowerInvariant();
        var cleanMatric = (student.StudentNumber ?? string.Empty).Trim().ToLowerInvariant();
        var sidStr = student.Id.ToString().ToLowerInvariant();

        // Non-cancelled orders placed today under scholarship coverage
        var claimedToday = await db.CafeteriaVendorOrders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= startOfDayUtc
                     && o.Status != CafeteriaOrderStatus.Cancelled
                     && o.IsScholarshipCovered
                     && (o.StudentUsername.ToLower() == cleanEmail
                         || (o.MatricNo != null && o.MatricNo.ToLower() == cleanMatric)
                         || o.StudentUsername.ToLower() == sidStr))
            .ToListAsync(ct);

        if (!claimedToday.Any()) return windows;

        foreach (var order in claimedToday)
        {
            var window = ResolveMealWindow(order);
            if (!string.IsNullOrEmpty(window) && windows.ContainsKey(window))
            {
                windows[window] = true;
            }
        }

        return windows;
    }

    private static (string CurrentSession, List<string> AvailableWindows, string RolloverSummary) CalculateAvailableWindows(
        Dictionary<string, bool> dailyClaimed,
        TimeSpan timeOfDay)
    {
        string currentSession;
        if (timeOfDay >= new TimeSpan(7, 0, 0) && timeOfDay < new TimeSpan(12, 0, 0))
        {
            currentSession = "Breakfast";
        }
        else if (timeOfDay >= new TimeSpan(12, 0, 0) && timeOfDay < new TimeSpan(18, 0, 0))
        {
            currentSession = "Lunch";
        }
        else if (timeOfDay >= new TimeSpan(18, 0, 0) && timeOfDay <= new TimeSpan(23, 59, 59))
        {
            currentSession = "Dinner";
        }
        else
        {
            currentSession = "Closed";
        }

        bool breakfastClaimed = dailyClaimed.GetValueOrDefault("Breakfast", false);
        bool lunchClaimed = dailyClaimed.GetValueOrDefault("Lunch", false);
        bool dinnerClaimed = dailyClaimed.GetValueOrDefault("Dinner", false);

        var available = new List<string>();
        string summary;

        switch (currentSession)
        {
            case "Breakfast":
                if (!breakfastClaimed) available.Add("Breakfast");
                summary = !breakfastClaimed
                    ? "Breakfast session active (07:00 - 10:00 WAT). Claim your morning meal."
                    : "Breakfast claimed. Lunch service begins at 12:00 WAT.";
                break;

            case "Lunch":
                if (!lunchClaimed) available.Add("Lunch");
                if (!breakfastClaimed) available.Add("Breakfast"); // Skipped breakfast rolled over!

                if (!breakfastClaimed && !lunchClaimed)
                    summary = "Lunch session active. Because you skipped breakfast, rollover is active: claim Breakfast + Lunch together!";
                else if (!lunchClaimed)
                    summary = "Lunch session active (12:00 - 15:30 WAT). Claim your afternoon meal.";
                else
                    summary = "Lunch claimed. Dinner service begins at 18:00 WAT.";
                break;

            case "Dinner":
                if (!dinnerClaimed) available.Add("Dinner");
                if (!lunchClaimed) available.Add("Lunch"); // Skipped lunch rolled over!
                if (!breakfastClaimed) available.Add("Breakfast"); // Skipped breakfast rolled over!

                if (!breakfastClaimed && !lunchClaimed && !dinnerClaimed)
                    summary = "Dinner session active. Full rollover active: you skipped breakfast and lunch, so you can order Breakfast, Lunch, and Dinner all at once!";
                else if (!lunchClaimed && !dinnerClaimed)
                    summary = "Dinner session active. Lunch rollover active: you can order Lunch + Dinner together!";
                else if (!breakfastClaimed && !dinnerClaimed)
                    summary = "Dinner session active. Breakfast rollover active: you can order Breakfast + Dinner together!";
                else if (!dinnerClaimed)
                    summary = "Dinner session active (18:00 - 21:00 WAT). Claim your evening meal.";
                else
                    summary = "All scholarship meal windows (Breakfast, Lunch, Dinner) have been claimed for today.";
                break;

            default:
                summary = "Cafeteria is currently closed. Service re-opens at 07:00 WAT.";
                break;
        }

        return (currentSession, available, summary);
    }

    private static string ResolveMealWindow(CafeteriaVendorOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.MealSession))
            return order.MealSession;

        if (string.IsNullOrWhiteSpace(order.MenuItemName))
            return "Lunch";

        var name = order.MenuItemName.ToLowerInvariant();
        if (name.Contains("breakfast") || name.Contains("egg") || name.Contains("toast") || name.Contains("pancake") || name.Contains("tea") || name.Contains("coffee"))
            return "Breakfast";
        if (name.Contains("dinner") || name.Contains("supper"))
            return "Dinner";

        return "Lunch";
    }

    private static string ResolveMealWindowFromInput(int feedingTimeId, string? menuItemName)
    {
        if (feedingTimeId == 1) return "Breakfast";
        if (feedingTimeId == 2) return "Lunch";
        if (feedingTimeId == 3) return "Dinner";

        if (!string.IsNullOrWhiteSpace(menuItemName))
        {
            var name = menuItemName.ToLowerInvariant();
            if (name.Contains("breakfast")) return "Breakfast";
            if (name.Contains("lunch")) return "Lunch";
            if (name.Contains("dinner") || name.Contains("supper")) return "Dinner";
        }

        return "Lunch";
    }

    private static TimeZoneInfo GetWatTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("W. Central Africa Standard Time"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("WAT", TimeSpan.FromHours(1), "WAT", "WAT"); }
        }
    }

    private static ScholarshipDto MapToDto(Scholarship s) => new(
        s.Id, s.Name, s.Description ?? "", s.Type, s.CoverageFlags, s.PercentageCovered,
        s.SponsorOrganizationId, s.SponsorOrganization?.Name, s.MinJambScore, s.MaxJambScore, s.IsActive, s.CreatedAt);

    private static StudentScholarshipDto MapToStudentScholarshipDto(StudentScholarship ss) => new(
        ss.Id, ss.StudentId, 
        ss.Student != null ? $"{ss.Student.FirstName} {ss.Student.LastName}" : null,
        ss.Student != null ? (ss.Student.JambRegistrationNumber ?? ss.Student.OfficialEmail) : null,
        ss.ScholarshipId, ss.SessionId, ss.CalculatedAmount, ss.CreatedAt,
        MapToDto(ss.Scholarship));
}
