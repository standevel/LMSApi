using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin.RegistrationConfig;

public sealed class CleanupPassedCourseRegistrationsRequest
{
    public Guid? AcademicSessionId { get; set; }
}

public sealed class AdminCleanupPassedCourseRegistrationsEndpoint(IRegistrationService registrationService)
    : ApiEndpoint<CleanupPassedCourseRegistrationsRequest, RegistrationCleanupResultDto>
{
    public override void Configure()
    {
        Post("admin/registration/cleanup-passed-courses");
        Roles("Admin", "SuperAdmin");
        Tags("Administration");
    }

    public override async Task HandleAsync(CleanupPassedCourseRegistrationsRequest req, CancellationToken ct)
    {
        var result = await registrationService.CleanupInvalidRegistrationsAsync(req.AcademicSessionId, ct);
        if (result.IsError)
        {
            var err = result.FirstError;
            await SendFailureAsync(400, err.Description, err.Code, err.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}
