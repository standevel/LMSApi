using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Fees;

public sealed class GetStudentBillEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Get("fees/bill/{studentId}/{sessionId}");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Registry", "Parent");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentIdStr = Route<string>("studentId");
        var sessionId = Route<Guid>("sessionId");
        var callerId = HttpContext.Items["CurrentUserId"] as Guid?;

        Guid? parsedStudentId = Guid.TryParse(studentIdStr, out var g) ? g : null;

        var actualStudentId = await db.Students
            .Where(s => (parsedStudentId != null && s.Id == parsedStudentId) ||
                        s.EntraObjectId == studentIdStr ||
                        (parsedStudentId != null && s.EntraObjectId == db.Users.Where(u => u.Id == parsedStudentId).Select(u => u.EntraObjectId).FirstOrDefault()) ||
                        (parsedStudentId != null && s.OfficialEmail == db.Users.Where(u => u.Id == parsedStudentId).Select(u => u.Email).FirstOrDefault()))
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (actualStudentId == Guid.Empty)
        {
            await SendFailureAsync(404, "Student not found", "NOT_FOUND", "The student could not be found.", ct);
            return;
        }

        // Ownership check: students can only access their own bill, parents can only access linked student bills
        if (User.IsInRole("Student") &&
            !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
            !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            var resolvedCallerId = await db.Students
                .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == callerId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                            s.OfficialEmail == db.Users.Where(u => u.Id == callerId).Select(u => u.Email).FirstOrDefault())
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (resolvedCallerId != actualStudentId)
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only access your own fee bill.", ct);
                return;
            }
        }
        else if (User.IsInRole("Parent") &&
                 !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
                 !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            if (callerId == null || !await db.ParentStudentLinks.AnyAsync(psl => psl.StudentId == actualStudentId && psl.ParentGuardian!.UserId == callerId.Value, ct))
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You are not linked to this student.", ct);
                return;
            }
        }

        var record = await feeService.GetStudentBillAsync(actualStudentId, sessionId);
        if (record == null)
        {
            record = await feeService.GenerateStudentBillAsync(actualStudentId, sessionId);
            if (record is null)
            {
                await SendFailureAsync(404, "Bill not found", "NOT_FOUND", "No fee bill found for this student and session.", ct);
                return;
            }
        }

        await SendSuccessAsync(MapBill(record), ct);
    }

    internal static StudentBillResponse MapBill(Data.Entities.StudentFeeRecord record) => new(
        record.Id,
        record.StudentId,
        GetStudentDisplayName(record.Student),
        record.SessionId,
        record.Session?.Name ?? "",
        record.TotalAmount,
        record.ScholarshipDiscount,
        record.TotalAmount - record.ScholarshipDiscount,
        record.AmountPaid,
        record.Balance,
        record.LateFeeApplied,
        record.LateFeeTotal,
        record.Status.ToString(),
        record.GeneratedAt,
        [],
        record.Payments.Select(FeeMapper.ToPaymentResponse),
        record.LateFeeApplications.Select(l => new LateFeeApplicationResponse(
            l.Id, l.FeeTemplateId, l.FeeTemplate?.Name ?? "",
            l.AmountCharged, l.FeeType.ToString(),
            l.BaseRateUsed, l.EffectiveDueDate, l.AppliedAt, l.AppliedBy))
    );

    private static string GetStudentDisplayName(Student? student)
    {
        if (student == null) return "";
        var name = $"{student.FirstName} {student.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? student.OfficialEmail : name;
    }
}

public sealed class GetStudentBillActiveSessionEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Get("fees/bill/{studentId}");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Registry", "Parent");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentIdStr = Route<string>("studentId");
        var callerId = HttpContext.Items["CurrentUserId"] as Guid?;

        Guid? parsedStudentId = Guid.TryParse(studentIdStr, out var g) ? g : null;

        var actualStudentId = await db.Students
            .Where(s => (parsedStudentId != null && s.Id == parsedStudentId) ||
                        s.EntraObjectId == studentIdStr ||
                        (parsedStudentId != null && s.EntraObjectId == db.Users.Where(u => u.Id == parsedStudentId).Select(u => u.EntraObjectId).FirstOrDefault()) ||
                        (parsedStudentId != null && s.OfficialEmail == db.Users.Where(u => u.Id == parsedStudentId).Select(u => u.Email).FirstOrDefault()))
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (actualStudentId == Guid.Empty)
        {
            await SendFailureAsync(404, "Student not found", "NOT_FOUND", "The student could not be found.", ct);
            return;
        }

        // Ownership check: students can only access their own bill, parents can only access linked student bills
        if (User.IsInRole("Student") &&
            !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
            !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            var resolvedCallerId = await db.Students
                .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == callerId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                            s.OfficialEmail == db.Users.Where(u => u.Id == callerId).Select(u => u.Email).FirstOrDefault())
                .Select(s => s.Id)
                .FirstOrDefaultAsync(ct);

            if (resolvedCallerId != actualStudentId)
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only access your own fee bill.", ct);
                return;
            }
        }
        else if (User.IsInRole("Parent") &&
                 !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
                 !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            if (callerId == null || !await db.ParentStudentLinks.AnyAsync(psl => psl.StudentId == actualStudentId && psl.ParentGuardian!.UserId == callerId.Value, ct))
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You are not linked to this student.", ct);
                return;
            }
        }

        // Resolve active session
        var activeSession = await db.AcademicSessions
            .Where(s => s.IsActive)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (activeSession is null)
        {
            await SendFailureAsync(400, "No active session found.", "NO_ACTIVE_SESSION",
                "No active academic session found. Please contact the Registry.", ct);
            return;
        }

        var record = await feeService.GetStudentBillAsync(actualStudentId, activeSession.Value);
        if (record == null)
        {
            record = await feeService.GenerateStudentBillAsync(actualStudentId, activeSession.Value);
            if (record is null)
            {
                await SendFailureAsync(404, "Bill not found", "NOT_FOUND", "No fee bill found for this student and session.", ct);
                return;
            }
        }

        await SendSuccessAsync(GetStudentBillEndpoint.MapBill(record), ct);
    }
}

public sealed class GenerateStudentBillEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Post("fees/bill/{studentId}/{sessionId}/generate");
        Roles("SuperAdmin", "Admin", "Finance", "Registry");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = Route<Guid>("studentId");
        var sessionId = Route<Guid>("sessionId");
        await feeService.GenerateStudentBillAsync(studentId, sessionId);
        var full = await feeService.GetStudentBillAsync(studentId, sessionId);
        await SendSuccessAsync(GetStudentBillEndpoint.MapBill(full!), ct);
    }
}

public sealed class GetMyBillEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Get("fees/my-bill");
        Roles("Student");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Resolve caller identity from middleware
        if (HttpContext.Items["CurrentUserId"] is not Guid appUserId)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
            return;
        }

        var studentId = await db.Students
            .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == appUserId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                        s.OfficialEmail == db.Users.Where(u => u.Id == appUserId).Select(u => u.Email).FirstOrDefault())
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (studentId == Guid.Empty)
        {
            await SendFailureAsync(404, "Student not found", "NOT_FOUND", "Your student profile could not be found.", ct);
            return;
        }

        // Resolve sessionId — from query param or active session
        var sessionIdStr = Query<string?>("sessionId", isRequired: false);
        Guid sessionId;

        if (!string.IsNullOrWhiteSpace(sessionIdStr) && Guid.TryParse(sessionIdStr, out var parsedId))
        {
            sessionId = parsedId;
        }
        else
        {
            var activeSession = await db.AcademicSessions
                .Where(s => s.IsActive)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(ct);

            if (activeSession is null)
            {
                await SendFailureAsync(400, "No active session found.", "NO_ACTIVE_SESSION",
                    "No active academic session found. Please contact the Registry.", ct);
                return;
            }

            sessionId = activeSession.Value;
        }

        var record = await feeService.GetStudentBillAsync(studentId, sessionId);
        if (record is null)
        {
            record = await feeService.GenerateStudentBillAsync(studentId, sessionId);
            if (record is null)
            {
                await SendFailureAsync(404, "Bill not found", "NOT_FOUND",
                    "Your fee bill could not be generated. Please contact the Finance office.", ct);
                return;
            }
        }

        await SendSuccessAsync(GetStudentBillEndpoint.MapBill(record), ct);
    }
}
