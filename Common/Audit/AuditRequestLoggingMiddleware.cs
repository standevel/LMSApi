using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LMS.Api.Services;
using Microsoft.AspNetCore.Routing;

namespace LMS.Api.Common.Audit;

public sealed partial class AuditRequestLoggingMiddleware(RequestDelegate next)
{
    private const int MaxBodyCharacters = 16000;
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "confirmPassword",
        "currentPassword",
        "newPassword",
        "token",
        "accessToken",
        "refreshToken",
        "authorization",
        "secret",
        "clientSecret",
        "apiKey",
        "otp",
        "pin"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var shouldAudit = ShouldAudit(context.Request);
        if (shouldAudit && !context.Request.HasFormContentType)
        {
            context.Request.EnableBuffering();
        }

        Exception? exception = null;
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            if (shouldAudit)
            {
                await WriteAuditLogAsync(context, exception);
            }
        }
    }

    private static bool ShouldAudit(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method) &&
            !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsPatch(request.Method) &&
            !HttpMethods.IsDelete(request.Method))
        {
            return false;
        }

        var path = request.Path.ToString();
        return !path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteAuditLogAsync(HttpContext context, Exception? exception)
    {
        try
        {
            var auditService = context.RequestServices.GetService<IAuditService>();
            if (auditService is null)
            {
                return;
            }

            var request = context.Request;
            var path = request.Path.ToString();
            var statusCode = exception is null ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;
            var (entityName, entityId) = ResolveEntity(path, request.RouteValues);
            var bodyJson = await CaptureRequestBodyAsync(request, CancellationToken.None);
            var userId = ResolveUserId(context.User);

            await auditService.LogAsync(new AuditLogEntry
            {
                Action = $"{request.Method} {path}",
                EntityName = entityName,
                EntityId = entityId,
                Changes = BuildChangesSummary(request.Method, path, statusCode, bodyJson, exception),
                UserId = userId,
                HttpMethod = request.Method,
                Path = path,
                QueryString = request.QueryString.HasValue ? request.QueryString.Value : null,
                StatusCode = statusCode,
                IpAddress = ResolveIpAddress(context),
                UserAgent = request.Headers.UserAgent.ToString(),
                CorrelationId = context.TraceIdentifier,
                RequestContentType = request.ContentType,
                RequestBodyJson = bodyJson
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Audit] Failed to capture request audit details: {ex.Message}");
        }
    }

    private static async Task<string?> CaptureRequestBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.HasFormContentType)
        {
            return await CaptureFormBodyAsync(request, ct);
        }

        if (request.Body is null || !request.Body.CanSeek)
        {
            return null;
        }

        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return SanitizeJsonPayload(raw);
    }

    private static async Task<string?> CaptureFormBodyAsync(HttpRequest request, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in form)
        {
            payload[field.Key] = SensitiveKeys.Contains(field.Key)
                ? "[REDACTED]"
                : Truncate(field.Value.ToString());
        }

        if (form.Files.Count > 0)
        {
            payload["_files"] = form.Files.Select(file => new
            {
                file.Name,
                file.FileName,
                file.ContentType,
                file.Length
            }).ToList();
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string SanitizeJsonPayload(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var sanitized = SanitizeElement(document.RootElement);
            return Truncate(JsonSerializer.Serialize(sanitized));
        }
        catch (JsonException)
        {
            return Truncate(raw);
        }
    }

    private static object? SanitizeElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => SensitiveKeys.Contains(property.Name) ? "[REDACTED]" : SanitizeElement(property.Value),
                StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(SanitizeElement).ToList(),
            JsonValueKind.String => Truncate(element.GetString() ?? string.Empty),
            JsonValueKind.Number => element.TryGetInt64(out var number) ? number : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string BuildChangesSummary(string method, string path, int statusCode, string? bodyJson, Exception? exception)
    {
        var result = statusCode is >= 200 and < 400 ? "succeeded" : "failed";
        var bodyNote = string.IsNullOrWhiteSpace(bodyJson) ? "No request body captured." : "Sanitized request body captured.";
        var exceptionNote = exception is null ? string.Empty : $" Exception: {exception.GetType().Name}.";
        return $"{method} {path} {result} with HTTP {statusCode}.{exceptionNote} {bodyNote}";
    }

    private static (string EntityName, string EntityId) ResolveEntity(string path, RouteValueDictionary routeValues)
    {
        var routeId = routeValues
            .Where(pair => pair.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || pair.Key.Equals("id", StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value?.ToString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !segment.Equals("api", StringComparison.OrdinalIgnoreCase))
            .Where(segment => !GuidPattern().IsMatch(segment))
            .ToList();

        var entityName = segments.LastOrDefault() ?? "Request";
        if (IsActionSegment(entityName) && segments.Count > 1)
        {
            entityName = segments[^2];
        }

        return (entityName, routeId ?? "N/A");
    }

    private static bool IsActionSegment(string segment)
    {
        return segment.Equals("status", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("duplicate", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("upload", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("preview", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("import", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? ResolveIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxBodyCharacters ? value : value[..MaxBodyCharacters] + "...[TRUNCATED]";
    }

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidPattern();
}
