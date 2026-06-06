using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LMS.Api.Endpoints.BulkOperations;

public sealed class BulkImportUsersEndpoint(IBulkOperationService bulkOperationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateBulkOperationRequest, BulkOperationDto>
{
    public override void Configure()
    {
        Post("bulk-operations/users");
        Roles("SuperAdmin", "Admin");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(CreateBulkOperationRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Modify the request to specify it's a user import
        var userImportRequest = req with { OperationType = "UserImport" };
        var result = await bulkOperationService.CreateOperationAsync(userImportRequest, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class BulkImportEnrollmentsEndpoint(IBulkOperationService bulkOperationService, ICurrentUserContext currentUserContext)
    : ApiEndpoint<CreateBulkOperationRequest, BulkOperationDto>
{
    public override void Configure()
    {
        Post("bulk-operations/enrollments");
        Roles("SuperAdmin", "Admin");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(CreateBulkOperationRequest req, CancellationToken ct)
    {
        var userId = await currentUserContext.GetUserIdAsync(ct);
        if (!userId.HasValue)
        {
            await SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User not authenticated", ct);
            return;
        }

        // Modify the request to specify it's an enrollment import
        var enrollmentImportRequest = req with { OperationType = "EnrollmentImport" };
        var result = await bulkOperationService.CreateOperationAsync(enrollmentImportRequest, userId.Value, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetBulkOperationsEndpoint(IBulkOperationService bulkOperationService)
    : ApiEndpointWithoutRequest<List<BulkOperationDto>>
{
    public override void Configure()
    {
        Get("bulk-operations");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await bulkOperationService.GetOperationsAsync(null, ct);
        await SendAsync(result, ct);
    }
}

public sealed class GetBulkOperationByIdEndpoint(IBulkOperationService bulkOperationService)
    : ApiEndpoint<GetBulkOperationByIdEndpoint.Request, BulkOperationDto>
{
    public sealed class Request
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid OperationId { get; set; }
    }

    public override void Configure()
    {
        Get("bulk-operations/{OperationId}");
        Roles("SuperAdmin", "Admin", "Registrar");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var result = await bulkOperationService.GetByIdAsync(req.OperationId, ct);
        await SendAsync(result, ct);
    }
}

public sealed class ProcessBulkOperationEndpoint(IBulkOperationService bulkOperationService)
    : ApiEndpoint<ProcessBulkOperationEndpoint.ProcessBulkOperationRequest, bool>
{
    public class ProcessBulkOperationRequest
    {
        [Microsoft.AspNetCore.Mvc.FromRoute] public Guid OperationId { get; set; }
    }

    public override void Configure()
    {
        Post("bulk-operations/{OperationId}/process");
        Roles("SuperAdmin", "Admin");
        Tags("BulkOperations");
    }

    public override async Task HandleAsync(ProcessBulkOperationRequest req, CancellationToken ct)
    {
        var result = await bulkOperationService.ProcessBulkOperationAsync(req.OperationId, ct);
        await SendAsync(result.Match(success => true, errors => false), ct);
    }
}
