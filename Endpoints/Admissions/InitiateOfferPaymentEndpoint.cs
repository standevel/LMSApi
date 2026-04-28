using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Admissions;

public sealed class InitiateOfferPaymentEndpoint(LmsDbContext dbContext, IFeeService feeService)
    : ApiEndpoint<InitiateOfferPaymentRequest, GatewayInitResponse>
{
    public override void Configure()
    {
        Post("/api/admissions/offers/{Id}/payments/initiate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InitiateOfferPaymentRequest req, CancellationToken ct)
    {
        var app = await dbContext.AdmissionApplications.FirstOrDefaultAsync(x => x.Id == Route<Guid>("Id"), ct);
        if (app is null)
        {
            await SendFailureAsync(404, "Application not found", "not_found", "No application found with the given ID.", ct);
            return;
        }

        if (app.Status != AdmissionStatus.OfferAccepted)
        {
            await SendFailureAsync(400, "Payment is not available", "invalid_offer_state", "This application is not awaiting acceptance-fee payment.", ct);
            return;
        }

        var student = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == app.StudentEmail || x.EntraObjectId == $"admission:{app.Id}", ct);

        if (student is null)
        {
            await SendFailureAsync(404, "Student record not found", "student_not_found", "No student record exists for this offer yet.", ct);
            return;
        }

        var feeRecord = await dbContext.StudentFeeRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StudentId == student.Id && x.SessionId == app.AcademicSessionId, ct);

        if (feeRecord is null || feeRecord.Balance <= 0)
        {
            await SendFailureAsync(400, "No acceptance fee is due", "no_fee_due", "There is no outstanding acceptance fee for this application.", ct);
            return;
        }

        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
        var result = await feeService.InitiateGatewayPaymentAsync(new InitiateGatewayPaymentRequest(
            feeRecord.Id,
            feeRecord.Balance,
            req.Gateway,
            req.CallbackUrl,
            app.StudentEmail,
            fullName));

        await SendSuccessAsync(result, ct);
    }
}
