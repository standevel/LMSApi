using ErrorOr;
using FastEndpoints;
using LMS.Api.Contracts;
using Microsoft.Extensions.Primitives;

namespace LMS.Api.Endpoints;

public abstract class ApiEndpoint<TRequest, TResponse> : Endpoint<TRequest, ApiResponse<TResponse>>
    where TRequest : notnull
{
    protected Task SendSuccessAsync(TResponse data, CancellationToken ct, string message = "Request successful") =>
        Send.OkAsync(ApiResponse<TResponse>.Ok(data, message), ct);

    protected Task SendCreatedAsync(TResponse data, CancellationToken ct, string message = "Created successfully") =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Ok(data, message),
            statusCode: 201));

    protected Task SendAsync(ErrorOr<TResponse> result, CancellationToken ct)
    {
        if (result.IsError)
        {
            return HandleErrorAsync(result.Errors, ct);
        }

        return SendSuccessAsync(result.Value, ct);
    }

    protected T? QueryParam<T>(string key) where T : struct
    {
        if (!HttpContext.Request.Query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return (T?)ConvertQueryValue(typeof(T), raw);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertQueryValue(Type targetType, string raw)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return underlyingType == typeof(string) ? raw :
            underlyingType == typeof(int) ? int.Parse(raw) :
            underlyingType == typeof(long) ? long.Parse(raw) :
            underlyingType == typeof(bool) ? bool.Parse(raw) :
            underlyingType == typeof(Guid) ? Guid.Parse(raw) :
            underlyingType == typeof(DateTime) ? DateTime.Parse(raw) :
            underlyingType == typeof(DateOnly) ? DateOnly.Parse(raw) :
            underlyingType == typeof(TimeOnly) ? TimeOnly.Parse(raw) :
            throw new InvalidOperationException($"Unsupported query param type: {targetType}");
    }

    protected Task SendFailureAsync(
        int statusCode,
        string message,
        string errorCode,
        string errorMessage,
        CancellationToken ct) =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Fail(message, statusCode, new ApiError(errorCode, errorMessage)),
            statusCode: statusCode));

    protected Task SendUnauthorizedAsync(CancellationToken ct) =>
        SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);

    protected Task SendForbiddenAsync(CancellationToken ct) =>
        SendFailureAsync(403, "Forbidden", "FORBIDDEN", "User does not have permission", ct);

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

    protected Task SendCreatedAsync(TResponse data, CancellationToken ct, string message = "Created successfully") =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Ok(data, message),
            statusCode: 201));

    protected Task SendAsync(ErrorOr<TResponse> result, CancellationToken ct)
    {
        if (result.IsError)
        {
            return HandleErrorAsync(result.Errors, ct);
        }

        return SendSuccessAsync(result.Value, ct);
    }

    protected T? QueryParam<T>(string key) where T : struct
    {
        if (!HttpContext.Request.Query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return (T?)ConvertQueryValue(typeof(T), raw);
        }
        catch
        {
            return null;
        }
    }

    private static object? ConvertQueryValue(Type targetType, string raw)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return underlyingType == typeof(string) ? raw :
            underlyingType == typeof(int) ? int.Parse(raw) :
            underlyingType == typeof(long) ? long.Parse(raw) :
            underlyingType == typeof(bool) ? bool.Parse(raw) :
            underlyingType == typeof(Guid) ? Guid.Parse(raw) :
            underlyingType == typeof(DateTime) ? DateTime.Parse(raw) :
            underlyingType == typeof(DateOnly) ? DateOnly.Parse(raw) :
            underlyingType == typeof(TimeOnly) ? TimeOnly.Parse(raw) :
            throw new InvalidOperationException($"Unsupported query param type: {targetType}");
    }

    protected Task SendFailureAsync(
        int statusCode,
        string message,
        string errorCode,
        string errorMessage,
        CancellationToken ct) =>
        Send.ResultAsync(TypedResults.Json(
            ApiResponse<TResponse>.Fail(message, statusCode, new ApiError(errorCode, errorMessage)),
            statusCode: statusCode));

    protected Task SendUnauthorizedAsync(CancellationToken ct) =>
        SendFailureAsync(401, "Unauthorized", "UNAUTHORIZED", "User is not authenticated", ct);

    protected Task SendForbiddenAsync(CancellationToken ct) =>
        SendFailureAsync(403, "Forbidden", "FORBIDDEN", "User does not have permission", ct);

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
