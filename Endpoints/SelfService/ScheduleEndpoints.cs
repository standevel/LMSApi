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

        // Read academicSessionId injected by the frontend interceptor
        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        if (!Guid.TryParse(sessionIdStr, out var currentAcademicSessionId) || currentAcademicSessionId == Guid.Empty)
        {
            await SendFailureAsync(400, "Bad Request", "MISSING_SESSION", "A valid academicSessionId query parameter is required.", ct);
            return;
        }

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

        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        Guid.TryParse(sessionIdStr, out var sessionId);
        var result = await registrationService.GetRegistrationHistoryAsync(userId.Value, sessionId == Guid.Empty ? null : sessionId, ct);
        await SendAsync(result, ct);
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

public sealed class CheckPrerequisitesEndpoint(IPrerequisiteValidationService prerequisiteValidationService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<bool>
{
    public override void Configure()
    {
        Get("self-service/prerequisite-override/check");
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

        var courseOfferingId = QueryParam<Guid>("courseOfferingId");
        if (!courseOfferingId.HasValue)
        {
            await SendFailureAsync(400, "Bad Request", "BAD_REQUEST", "Course offering ID is required", ct);
            return;
        }

        var result = await prerequisiteValidationService.CheckPrerequisitesAsync(userId.Value, courseOfferingId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetStudentExamsEndpoint(IScheduleService scheduleService, ICurrentUserContext currentUserContext)
    : ApiEndpointWithoutRequest<List<StudentExamDto>>
{
    public override void Configure()
    {
        Get("self-service/schedule/exams");
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

        Guid currentAcademicSessionId = Guid.Empty;
        var sessionIdStr = HttpContext.Request.Query["academicSessionId"].FirstOrDefault();
        if (!Guid.TryParse(sessionIdStr, out currentAcademicSessionId) || currentAcademicSessionId == Guid.Empty)
        {
            await SendFailureAsync(400, "Bad Request", "MISSING_SESSION", "A valid academicSessionId query parameter is required.", ct);
            return;
        }

        var result = await scheduleService.GetStudentExamsAsync(userId.Value, currentAcademicSessionId, ct);
        if (result.IsError)
        {
            await SendFailureAsync(400, result.FirstError.Description, result.FirstError.Code, result.FirstError.Description, ct);
            return;
        }

        await SendSuccessAsync(result.Value, ct);
    }
}