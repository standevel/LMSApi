using FastEndpoints;
using LMS.Api.Contracts;

namespace LMS.Api.Endpoints.Health;

public sealed class HealthEndpoint : EndpointWithoutRequest<ApiResponse<HealthResponse>>
{
    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var data = new HealthResponse("LMS API is running", DateTime.UtcNow);
        await Send.OkAsync(ApiResponse<HealthResponse>.Ok(data), ct);
    }
}

public sealed record HealthResponse(string Message, DateTime TimestampUtc);
