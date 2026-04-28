using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class FeeService(
    LmsDbContext db,
    PaystackService paystackService,
    HydrogenService hydrogenService) : IFeeService
{
    // ─── Templates ──────────────────────────────────────────────────────────

    public async Task<FeeTemplate> CreateTemplateAsync(CreateFeeTemplateRequest req)
    {
        var template = new FeeTemplate
        {
            Name = req.Name,
            Description = req.Description,
            FeeCategoryId = req.FeeCategoryId,
            Scope = req.Scope,
            SessionId = req.SessionId,
            FacultyId = req.FacultyId,
            ProgramId = req.ProgramId,
            DueDate = req.DueDate,
            LateFeeType = req.LateFeeType,
            LateFeeAmount = req.LateFeeAmount
        };
        db.FeeTemplates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    public async Task<FeeTemplate> UpdateTemplateAsync(Guid id, UpdateFeeTemplateRequest req)
    {
        var t = await db.FeeTemplates.FindAsync(id)
            ?? throw new KeyNotFoundException("Fee template not found.");
        t.Name = req.Name;
        t.Description = req.Description;
        t.FeeCategoryId = req.FeeCategoryId;
        t.SessionId = req.SessionId;
        t.FacultyId = req.FacultyId;
        t.ProgramId = req.ProgramId;
        t.DueDate = req.DueDate;
        t.LateFeeType = req.LateFeeType;
        t.LateFeeAmount = req.LateFeeAmount;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return t;
    }

    public async Task<FeeTemplate> ToggleTemplateAsync(Guid id)
    {
        var t = await db.FeeTemplates.FindAsync(id)
            ?? throw new KeyNotFoundException("Fee template not found.");
        t.IsActive = !t.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return t;
    }

    public async Task<IEnumerable<FeeTemplate>> GetTemplatesAsync(bool? activeOnly = null)
    {
        var q = db.FeeTemplates
            .Include(t => t.LineItems)
            .Include(t => t.Category)
            .Include(t => t.Session)
            .Include(t => t.Faculty)
            .Include(t => t.Program)
            .AsQueryable();
        if (activeOnly.HasValue) q = q.Where(t => t.IsActive == activeOnly.Value);
        return await q.OrderBy(t => t.Category.Name).ThenBy(t => t.Name).ToListAsync();
    }

    public async Task<FeeTemplate?> GetTemplateByIdAsync(Guid id)
    {
        return await db.FeeTemplates
            .Include(t => t.LineItems)
            .Include(t => t.Category)
            .Include(t => t.Session)
            .Include(t => t.Faculty)
            .Include(t => t.Program)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Faculty)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Program)
            .Include(t => t.Assignments)
                .ThenInclude(a => a.Student)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // ─── Categories ─────────────────────────────────────────────────────────

    public async Task<FeeCategory> CreateCategoryAsync(CreateFeeCategoryRequest req)
    {
        var c = new FeeCategory { Name = req.Name, Description = req.Description };
        db.FeeCategories.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<FeeCategory> UpdateCategoryAsync(Guid id, UpdateFeeCategoryRequest req)
    {
        var c = await db.FeeCategories.FindAsync(id)
            ?? throw new KeyNotFoundException("Category not found.");
        c.Name = req.Name;
        c.Description = req.Description;
        c.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<FeeCategory> ToggleCategoryAsync(Guid id)
    {
        var c = await db.FeeCategories.FindAsync(id)
            ?? throw new KeyNotFoundException("Category not found.");
        c.IsActive = !c.IsActive;
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<IEnumerable<FeeCategory>> GetCategoriesAsync(bool? activeOnly = true)
    {
        var q = db.FeeCategories.AsQueryable();
        if (activeOnly == true) q = q.Where(c => c.IsActive == activeOnly.Value);
        return await q.OrderBy(c => c.Name).ToListAsync();
    }

    // ─── Line Items ──────────────────────────────────────────────────────────

    public async Task<FeeLineItem> AddLineItemAsync(Guid templateId, AddFeeLineItemRequest req)
    {
        var template = await db.FeeTemplates.FindAsync(templateId)
            ?? throw new KeyNotFoundException("Fee template not found.");
        var item = new FeeLineItem
        {
            FeeTemplateId = templateId,
            Name = req.Name,
            Description = req.Description,
            Amount = req.Amount,
            IsOptional = req.IsOptional,
            SortOrder = req.SortOrder
        };
        db.FeeLineItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<FeeLineItem> UpdateLineItemAsync(Guid itemId, UpdateFeeLineItemRequest req)
    {
        var item = await db.FeeLineItems.FindAsync(itemId)
            ?? throw new KeyNotFoundException("Fee line item not found.");
        item.Name = req.Name;
        item.Description = req.Description;
        item.Amount = req.Amount;
        item.IsOptional = req.IsOptional;
        item.SortOrder = req.SortOrder;
        await db.SaveChangesAsync();
        return item;
    }

    public async Task DeleteLineItemAsync(Guid itemId)
    {
        var item = await db.FeeLineItems.FindAsync(itemId)
            ?? throw new KeyNotFoundException("Fee line item not found.");
        db.FeeLineItems.Remove(item);
        await db.SaveChangesAsync();
    }

    // ─── Assignments ─────────────────────────────────────────────────────────

    public async Task<FeeAssignment> AssignFeeAsync(AssignFeeRequest req)
    {
        var assignment = new FeeAssignment
        {
            FeeTemplateId = req.FeeTemplateId,
            Scope = req.Scope,
            FacultyId = req.FacultyId,
            ProgramId = req.ProgramId,
            StudentId = req.StudentId,
            SessionId = req.SessionId,
            AmountOverride = req.AmountOverride,
            DueDateOverride = req.DueDateOverride
        };
        db.FeeAssignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    public async Task<IEnumerable<FeeAssignment>> GetAssignmentsAsync(Guid? templateId = null, Guid? sessionId = null)
    {
        var q = db.FeeAssignments
            .Include(a => a.FeeTemplate).ThenInclude(t => t.LineItems)
            .Include(a => a.Faculty)
            .Include(a => a.Program)
            .Include(a => a.Student)
            .Include(a => a.Session)
            .AsQueryable();
        if (templateId.HasValue) q = q.Where(a => a.FeeTemplateId == templateId.Value);
        if (sessionId.HasValue) q = q.Where(a => a.SessionId == sessionId.Value);
        return await q.ToListAsync();
    }

    public async Task DeleteAssignmentAsync(Guid id)
    {
        var a = await db.FeeAssignments.FindAsync(id)
            ?? throw new KeyNotFoundException("Fee assignment not found.");
        db.FeeAssignments.Remove(a);
        await db.SaveChangesAsync();
    }

    // ─── Student Bills ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates or recalculates a student's fee bill for a session.
    /// Merges fees from University → Faculty → Program → Student scope (highest scope wins for overrides).
    /// </summary>
    public async Task<StudentFeeRecord> GenerateStudentBillAsync(Guid studentId, Guid sessionId)
    {
        var existing = await db.StudentFeeRecords
            .Include(r => r.Payments)
            .Include(r => r.LateFeeApplications)
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.SessionId == sessionId);

        // Load student's program/faculty for assignment matching
        var enrollment = await db.Enrollments
            .Include(e => e.Program).ThenInclude(p => p.Faculty)
            .FirstOrDefaultAsync(e => e.UserId == studentId && e.AcademicSessionId == sessionId);

        Guid? facultyId = enrollment?.Program?.FacultyId;
        Guid? programId = enrollment?.ProgramId;

        // Get all active templates
        var allTemplates = await db.FeeTemplates
            .Include(t => t.LineItems)
            .Where(t => t.IsActive && (t.SessionId == null || t.SessionId == sessionId))
            .ToListAsync();

        // Get all active assignments for this student
        var assignments = await db.FeeAssignments
            .Include(a => a.FeeTemplate).ThenInclude(t => t.LineItems)
            .Where(a => a.IsActive &&
                (a.SessionId == null || a.SessionId == sessionId) &&
                (a.Scope == FeeScope.University ||
                 (a.Scope == FeeScope.Faculty && a.FacultyId == facultyId) ||
                 (a.Scope == FeeScope.Program && a.ProgramId == programId) ||
                 (a.Scope == FeeScope.Student && a.StudentId == studentId)))
            .ToListAsync();

        // Collect applicable templates (from assignments), deduplicate by template id
        // A student-level assignment for a template overrides program overrides amount, etc.
        var templateAmounts = new Dictionary<Guid, decimal>();
        foreach (var scope in new[] { FeeScope.University, FeeScope.Faculty, FeeScope.Program, FeeScope.Student })
        {
            foreach (var a in assignments.Where(a => a.Scope == scope))
            {
                var amount = a.AmountOverride ?? a.FeeTemplate.LineItems.Sum(li => li.Amount);
                templateAmounts[a.FeeTemplateId] = amount;
            }
        }

        // Also include University-scope templates with no explicit assignment
        foreach (var t in allTemplates.Where(t => t.Scope == FeeScope.University && !templateAmounts.ContainsKey(t.Id)))
        {
            templateAmounts[t.Id] = t.LineItems.Sum(li => li.Amount);
        }

        decimal total = templateAmounts.Values.Sum();

        if (existing == null)
        {
            existing = new StudentFeeRecord
            {
                StudentId = studentId,
                SessionId = sessionId,
                TotalAmount = total,
                Status = FeeRecordStatus.Outstanding
            };
            db.StudentFeeRecords.Add(existing);
        }
        else
        {
            // Preserve paid amount, recalculate total
            existing.TotalAmount = total + existing.LateFeeTotal;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<StudentFeeRecord?> GetStudentBillAsync(Guid studentId, Guid sessionId)
    {
        return await db.StudentFeeRecords
            .Include(r => r.Student)
            .Include(r => r.Session)
            .Include(r => r.Payments)
            .Include(r => r.LateFeeApplications)
                .ThenInclude(l => l.FeeTemplate)
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.SessionId == sessionId);
    }

    public async Task<IEnumerable<StudentFeeRecord>> GetAllBillsAsync(Guid? sessionId = null, FeeRecordStatus? status = null)
    {
        var q = db.StudentFeeRecords
            .Include(r => r.Student)
            .Include(r => r.Session)
            .AsQueryable();
        if (sessionId.HasValue) q = q.Where(r => r.SessionId == sessionId.Value);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        return await q.OrderByDescending(r => r.GeneratedAt).ToListAsync();
    }

    // ─── Payments — Gateway ───────────────────────────────────────────────────

    public async Task<GatewayInitResponse> InitiateGatewayPaymentAsync(InitiateGatewayPaymentRequest req)
    {
        var record = await db.StudentFeeRecords.FindAsync(req.StudentFeeRecordId)
            ?? throw new KeyNotFoundException("Student fee record not found.");

        var gatewayRef = $"LMS-{record.StudentId:N[..8]}-{Guid.NewGuid():N[..6]}".ToUpperInvariant();

        string checkoutUrl;
        string gatewayReference;

        if (req.Gateway.Equals("Paystack", StringComparison.OrdinalIgnoreCase))
        {
            var (url, reference) = await paystackService.InitializeTransactionAsync(
                req.CustomerEmail, req.Amount, gatewayRef, req.CallbackUrl,
                new { studentId = req.StudentFeeRecordId, system = "LMS" });
            checkoutUrl = url;
            gatewayReference = reference;
        }
        else if (req.Gateway.Equals("Hydrogen", StringComparison.OrdinalIgnoreCase))
        {
            var (url, txRef) = await hydrogenService.InitiatePaymentAsync(
                req.CustomerEmail, req.CustomerName, req.Amount,
                $"LMS Fee Payment", req.CallbackUrl,
                new { studentFeeRecordId = req.StudentFeeRecordId });
            checkoutUrl = url;
            gatewayReference = txRef;
        }
        else
        {
            throw new InvalidOperationException($"Unknown gateway: {req.Gateway}");
        }

        // Create a pending payment record
        var payment = new FeePayment
        {
            StudentFeeRecordId = req.StudentFeeRecordId,
            Amount = req.Amount,
            PaymentMethod = req.Gateway.Equals("Paystack", StringComparison.OrdinalIgnoreCase)
                ? PaymentMethod.Paystack : PaymentMethod.Hydrogen,
            GatewayReference = gatewayReference,
            GatewayCheckoutUrl = checkoutUrl,
            Status = PaymentStatus.Pending
        };
        db.FeePayments.Add(payment);
        await db.SaveChangesAsync();

        return new GatewayInitResponse(checkoutUrl, gatewayReference);
    }

    public async Task HandlePaystackWebhookAsync(string rawBody, string signature)
    {
        if (!paystackService.VerifySignature(rawBody, signature))
            throw new UnauthorizedAccessException("Invalid Paystack webhook signature.");

        using var doc = JsonDocument.Parse(rawBody);
        var evt = doc.RootElement.GetProperty("event").GetString();
        if (evt != "charge.success") return; // Only handle successful charges

        var reference = doc.RootElement
            .GetProperty("data")
            .GetProperty("reference").GetString() ?? "";

        await ConfirmPaymentByGatewayReferenceAsync(reference, "Gateway:Paystack");
    }

    public async Task HandleHydrogenWebhookAsync(string rawBody, string signature)
    {
        if (!hydrogenService.VerifySignature(rawBody, signature))
            throw new UnauthorizedAccessException("Invalid Hydrogen webhook signature.");

        using var doc = JsonDocument.Parse(rawBody);
        var evt = doc.RootElement.TryGetProperty("event", out var evtEl)
            ? evtEl.GetString() : null;
        if (evt != "payment.successful") return;

        var transactionRef = doc.RootElement
            .GetProperty("data")
            .GetProperty("transactionRef").GetString() ?? "";

        await ConfirmPaymentByGatewayReferenceAsync(transactionRef, "Gateway:Hydrogen");
    }

    private async Task ConfirmPaymentByGatewayReferenceAsync(string gatewayRef, string confirmedBy)
    {
        var payment = await db.FeePayments
            .Include(p => p.StudentFeeRecord)
            .FirstOrDefaultAsync(p => p.GatewayReference == gatewayRef && p.Status == PaymentStatus.Pending);

        if (payment == null) return; // Already handled or not found

        payment.Status = PaymentStatus.Confirmed;
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.ConfirmedBy = confirmedBy;

        await UpdateFeeRecordBalanceAsync(payment.StudentFeeRecord, payment.Amount);
        await db.SaveChangesAsync();
    }

    // ─── Payments — Manual ────────────────────────────────────────────────────

    public async Task<FeePayment> RecordManualPaymentAsync(RecordManualPaymentRequest req, string? receiptUrl)
    {
        var record = await db.StudentFeeRecords.FindAsync(req.StudentFeeRecordId)
            ?? throw new KeyNotFoundException("Student fee record not found.");

        var payment = new FeePayment
        {
            StudentFeeRecordId = req.StudentFeeRecordId,
            Amount = req.Amount,
            PaymentMethod = PaymentMethod.Manual,
            ReferenceNumber = req.ReferenceNumber,
            ReceiptUrl = receiptUrl,
            Status = PaymentStatus.Pending
        };
        db.FeePayments.Add(payment);
        await db.SaveChangesAsync();
        return payment;
    }

    public async Task<FeePayment> ConfirmPaymentAsync(Guid paymentId, string confirmedBy)
    {
        var payment = await db.FeePayments
            .Include(p => p.StudentFeeRecord)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can be confirmed.");

        payment.Status = PaymentStatus.Confirmed;
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.ConfirmedBy = confirmedBy;

        await UpdateFeeRecordBalanceAsync(payment.StudentFeeRecord, payment.Amount);
        await db.SaveChangesAsync();
        return payment;
    }

    public async Task<FeePayment> RejectPaymentAsync(Guid paymentId, string reason)
    {
        var payment = await db.FeePayments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can be rejected.");

        payment.Status = PaymentStatus.Rejected;
        payment.RejectionReason = reason;
        await db.SaveChangesAsync();
        return payment;
    }

    private async Task UpdateFeeRecordBalanceAsync(StudentFeeRecord record, decimal amount)
    {
        record.AmountPaid += amount;
        record.UpdatedAt = DateTime.UtcNow;

        record.Status = record.AmountPaid >= record.TotalAmount
            ? FeeRecordStatus.Paid
            : record.AmountPaid > 0
                ? FeeRecordStatus.PartiallyPaid
                : FeeRecordStatus.Outstanding;
    }

    // ─── History ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<FeePayment>> GetPaymentHistoryAsync(Guid studentId)
    {
        return await db.FeePayments
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Session)
            .Where(p => p.StudentFeeRecord.StudentId == studentId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<FeePayment>> GetAllPaymentsAsync(PaymentStatus? status = null, Guid? sessionId = null)
    {
        var q = db.FeePayments
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Student)
            .Include(p => p.StudentFeeRecord)
                .ThenInclude(r => r.Session)
            .AsQueryable();
        if (status.HasValue) q = q.Where(p => p.Status == status.Value);
        if (sessionId.HasValue) q = q.Where(p => p.StudentFeeRecord.SessionId == sessionId.Value);
        return await q.OrderByDescending(p => p.PaidAt).ToListAsync();
    }

    // ─── Late Fees ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies late fee surcharges to all students who have not fully paid by their respective due date.
    /// Only applies once per student per template (checks LateFeeApplications for existing entries).
    /// </summary>
    public async Task<IEnumerable<ApplyLateFeesResult>> ApplyLateFeesAsync(
        Guid sessionId, bool isDryRun, string appliedBy = "System")
    {
        var results = new List<ApplyLateFeesResult>();
        var now = DateTime.UtcNow;

        // Get all outstanding/partially paid records for this session
        var records = await db.StudentFeeRecords
            .Include(r => r.Student)
            .Include(r => r.LateFeeApplications)
            .Where(r => r.SessionId == sessionId &&
                        r.Status != FeeRecordStatus.Paid &&
                        r.Status != FeeRecordStatus.Waived)
            .ToListAsync();

        // Get all active templates with a late fee configured and due date in the past
        var templates = await db.FeeTemplates
            .Where(t => t.IsActive &&
                        t.HasLateFee &&
                        t.DueDate.HasValue &&
                        t.DueDate.Value < now &&
                        (t.SessionId == null || t.SessionId == sessionId))
            .ToListAsync();

        // Also check assignments with due date overrides
        var assignments = await db.FeeAssignments
            .Include(a => a.FeeTemplate)
            .Where(a => a.IsActive &&
                        a.SessionId == sessionId &&
                        a.DueDateOverride.HasValue &&
                        a.DueDateOverride.Value < now)
            .ToListAsync();

        foreach (var record in records)
        {
            foreach (var template in templates)
            {
                // Resolve effective due date: assignment override > template
                var effectiveDueDate = assignments
                    .Where(a => a.FeeTemplateId == template.Id &&
                                (a.StudentId == record.StudentId ||
                                 a.Scope == FeeScope.University))
                    .Select(a => a.DueDateOverride)
                    .FirstOrDefault() ?? template.DueDate!.Value;

                if (effectiveDueDate >= now) continue; // Not yet past due

                // Check if late fee was already applied for this template on this record
                bool alreadyApplied = record.LateFeeApplications
                    .Any(l => l.FeeTemplateId == template.Id);

                if (alreadyApplied)
                {
                    var studentName1 = record.Student != null ? $"{record.Student.FirstName} {record.Student.LastName}".Trim() : null;
                    results.Add(new ApplyLateFeesResult(
                        record.Id, string.IsNullOrWhiteSpace(studentName1) ? record.Student?.OfficialEmail ?? "" : studentName1,
                        template.Name, 0, effectiveDueDate, false, "Already applied"));
                    continue;
                }

                // Calculate surcharge
                decimal baseAmount = record.TotalAmount - record.LateFeeTotal;
                decimal surcharge = template.LateFeeType == LateFeeType.Percentage
                    ? Math.Round(baseAmount * (template.LateFeeAmount / 100m), 2)
                    : template.LateFeeAmount;

                if (!isDryRun)
                {
                    var lfa = new LateFeeApplication
                    {
                        StudentFeeRecordId = record.Id,
                        FeeTemplateId = template.Id,
                        AmountCharged = surcharge,
                        FeeType = template.LateFeeType,
                        BaseRateUsed = template.LateFeeAmount,
                        EffectiveDueDate = effectiveDueDate,
                        AppliedBy = appliedBy
                    };
                    db.LateFeeApplications.Add(lfa);

                    record.TotalAmount += surcharge;
                    record.LateFeeTotal += surcharge;
                    record.LateFeeApplied = true;
                    record.UpdatedAt = now;
                }

                var studentName2 = record.Student != null ? $"{record.Student.FirstName} {record.Student.LastName}".Trim() : null;
                results.Add(new ApplyLateFeesResult(
                    record.Id, string.IsNullOrWhiteSpace(studentName2) ? record.Student?.OfficialEmail ?? "" : studentName2,
                    template.Name, surcharge, effectiveDueDate, !isDryRun, null));
            }
        }

        if (!isDryRun) await db.SaveChangesAsync();
        return results;
    }
}
