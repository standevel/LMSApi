namespace LMS.Api.Security;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "LMS.Api";
    public string Audience { get; set; } = "LMS.Client";
    public string SigningKey { get; set; } = "change-this-development-only-signing-key-at-least-32-characters";
    public int ExpiryMinutes { get; set; } = 60;
}
