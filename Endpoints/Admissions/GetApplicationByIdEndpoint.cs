using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admissions;

public sealed class GetApplicationByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetApplicationByIdEndpoint(IAdmissionService admissionService, LmsDbContext dbContext)
    : ApiEndpoint<GetApplicationByIdRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Get("/api/admissions/applications/{Id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetApplicationByIdRequest req, CancellationToken ct)
    {
        var app = await admissionService.GetApplicationByIdAsync(req.Id);

        if (app is null)
        {
            await SendFailureAsync(404, "Application not found", "not_found", "No application found with the given ID.", ct);
            return;
        }

        var (studentUserId, feeRecord) = await AdmissionResponseMapper.GetOfferFeeContextAsync(dbContext, app, ct);
        var response = AdmissionResponseMapper.Map(app, studentUserId, feeRecord);

        await SendSuccessAsync(response, ct);
    }
}
