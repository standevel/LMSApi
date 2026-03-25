using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;

namespace LMS.Api.Common.Extensions;

public static class ErrorOrExtensions
{
    public static async Task ToResponseAsync<T>(this ErrorOr<T> result, IEndpoint endpoint, CancellationToken ct)
    {
        if (!result.IsError)
        {
            var response = ApiResponse<T>.Ok(result.Value);
            await endpoint.HttpContext.Response.SendAsync(response, 200, cancellation: ct);
            return;
        }

        var firstError = result.FirstError;
        var statusCode = firstError.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            _ => 500
        };

        var errorResponse = ApiResponse<T>.Fail(
            firstError.Description,
            statusCode,
            new ApiError(firstError.Code, firstError.Description));

        await endpoint.HttpContext.Response.SendAsync(errorResponse, statusCode, cancellation: ct);
    }

    public static async Task ToResponseAsync(this ErrorOr<Deleted> result, IEndpoint endpoint, CancellationToken ct)
    {
        if (!result.IsError)
        {
            var response = ApiResponse<bool>.Ok(true, "Resource deleted successfully");
            await endpoint.HttpContext.Response.SendAsync(response, 200, cancellation: ct);
            return;
        }

        var firstError = result.FirstError;
        var statusCode = firstError.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            _ => 500
        };

        var errorResponse = ApiResponse<bool>.Fail(
            firstError.Description,
            statusCode,
            new ApiError(firstError.Code, firstError.Description));

        await endpoint.HttpContext.Response.SendAsync(errorResponse, statusCode, cancellation: ct);
    }
}
