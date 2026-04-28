using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Fees;

public sealed class GetStudentBillEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Get("/api/fees/bill/{studentId}/{sessionId}");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Registry");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentId = Route<Guid>("studentId");
        var sessionId = Route<Guid>("sessionId");

        // Ownership check: students can only access their own bill
        if (User.IsInRole("Student") &&
            !User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
            !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            var callerId = HttpContext.Items["CurrentUserId"] as Guid?;
            if (callerId != studentId)
            {
                await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only access your own fee bill.", ct);
                return;
            }
        }

        var record = await feeService.GetStudentBillAsync(studentId, sessionId);
        if (record == null)
        {
            await SendFailureAsync(404, "Bill not found", "NOT_FOUND", "No fee bill found for this student and session.", ct);
            return;
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

public sealed class GenerateStudentBillEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<StudentBillResponse>
{
    public override void Configure()
    {
        Post("/api/fees/bill/{studentId}/{sessionId}/generate");
        Roles("SuperAdmin", "Admin", "Finance", "Registry");
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
        Get("/api/fees/my-bill");
        Roles("Student");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Resolve caller identity from middleware
        if (HttpContext.Items["CurrentUserId"] is not Guid studentId)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "Could not resolve your identity.", ct);
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
            await SendFailureAsync(404, "Bill not found", "NOT_FOUND",
                "Your fee bill has not been generated yet. Please contact the Finance office.", ct);
            return;
        }

        await SendSuccessAsync(GetStudentBillEndpoint.MapBill(record), ct);
    }
}
