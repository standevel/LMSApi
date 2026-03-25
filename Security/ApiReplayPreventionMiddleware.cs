using LMS.Api.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;

namespace LMS.Api.Security;

public sealed class ApiReplayPreventionMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    IOptions<ApiReplayPreventionOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;
        if (!settings.Enabled || IsSafeMethod(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var requestId = context.Request.Headers[settings.RequestIdHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            await WriteFailureAsync(
                context,
                StatusCodes.Status400BadRequest,
                "missing_request_id",
                $"{settings.RequestIdHeaderName} header is required for state-changing requests.");
            return;
        }

        var timestampRaw = context.Request.Headers[settings.TimestampHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(timestampRaw))
        {
            await WriteFailureAsync(
                context,
                StatusCodes.Status400BadRequest,
                "missing_request_timestamp",
                $"{settings.TimestampHeaderName} header is required for state-changing requests.");
            return;
        }

        if (!TryParseTimestampUtc(timestampRaw, out var requestUtc))
        {
            await WriteFailureAsync(
                context,
                StatusCodes.Status400BadRequest,
                "invalid_request_timestamp",
                $"{settings.TimestampHeaderName} must be ISO-8601 UTC or unix epoch seconds.");
            return;
        }

        var skewSeconds = Math.Abs((DateTime.UtcNow - requestUtc).TotalSeconds);
        if (skewSeconds > settings.AllowedClockSkewSeconds)
        {
            await WriteFailureAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "request_expired",
                "Request timestamp is outside the allowed window.");
            return;
        }

        var userKey = context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue("oid")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "anonymous";

        var replayKey = $"replay:{userKey}:{context.Request.Method}:{context.Request.Path}:{requestId}";
        if (cache.TryGetValue(replayKey, out _))
        {
            await WriteFailureAsync(
                context,
                StatusCodes.Status409Conflict,
                "replay_detected",
                "Replay request detected.");
            return;
        }

        cache.Set(
            replayKey,
            true,
            TimeSpan.FromSeconds(settings.AllowedClockSkewSeconds));

        await next(context);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static bool TryParseTimestampUtc(string raw, out DateTime utcTimestamp)
    {
        if (long.TryParse(raw, out var unixSeconds))
        {
            utcTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            return true;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            utcTimestamp = dto.UtcDateTime;
            return true;
        }

        utcTimestamp = default;
        return false;
    }

    private static async Task WriteFailureAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(message, status, new ApiError(code, message));
        await context.Response.WriteAsJsonAsync(response);
    }
}
