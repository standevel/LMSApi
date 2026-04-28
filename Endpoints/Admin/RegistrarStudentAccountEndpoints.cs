using FastEndpoints;
using LMS.Api.Services;

namespace LMS.Api.Endpoints.Admin;

/// <summary>
/// Response DTO for pending student accounts
/// </summary>
public sealed record PendingStudentAccountResponse(
    Guid ApplicationId,
    string ApplicationNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? Phone,
    string ProgramName,
    string SessionName,
    DateTime? OfferAcceptedAt);

/// <summary>
/// Response for single student account creation
/// </summary>
public sealed record CreateStudentAccountResponse(
    bool Success,
    string Message,
    Guid? StudentId,
    string? OfficialEmail,
    string? TemporaryPassword,
    bool IsExistingAccount,
    decimal AmountDue);

/// <summary>
/// Request for bulk account creation
/// </summary>
public sealed record BulkCreateStudentAccountsRequest(Guid[] ApplicationIds);

/// <summary>
/// Response for bulk account creation
/// </summary>
public sealed record BulkCreateStudentAccountsResponse(
    int TotalRequested,
    int Successful,
    int Failed,
    List<CreateStudentAccountResponse> Results);

/// <summary>
/// Endpoint to list students who accepted offers but don't have accounts yet.
/// For Registrar dashboard.
/// </summary>
public sealed class ListPendingStudentAccountsEndpoint(IAdmissionService admissionService)
    : ApiEndpointWithoutRequest<List<PendingStudentAccountResponse>>
{
    public override void Configure()
    {
        Get("/api/admin/students/pending-accounts");
        Roles("SuperAdmin", "Admin", "Registrar");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pending = await admissionService.GetPendingStudentAccountsAsync(ct);

        var response = pending.Select(p => new PendingStudentAccountResponse(
            p.ApplicationId,
            p.ApplicationNumber,
            p.FirstName,
            p.LastName,
            p.MiddleName,
            p.Email,
            p.Phone,
            p.ProgramName,
            p.SessionName,
            p.OfferAcceptedAt
        )).ToList();

        await SendSuccessAsync(response, ct);
    }
}

/// <summary>
/// Endpoint to create a student account for a single accepted application.
/// Triggered by Registrar.
/// </summary>
public sealed class CreateStudentAccountEndpoint(IAdmissionService admissionService)
    : ApiEndpointWithoutRequest<CreateStudentAccountResponse>
{
    public override void Configure()
    {
        Post("/api/admin/applications/{ApplicationId}/create-account");
        Roles("SuperAdmin", "Admin", "Registrar");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var applicationId = Route<Guid>("ApplicationId");

        var result = await admissionService.CreateStudentAccountAsync(applicationId, ct);

        if (!result.Success)
        {
            await SendFailureAsync(400, result.ErrorMessage ?? "Failed to create account", 
                "ACCOUNT_CREATION_FAILED", result.ErrorMessage ?? "Unknown error", ct);
            return;
        }

        var response = new CreateStudentAccountResponse(
            result.Success,
            "Student account created successfully",
            result.StudentId,
            result.OfficialEmail,
            result.TemporaryPassword,
            result.IsExistingAccount,
            result.AmountDue
        );

        await SendSuccessAsync(response, ct);
    }
}

/// <summary>
/// Endpoint to bulk create student accounts for multiple accepted applications.
/// Triggered by Registrar.
/// </summary>
public sealed class BulkCreateStudentAccountsEndpoint(IAdmissionService admissionService)
    : ApiEndpoint<BulkCreateStudentAccountsRequest, BulkCreateStudentAccountsResponse>
{
    public override void Configure()
    {
        Post("/api/admin/students/bulk-create-accounts");
        Roles("SuperAdmin", "Admin", "Registrar");
    }

    public override async Task HandleAsync(BulkCreateStudentAccountsRequest req, CancellationToken ct)
    {
        if (req.ApplicationIds == null || req.ApplicationIds.Length == 0)
        {
            await SendFailureAsync(400, "No application IDs provided", 
                "INVALID_REQUEST", "Please provide at least one application ID", ct);
            return;
        }

        if (req.ApplicationIds.Length > 50)
        {
            await SendFailureAsync(400, "Too many applications", 
                "INVALID_REQUEST", "Maximum 50 applications allowed per bulk operation", ct);
            return;
        }

        var results = new List<CreateStudentAccountResponse>();
        int successful = 0;
        int failed = 0;

        foreach (var applicationId in req.ApplicationIds)
        {
            try
            {
                var result = await admissionService.CreateStudentAccountAsync(applicationId, ct);

                var response = new CreateStudentAccountResponse(
                    result.Success,
                    result.Success ? "Account created successfully" : (result.ErrorMessage ?? "Failed"),
                    result.StudentId,
                    result.OfficialEmail,
                    result.TemporaryPassword,
                    result.IsExistingAccount,
                    result.AmountDue
                );

                results.Add(response);

                if (result.Success)
                    successful++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                results.Add(new CreateStudentAccountResponse(
                    false,
                    $"Exception: {ex.Message}",
                    null,
                    null,
                    null,
                    false,
                    0));
                failed++;
            }
        }

        var bulkResponse = new BulkCreateStudentAccountsResponse(
            req.ApplicationIds.Length,
            successful,
            failed,
            results
        );

        await SendSuccessAsync(bulkResponse, ct);
    }
}
