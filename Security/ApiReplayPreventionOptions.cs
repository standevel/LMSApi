namespace LMS.Api.Security;

public sealed class ApiReplayPreventionOptions
{
    public bool Enabled { get; set; } = true;
    public int AllowedClockSkewSeconds { get; set; } = 300;
    public string RequestIdHeaderName { get; set; } = "X-Request-Id";
    public string TimestampHeaderName { get; set; } = "X-Request-Timestamp";
}
