using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Students.ProfileCompletion;

public sealed class GetProfileCompletionStatusEndpoint(
    LmsDbContext dbContext,
    ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<StudentProfileCompletionStatusResponse>
{
    public override void Configure()
    {
        Get("student/profile-completion-status");
        Roles("Student", "SuperAdmin", "Admin");
        Tags("StudentProfileCompletion");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var student = await ResolveCurrentStudentAsync(ct);
        if (student == null)
        {
            await SendSuccessAsync(new StudentProfileCompletionStatusResponse
            {
                IsRequired = false,
                MissingFields = []
            }, ct);
            return;
        }

        // Find active requirements matching this student's profile
        var matchingReqs = await dbContext.ProfileCompletionRequirements
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.AcademicSessionId == null || r.AcademicSessionId == student.AcademicSessionId)
            .Where(r => r.AcademicLevelId == null || r.AcademicLevelId == student.LevelId)
            .Where(r => r.FacultyId == null || r.FacultyId == student.FacultyId)
            .Where(r => r.AcademicProgramId == null || r.AcademicProgramId == student.AcademicProgramId)
            .ToListAsync(ct);

        if (matchingReqs.Count == 0)
        {
            await SendSuccessAsync(new StudentProfileCompletionStatusResponse
            {
                IsRequired = false,
                MissingFields = []
            }, ct);
            return;
        }

        // Fetch linked AdmissionApplication for full record review
        AdmissionApplication? app = null;
        if (student.AdmissionApplicationId.HasValue)
        {
            app = await dbContext.AdmissionApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == student.AdmissionApplicationId.Value, ct);
        }

        if (app == null && !string.IsNullOrWhiteSpace(student.PersonalEmail))
        {
            app = await dbContext.AdmissionApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.StudentEmail == student.PersonalEmail || a.OfficialEmail == student.OfficialEmail, ct);
        }

        // Parse address from EmergencyContactJson
        string? existingAddress = null;
        if (app != null && !string.IsNullOrWhiteSpace(app.EmergencyContactJson))
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(app.EmergencyContactJson);
                if (jsonDoc.RootElement.TryGetProperty("address", out var addrProp))
                {
                    existingAddress = addrProp.GetString();
                }
            }
            catch
            {
                // ignore json parse error
            }
        }

        // Gather all required field keys requested by active campaigns
        var requiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in matchingReqs)
        {
            if (!string.IsNullOrWhiteSpace(req.RequiredFieldsJson))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<List<string>>(req.RequiredFieldsJson);
                    if (keys != null)
                    {
                        foreach (var k in keys) requiredKeys.Add(k);
                    }
                }
                catch
                {
                    // ignore format errors
                }
            }
        }

        var missingFields = new List<MissingFieldDescriptor>();

        if (requiredKeys.Contains("gender") && string.IsNullOrWhiteSpace(student.Gender) && string.IsNullOrWhiteSpace(app?.Gender))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "gender",
                Label = "Gender",
                Type = "select",
                Options = ["Male", "Female", "Other"],
                Required = true
            });
        }

        if (requiredKeys.Contains("dateOfBirth") && (app == null || !app.DateOfBirth.HasValue))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "dateOfBirth",
                Label = "Date of Birth",
                Type = "date",
                Required = true
            });
        }

        if (requiredKeys.Contains("phone") && string.IsNullOrWhiteSpace(student.Phone) && string.IsNullOrWhiteSpace(app?.Phone))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "phone",
                Label = "Phone Number",
                Type = "tel",
                Placeholder = "e.g. +234 801 234 5678",
                Required = true
            });
        }

        if (requiredKeys.Contains("address") && string.IsNullOrWhiteSpace(existingAddress))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "address",
                Label = "Residential Address",
                Type = "text",
                Placeholder = "Street address, City, State",
                Required = true
            });
        }

        if (requiredKeys.Contains("emergencyContactName") && string.IsNullOrWhiteSpace(student.EmergencyContactName) && string.IsNullOrWhiteSpace(app?.EmergencyContactName))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "emergencyContactName",
                Label = "Next of Kin / Emergency Contact Full Name",
                Type = "text",
                Required = true
            });
        }

        if (requiredKeys.Contains("emergencyContactPhone") && string.IsNullOrWhiteSpace(student.EmergencyContactPhone) && string.IsNullOrWhiteSpace(app?.EmergencyContactPhone))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "emergencyContactPhone",
                Label = "Next of Kin / Emergency Contact Phone",
                Type = "tel",
                Required = true
            });
        }

        if (requiredKeys.Contains("emergencyContactEmail") && string.IsNullOrWhiteSpace(student.EmergencyContactEmail) && string.IsNullOrWhiteSpace(app?.EmergencyContactEmail))
        {
            missingFields.Add(new MissingFieldDescriptor
            {
                Key = "emergencyContactEmail",
                Label = "Next of Kin / Emergency Contact Email",
                Type = "email",
                Required = true
            });
        }

        if (missingFields.Count == 0)
        {
            await SendSuccessAsync(new StudentProfileCompletionStatusResponse
            {
                IsRequired = false,
                MissingFields = []
            }, ct);
            return;
        }

        var primary = matchingReqs[0];
        var isBlocking = matchingReqs.Any(r => r.IsBlocking);
        var deadline = matchingReqs.Where(r => r.Deadline.HasValue).Select(r => r.Deadline).Min();

        await SendSuccessAsync(new StudentProfileCompletionStatusResponse
        {
            IsRequired = true,
            RequirementId = primary.Id,
            Title = primary.Title,
            Description = primary.Description,
            IsBlocking = isBlocking,
            Deadline = deadline,
            MissingFields = missingFields
        }, ct);
    }

    private async Task<LMS.Api.Data.Entities.Student?> ResolveCurrentStudentAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (userId.HasValue)
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.Id == userId.Value, ct);
            if (student != null) return student;

            var user = await dbContext.Users.FindAsync([userId.Value], ct);
            if (user != null)
            {
                student = await dbContext.Students
                    .Include(s => s.AcademicProgram)
                    .Include(s => s.Level)
                    .FirstOrDefaultAsync(s => s.OfficialEmail == user.Email || s.PersonalEmail == user.Email, ct);
                if (student != null) return student;
            }
        }

        var entraId = currentUserContext.GetEntraObjectId();
        if (!string.IsNullOrWhiteSpace(entraId))
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.EntraObjectId == entraId, ct);
            if (student != null) return student;
        }

        var email = HttpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? HttpContext.User.FindFirstValue("email")
            ?? HttpContext.User.FindFirstValue("preferred_username");
        if (!string.IsNullOrWhiteSpace(email))
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.OfficialEmail == email || s.PersonalEmail == email, ct);
            if (student != null) return student;
        }

        return null;
    }
}

