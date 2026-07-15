using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using LMS.Api.Endpoints;

namespace LMS.Api.Endpoints.Fees;

// ─── Initiate gateway payment ─────────────────────────────────────────────────

public sealed class InitiateGatewayPaymentEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpoint<InitiateGatewayPaymentRequest, GatewayInitResponse>
{
    public override void Configure()
    {
        Post("fees/payments/initiate");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Parent");
        Tags("Fees");
    }

    public override async Task HandleAsync(InitiateGatewayPaymentRequest req, CancellationToken ct)
    {
        var callerId = HttpContext.Items["CurrentUserId"] as Guid?;
        
        // Ownership check
        if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") && !User.IsInRole("Finance"))
        {
            var feeRecord = await db.StudentFeeRecords.FindAsync(new object[] { req.StudentFeeRecordId }, ct);
            if (feeRecord == null)
            {
                await SendFailureAsync(404, "Student fee record not found.", "NOT_FOUND", "Fee record not found.", ct);
                return;
            }

            if (User.IsInRole("Student"))
            {
                var resolvedStudentId = await db.Students
                    .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == callerId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                                s.OfficialEmail == db.Users.Where(u => u.Id == callerId).Select(u => u.Email).FirstOrDefault())
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync(ct);

                if (resolvedStudentId != feeRecord.StudentId)
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only initiate payment for your own fee record.", ct);
                    return;
                }
            }
            else if (User.IsInRole("Parent"))
            {
                if (callerId == null || !await db.ParentStudentLinks.AnyAsync(psl => psl.StudentId == feeRecord.StudentId && psl.ParentGuardian!.UserId == callerId.Value, ct))
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You are not linked to the student for this fee record.", ct);
                    return;
                }
            }
        }

        try
        {
            var result = await feeService.InitiateGatewayPaymentAsync(req);
            await SendSuccessAsync(result, ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_GATEWAY", ex.Message, ct);
        }
    }
}

// ─── Record manual payment (with optional receipt file) ───────────────────────

public sealed class RecordManualPaymentEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpoint<RecordManualPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Post("fees/payments/manual");
        AllowFileUploads();
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Parent");
        Tags("Fees");
    }

    public override async Task HandleAsync(RecordManualPaymentRequest req, CancellationToken ct)
    {
        var callerId = HttpContext.Items["CurrentUserId"] as Guid?;
        
        // Ownership check
        if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") && !User.IsInRole("Finance"))
        {
            var feeRecord = await db.StudentFeeRecords.FindAsync(new object[] { req.StudentFeeRecordId }, ct);
            if (feeRecord == null)
            {
                await SendFailureAsync(404, "Student fee record not found.", "NOT_FOUND", "Fee record not found.", ct);
                return;
            }

            if (User.IsInRole("Student"))
            {
                var resolvedStudentId = await db.Students
                    .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == callerId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                                s.OfficialEmail == db.Users.Where(u => u.Id == callerId).Select(u => u.Email).FirstOrDefault())
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync(ct);

                if (resolvedStudentId != feeRecord.StudentId)
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only submit manual payment for your own fee record.", ct);
                    return;
                }
            }
            else if (User.IsInRole("Parent"))
            {
                if (callerId == null || !await db.ParentStudentLinks.AnyAsync(psl => psl.StudentId == feeRecord.StudentId && psl.ParentGuardian!.UserId == callerId.Value, ct))
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You are not linked to the student for this fee record.", ct);
                    return;
                }
            }
        }

        string? receiptUrl = null;

        // If a receipt file was uploaded, save it (simplified: store in wwwroot/receipts)
        if (Files.Count > 0)
        {
            var file = Files[0];
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "receipts");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream, ct);
            receiptUrl = $"/receipts/{fileName}";
        }

        var payment = await feeService.RecordManualPaymentAsync(req, receiptUrl);
        await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
    }
}

// ─── Confirm / Reject ─────────────────────────────────────────────────────────

