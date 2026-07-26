using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Services;
using Microsoft.Extensions.Primitives;

namespace LMS.Api.Endpoints.Admin.Students;

public sealed class StudentListEndpoint : ApiEndpointWithoutRequest<StudentListResponse>
{
    private readonly IStudentService _studentService;

    public StudentListEndpoint(IStudentService studentService)
    {
        _studentService = studentService;
    }

    public override void Configure()
    {
        Get("admin/students");
        Tags("Admin - Students");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = HttpContext.Request.Query;

        string? search = GetString(query, "search");
        string? programId = GetString(query, "programId");
        string? departmentId = GetString(query, "departmentId");
        string? facultyId = GetString(query, "facultyId");
        string? levelId = GetString(query, "levelId");
        // Accept both 'sessionId' and 'academicSessionId' (sent by the frontend interceptor)
        string? sessionId = GetString(query, "sessionId") ?? GetString(query, "academicSessionId");
        string? status = GetString(query, "status");
        string? sortBy = GetString(query, "sortBy");
        string? sortDir = GetString(query, "sortDir");
        var page = GetInt(query, "page") ?? 1;
        var pageSize = GetInt(query, "pageSize") ?? 25;

        var (students, totalCount) = await _studentService.GetStudentsAsync(
            search, programId, departmentId, facultyId, levelId, sessionId, status, sortBy, sortDir, page, pageSize, ct);

        await SendSuccessAsync(new StudentListResponse
        {
            Students = students,
            TotalCount = totalCount
        }, ct);
    }

    private static string? GetString(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
            return null;
        var raw = values.ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static int? GetInt(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
            return null;
        var raw = values.ToString();
        return int.TryParse(raw, out var val) ? val : null;
    }
}
