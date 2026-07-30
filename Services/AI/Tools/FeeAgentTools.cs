using System.ComponentModel;
using LMS.Api.Services;

namespace LMS.Api.Services.AI.Tools;

public class FeeAgentTools
{
    private readonly IFeeService _feeService;
    private readonly ILogger<FeeAgentTools> _logger;

    public FeeAgentTools(IFeeService feeService, ILogger<FeeAgentTools> logger)
    {
        _feeService = feeService;
        _logger = logger;
    }

    [Description("Checks student fee record and balance status for a given academic session.")]
    public async Task<string> CheckFeeStatusAsync(Guid studentId, Guid sessionId)
    {
        _logger.LogInformation("FeeAgentTool calling CheckFeeStatusAsync for Student {StudentId}, Session {SessionId}", studentId, sessionId);

        var bill = await _feeService.GetStudentBillAsync(studentId, sessionId);
        if (bill == null)
        {
            return "No bill has been generated yet for this student in the specified session.";
        }

        return $"Bill Status: {bill.Status}. Total Fee: {bill.TotalAmount:C}, Paid Amount: {bill.AmountPaid:C}, Balance Due: {bill.Balance:C}.";
    }

    [Description("Retrieves recent payment history for a student.")]
    public async Task<string> GetPaymentHistorySummaryAsync(Guid studentId)
    {
        var payments = await _feeService.GetPaymentHistoryAsync(studentId);
        var paymentList = payments.ToList();

        if (paymentList.Count == 0)
        {
            return "No fee payments recorded for this student.";
        }

        var summary = string.Join("\n", paymentList.Take(5).Select(p => 
            $"- {p.PaidAt:yyyy-MM-dd}: {p.Amount:C} via {p.PaymentMethod} (Status: {p.Status}, Ref: {p.ReferenceNumber ?? p.GatewayReference ?? "N/A"})"));

        return $"Recent Payments ({paymentList.Count} total):\n" + summary;
    }
}
