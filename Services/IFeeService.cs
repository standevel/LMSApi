using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;

namespace LMS.Api.Services;

public interface IFeeService
{
    // Templates
    Task<FeeTemplate> CreateTemplateAsync(CreateFeeTemplateRequest req);
    Task<FeeTemplate> UpdateTemplateAsync(Guid id, UpdateFeeTemplateRequest req);
    Task<FeeTemplate> ToggleTemplateAsync(Guid id);
    Task<IEnumerable<FeeTemplate>> GetTemplatesAsync(bool? activeOnly = null);
    Task<FeeTemplate?> GetTemplateByIdAsync(Guid id);

    // Categories
    Task<FeeCategory> CreateCategoryAsync(CreateFeeCategoryRequest req);
    Task<FeeCategory> UpdateCategoryAsync(Guid id, UpdateFeeCategoryRequest req);
    Task<FeeCategory> ToggleCategoryAsync(Guid id);
    Task<IEnumerable<FeeCategory>> GetCategoriesAsync(bool? activeOnly = null);

    // Line Items
    Task<FeeLineItem> AddLineItemAsync(Guid templateId, AddFeeLineItemRequest req);
    Task<FeeLineItem> UpdateLineItemAsync(Guid itemId, UpdateFeeLineItemRequest req);
    Task DeleteLineItemAsync(Guid itemId);

    // Assignments
    Task<FeeAssignment> AssignFeeAsync(AssignFeeRequest req);
    Task<IEnumerable<FeeAssignment>> GetAssignmentsAsync(Guid? templateId = null, Guid? sessionId = null);
    Task DeleteAssignmentAsync(Guid id);

    // Student Bills
    Task<StudentFeeRecord> GenerateStudentBillAsync(Guid studentId, Guid sessionId);
    Task<StudentFeeRecord?> GetStudentBillAsync(Guid studentId, Guid sessionId);
    Task<IEnumerable<StudentFeeRecord>> GetAllBillsAsync(Guid? sessionId = null, FeeRecordStatus? status = null);

    // Payments — Gateway
    Task<GatewayInitResponse> InitiateGatewayPaymentAsync(InitiateGatewayPaymentRequest req);
    Task HandlePaystackWebhookAsync(string rawBody, string signature);
    Task HandleHydrogenWebhookAsync(string rawBody, string signature);

    // Payments — Manual
    Task<FeePayment> RecordManualPaymentAsync(RecordManualPaymentRequest req, string? receiptUrl);
    Task<FeePayment> ConfirmPaymentAsync(Guid paymentId, string confirmedBy);
    Task<FeePayment> RejectPaymentAsync(Guid paymentId, string reason);

    // History
    Task<IEnumerable<FeePayment>> GetPaymentHistoryAsync(Guid studentId);
    Task<IEnumerable<FeePayment>> GetAllPaymentsAsync(PaymentStatus? status = null, Guid? sessionId = null);

    // Late Fees
    Task<IEnumerable<ApplyLateFeesResult>> ApplyLateFeesAsync(Guid sessionId, bool isDryRun, string appliedBy = "System");
}