public sealed class SubmitProfileCompletionEndpoint(
    LmsDbContext dbContext,
    ICurrentUserContext currentUserContext)
    : ApiEndpoint<SubmitProfileCompletionRequest, SubmitProfileCompletionResponse>
{
    public override void Configure()
    {
        Post("student/submit-profile-completion");
        Roles("Student", "SuperAdmin", "Admin");
        Tags("StudentProfileCompletion");
    }

    public override async Task HandleAsync(SubmitProfileCompletionRequest req, CancellationToken ct)
    {
        var student = await ResolveCurrentStudentWithTrackingAsync(ct);
        if (student == null)
        {
            await SendFailureAsync(404, "StudentNotFound", "NOT_FOUND", "No student profile was found for this user account.", ct);
            return;
        }

        AdmissionApplication? app = null;
        if (student.AdmissionApplicationId.HasValue)
        {
            app = await dbContext.AdmissionApplications.FirstOrDefaultAsync(a => a.Id == student.AdmissionApplicationId.Value, ct);
        }

        if (app == null && !string.IsNullOrWhiteSpace(student.PersonalEmail))
        {
            app = await dbContext.AdmissionApplications
                .FirstOrDefaultAsync(a => a.StudentEmail == student.PersonalEmail || a.OfficialEmail == student.OfficialEmail, ct);
            if (app != null && !student.AdmissionApplicationId.HasValue)
            {
                student.AdmissionApplicationId = app.Id;
            }
        }

        // Apply Gender
        if (!string.IsNullOrWhiteSpace(req.Gender))
        {
            var normalizedGender = req.Gender.Trim();
            student.Gender = normalizedGender;
            if (app != null) app.Gender = normalizedGender;
        }

        // Apply DateOfBirth
        if (req.DateOfBirth.HasValue && app != null)
        {
            app.DateOfBirth = req.DateOfBirth.Value;
        }

        // Apply Phone
        if (!string.IsNullOrWhiteSpace(req.Phone))
        {
            var normalizedPhone = req.Phone.Trim();
            student.Phone = normalizedPhone;
            if (app != null) app.Phone = normalizedPhone;
        }

        // Apply Emergency Contact
        if (!string.IsNullOrWhiteSpace(req.EmergencyContactName))
        {
            var name = req.EmergencyContactName.Trim();
            student.EmergencyContactName = name;
            if (app != null) app.EmergencyContactName = name;
        }

        if (!string.IsNullOrWhiteSpace(req.EmergencyContactPhone))
        {
            var phone = req.EmergencyContactPhone.Trim();
            student.EmergencyContactPhone = phone;
            if (app != null) app.EmergencyContactPhone = phone;
        }

        if (!string.IsNullOrWhiteSpace(req.EmergencyContactEmail))
        {
            var email = req.EmergencyContactEmail.Trim();
            student.EmergencyContactEmail = email;
            if (app != null) app.EmergencyContactEmail = email;
        }

        // Apply Address to application EmergencyContactJson
        if (app != null && (!string.IsNullOrWhiteSpace(req.Address) || !string.IsNullOrWhiteSpace(req.City) || !string.IsNullOrWhiteSpace(req.State)))
        {
            Dictionary<string, object> contactDict = new();
            if (!string.IsNullOrWhiteSpace(app.EmergencyContactJson))
            {
                try
                {
                    contactDict = JsonSerializer.Deserialize<Dictionary<string, object>>(app.EmergencyContactJson) ?? new();
                }
                catch
                {
                    contactDict = new();
                }
            }

            if (!string.IsNullOrWhiteSpace(req.Address)) contactDict["address"] = req.Address.Trim();
            if (!string.IsNullOrWhiteSpace(req.City)) contactDict["city"] = req.City.Trim();
            if (!string.IsNullOrWhiteSpace(req.State)) contactDict["state"] = req.State.Trim();
            if (!string.IsNullOrWhiteSpace(req.Country)) contactDict["country"] = req.Country.Trim();
            if (!string.IsNullOrWhiteSpace(req.EmergencyRelationship)) contactDict["relationship"] = req.EmergencyRelationship.Trim();
            if (!string.IsNullOrWhiteSpace(req.EmergencyContactName)) contactDict["name"] = req.EmergencyContactName.Trim();
            if (!string.IsNullOrWhiteSpace(req.EmergencyContactPhone)) contactDict["phone"] = req.EmergencyContactPhone.Trim();
            if (!string.IsNullOrWhiteSpace(req.EmergencyContactEmail)) contactDict["email"] = req.EmergencyContactEmail.Trim();

            app.EmergencyContactJson = JsonSerializer.Serialize(contactDict);
        }

        student.UpdatedAt = DateTime.UtcNow;
        if (app != null) app.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        await SendSuccessAsync(new SubmitProfileCompletionResponse
        {
            Success = true,
            Message = "Profile updated successfully.",
            IsRequired = false,
            MissingFieldsRemaining = []
        }, ct);
    }

    private async Task<LMS.Api.Data.Entities.Student?> ResolveCurrentStudentWithTrackingAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (userId.HasValue)
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.Id == userId.Value, ct);
            if (student != null) return student;

            var user = await dbContext.Users.FindAsync([userId.Value], ct);
            if (user != null)
            {
                student = await dbContext.Students
                    .Include(s => s.AcademicProgram)
                    .Include(s => s.Level)
                    .FirstOrDefaultAsync(s => s.OfficialEmail == user.Email || s.PersonalEmail == user.Email, ct);
                if (student != null) return student;
            }
        }

        var entraId = currentUserContext.GetEntraObjectId();
        if (!string.IsNullOrWhiteSpace(entraId))
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.EntraObjectId == entraId, ct);
            if (student != null) return student;
        }

        var email = HttpContext.User.FindFirstValue(ClaimTypes.Email)
            ?? HttpContext.User.FindFirstValue("email")
            ?? HttpContext.User.FindFirstValue("preferred_username");
        if (!string.IsNullOrWhiteSpace(email))
        {
            var student = await dbContext.Students
                .Include(s => s.AcademicProgram)
                .Include(s => s.Level)
                .FirstOrDefaultAsync(s => s.OfficialEmail == email || s.PersonalEmail == email, ct);
            if (student != null) return student;
        }

        return null;
    }
}
