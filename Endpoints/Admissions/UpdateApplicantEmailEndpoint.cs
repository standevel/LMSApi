using FastEndpoints;
using FluentValidation;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed record UpdateApplicantEmailRequest(Guid Id, string Email);

public sealed class UpdateApplicantEmailValidator : Validator<UpdateApplicantEmailRequest>
{
    public UpdateApplicantEmailValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Application ID is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address must be provided.");
    }
}

public sealed class UpdateApplicantEmailEndpoint(IAdmissionService admissionService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<UpdateApplicantEmailRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Patch("admissions/applications/{Id}/email");
        Policies(LmsPolicies.AdmissionsManagement);
        Tags("Admissions");
        Description(d => d
            .WithName("Update Applicant Email")
            .WithSummary("Update the contact email address of an applicant and synchronize associated student/user records."));
    }

    public override async Task HandleAsync(UpdateApplicantEmailRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            var app = await admissionService.UpdateApplicantEmailAsync(req.Id, req.Email, userId, ct);
            var response = AdmissionResponseMapper.Map(app);
            await SendSuccessAsync(response, ct, "Applicant email address updated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Application Not Found", "not_found", ex.Message, ct);
        }
        catch (ArgumentException ex)
        {
            await SendFailureAsync(400, "Invalid Email", "validation_error", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Update Email Failed", "server_error", ex.Message, ct);
        }
    }
}