public sealed class ConfirmPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<ConfirmPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Patch("fees/payments/{id}/confirm");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(ConfirmPaymentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var confirmedBy = User.Identity?.Name ?? "Admin";
        try
        {
            var payment = await feeService.ConfirmPaymentAsync(id, confirmedBy);
            await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_STATE", ex.Message, ct);
        }
    }
}

public sealed class RejectPaymentEndpoint(IFeeService feeService)
    : ApiEndpoint<RejectPaymentRequest, FeePaymentResponse>
{
    public override void Configure()
    {
        Patch("fees/payments/{id}/reject");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(RejectPaymentRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            var payment = await feeService.RejectPaymentAsync(id, req.RejectionReason);
            await SendSuccessAsync(FeeMapper.ToPaymentResponse(payment), ct);
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, ex.Message, "NOT_FOUND", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, ex.Message, "INVALID_STATE", ex.Message, ct);
        }
    }
}

// ─── Payment History ──────────────────────────────────────────────────────────

public sealed class GetPaymentHistoryEndpoint(IFeeService feeService, LmsDbContext db)
    : ApiEndpointWithoutRequest<IEnumerable<FeePaymentResponse>>
{
    public override void Configure()
    {
        Get("fees/payments/student/{studentId}");
        Roles("SuperAdmin", "Admin", "Finance", "Student", "Registry", "Parent");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var studentIdStr = Route<string>("studentId");
        var callerId = HttpContext.Items["CurrentUserId"] as Guid?;

        Guid? parsedStudentId = Guid.TryParse(studentIdStr, out var g) ? g : null;

        // Resolve routeStudentId which might be an AppUser.Id or a Student.Id or an EntraObjectId
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

        // Ownership check: students can only access their own payment history, parents can only access linked student
        if (!User.IsInRole("SuperAdmin") && !User.IsInRole("Admin") &&
            !User.IsInRole("Finance") && !User.IsInRole("Registry"))
        {
            if (User.IsInRole("Student"))
            {
                var resolvedCallerId = await db.Students
                    .Where(s => s.EntraObjectId == db.Users.Where(u => u.Id == callerId).Select(u => u.EntraObjectId).FirstOrDefault() ||
                                s.OfficialEmail == db.Users.Where(u => u.Id == callerId).Select(u => u.Email).FirstOrDefault())
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync(ct);

                if (resolvedCallerId != actualStudentId)
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You can only access your own payment history.", ct);
                    return;
                }
            }
            else if (User.IsInRole("Parent"))
            {
                if (callerId == null || !await db.ParentStudentLinks.AnyAsync(psl => psl.StudentId == actualStudentId && psl.ParentGuardian!.UserId == callerId.Value, ct))
                {
                    await SendFailureAsync(403, "Access denied", "FORBIDDEN", "You are not linked to this student.", ct);
                    return;
                }
            }
        }

        var payments = await feeService.GetPaymentHistoryAsync(actualStudentId);
        await SendSuccessAsync(payments.Select(FeeMapper.ToPaymentResponse), ct);
    }
}

public sealed class GetAllPaymentsEndpoint(IFeeService feeService)
    : ApiEndpointWithoutRequest<IEnumerable<FeePaymentResponse>>
{
    public override void Configure()
    {
        Get("fees/payments");
        Roles("SuperAdmin", "Admin", "Finance");
        Tags("Fees");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var statusStr = Query<string?>("status", isRequired: false);
        var sessionIdStr = Query<string?>("sessionId", isRequired: false);
        var methodStr = Query<string?>("method", isRequired: false);
        Data.Enums.PaymentStatus? status = statusStr != null && Enum.TryParse<Data.Enums.PaymentStatus>(statusStr, true, out var s) ? s : null;
        Guid? sessionId = sessionIdStr != null && Guid.TryParse(sessionIdStr, out var g) ? g : null;
        var payments = await feeService.GetAllPaymentsAsync(status, sessionId, methodStr);
        await SendSuccessAsync(payments.Select(FeeMapper.ToPaymentResponse), ct);
    }
}

