using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.SelfService;

public sealed class RequestScheduleAdjustmentEndpoint(IScheduleService scheduleService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateScheduleAdjustmentRequest, ScheduleAdjustmentRequestDto>
{
    public override void Configure()
    {
        Post("self-service/schedule/adjust");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CreateScheduleAdjustmentRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await scheduleService.RequestScheduleAdjustmentAsync(
            userId.Value, 
            req.Reason, 
            req.DesiredSlotDetails, 
            ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentScheduleEndpoint(IScheduleService scheduleService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<LMS.Api.Contracts.ScheduleDto>>
{
    public override void Configure()
    {
        Get("self-service/schedule");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // For simplicity, we're using the current academic session
        // In a real implementation, you might want to determine this dynamically
        Guid currentAcademicSessionId = Guid.Empty; // This would need to be determined properly
        
        var result = await scheduleService.GetStudentScheduleAsync(userId.Value, currentAcademicSessionId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetRegistrationHistoryEndpoint(IRegistrationService registrationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<CourseRegistrationDto>>
{
    public override void Configure()
    {
        Get("self-service/registration-history");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Note: The RegistrationService doesn't currently have a GetRegistrationHistory method
        // This would need to be implemented in the service
        await SendFailureAsync(501, "Not Implemented", "NOT_IMPLEMENTED", "Get registration history functionality not yet implemented", ct);
    }
}

public sealed class RequestPrerequisiteOverrideEndpoint(IPrerequisiteValidationService prerequisiteValidationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreatePrerequisiteOverrideRequest, PrerequisiteOverrideDto>
{
    public override void Configure()
    {
        Post("self-service/prerequisite-override");
        Roles("Student");
        Tags("SelfService");
    }

    public override async Task HandleAsync(CreatePrerequisiteOverrideRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        var result = await prerequisiteValidationService.CreateOverrideRequestAsync(
            userId.Value, 
            req.CourseOfferingId, 
            req.Reason, 
            ct);
        await SendAsync(result, ct);
    }
}