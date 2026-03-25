using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;

namespace LMS.Api.Endpoints;

public abstract class ApiEndpoint<TRequest, TResponse> : Endpoint<TRequest, ApiResponse<TResponse>>
    where TRequest : notnull
{
    protected Task SendSuccessAsync(TResponse data, CancellationToken ct, string message = "Request successful") =>
        Send.OkAsync(ApiResponse<TResponse>.Ok(data, message), ct);

    protected Task SendFailureAsync(
        int statusCode,
        string message,
        string errorCode,
        string errorMessage,
        CancellationToken ct) =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Fail(message, statusCode, new ApiError(errorCode, errorMessage)),
            statusCode: statusCode));

    protected Task HandleErrorAsync(IReadOnlyList<Error> errors, CancellationToken ct)
    {
        var firstError = errors[0];
        var statusCode = firstError.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            _ => 500
        };

        return SendFailureAsync(statusCode, firstError.Description, firstError.Code, firstError.Description, ct);
    }
}

public abstract class ApiEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<ApiResponse<TResponse>>
{
    protected Task SendSuccessAsync(TResponse data, CancellationToken ct, string message = "Request successful") =>
        Send.OkAsync(ApiResponse<TResponse>.Ok(data, message), ct);

    protected Task SendFailureAsync(
        int statusCode,
        string message,
        string errorCode,
        string errorMessage,
        CancellationToken ct) =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Fail(message, statusCode, new ApiError(errorCode, errorMessage)),
            statusCode: statusCode));

    protected Task HandleErrorAsync(IReadOnlyList<Error> errors, CancellationToken ct)
    {
        var firstError = errors[0];
        var statusCode = firstError.Type switch
        {
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            ErrorType.Validation => 400,
            ErrorType.Unauthorized => 401,
            ErrorType.Forbidden => 403,
            _ => 500
        };

        return SendFailureAsync(statusCode, firstError.Description, firstError.Code, firstError.Description, ct);
    }
}
