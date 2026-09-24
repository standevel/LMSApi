using FastEndpoints;
using FluentValidation;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LMS.Api.Endpoints.Admissions;

public sealed class ChangeApplicationProgramValidator : Validator<ChangeApplicationProgramRequest>
{
    public ChangeApplicationProgramValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Application ID is required.");

        RuleFor(x => x.TargetProgramId)
            .NotEmpty().WithMessage("Target program ID is required.");
    }
}

public sealed class ChangeApplicationProgramEndpoint(IAdmissionService admissionService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<ChangeApplicationProgramRequest, AdmissionApplicationResponse>
{
    public override void Configure()
    {
        Post("admissions/applications/{Id}/change-program");
        Policies(PermissionPolicy.Build(LmsPermissions.AdmissionsManage));
        Tags("Admissions");
        Description(d => d
            .WithName("Change Application Program")
            .WithSummary("Change the chosen or offered academic program for an admission application across any status (draft, submitted, under review, admitted, offer accepted) and optionally regenerate and email the offer letter."));
    }

    public override async Task HandleAsync(ChangeApplicationProgramRequest req, CancellationToken ct)
    {
        try
        {
            var userId = await currentUserContext.GetUserIdAsync(ct);
            var app = await admissionService.ChangeApplicationProgramAsync(
                req.Id,
                req.TargetProgramId,
                req.Reason,
                req.RegenerateAndSendOffer,
                userId,
                ct);

            var response = AdmissionResponseMapper.Map(app);
            await SendSuccessAsync(response, ct, "Application academic program updated successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            await SendFailureAsync(404, "Not Found", "not_found", ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            await SendFailureAsync(400, "Invalid Program State", "invalid_state", ex.Message, ct);
        }
        catch (Exception ex)
        {
            await SendFailureAsync(500, "Change Program Failed", "server_error", ex.Message, ct);
        }
    }
}
