using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

/// <summary>
/// Policy-driven authorization for parent/guardian access to student data.
/// Effective permission = parent is linked to the student
///   AND the category is enabled in the institution configuration
///   AND the student has not explicitly denied the category
///   AND (if configured) sensitive categories / adult students require explicit student consent.
/// </summary>
public sealed class ParentAccessAuthorizationService : IParentAuthorizationService
{
    private readonly LmsDbContext _context;

    public ParentAccessAuthorizationService(LmsDbContext context)
    {
        _context = context;
    }

    public async Task<ErrorOr<bool>> CanAccessAsync(
        Guid parentId, Guid studentId, ParentAccessCategory category, CancellationToken ct = default)
    {
        var link = await _context.ParentStudentLinks
            .FirstOrDefaultAsync(l => l.ParentGuardianId == parentId && l.StudentId == studentId, ct);
        if (link == null)
            return Error.Forbidden("AccessDenied", "Parent is not linked to this student.");

        var config = await _context.SystemParentPortalConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var allowed = ParseAllowedCategories(config?.AllowedCategoriesJson);
        if (!allowed.Contains((int)category))
            return Error.Forbidden("CategoryDisabled", "This information category is not enabled for parent access.");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        var dob = student?.AdmissionApplication?.DateOfBirth;
        var isAdult = dob != null && Age(dob.Value) >= 18;

        var consent = await _context.ParentStudentConsents
            .FirstOrDefaultAsync(c => c.StudentId == studentId && c.Category == (int)category, ct);

        // Explicit student denial overrides everything.
        if (consent != null && !consent.IsAllowed)
            return Error.Forbidden("ConsentDenied", "The student has denied parent access to this category.");

        var sensitive = category is ParentAccessCategory.PrivateMessages
                        or ParentAccessCategory.LecturerConversations;

        if (config?.RequireStudentConsentForSensitive == true && sensitive
            && (consent == null || !consent.IsAllowed))
            return Error.Forbidden("ConsentRequired", "Student consent is required for this category.");

        if (config?.TreatAdultStudentsAsRestricted == true && isAdult
            && (consent == null || !consent.IsAllowed))
            return Error.Forbidden("AdultRestricted", "Access requires the student's explicit consent.");

        return true;
    }

    private static List<int> ParseAllowedCategories(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return Enum.GetValues<ParentAccessCategory>().Cast<int>().ToList();

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int>>(json);
            return parsed ?? new List<int>();
        }
        catch
        {
            return Enum.GetValues<ParentAccessCategory>().Cast<int>().ToList();
        }
    }

    private static int Age(DateTime dob)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }
}
