namespace LMS.Api.Security;

public interface ITokenService
{
    Task<string> CreateAccessTokenAsync(Guid userId, CancellationToken ct = default);
}
