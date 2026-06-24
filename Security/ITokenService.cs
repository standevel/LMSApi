namespace LMS.Api.Security;

public interface ITokenService
{
    Task<string> CreateAccessTokenAsync(Guid userId, CancellationToken ct = default);
    Task<string> CreateSwitchedUserTokenAsync(Guid actorUserId, Guid targetUserId, CancellationToken ct = default);
}
