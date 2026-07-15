using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ISessionRolloverService
{
    Task<ErrorOr<SessionRolloverResultDto>> RolloverSessionAsync(SessionRolloverRequest request, Guid userId, CancellationToken ct = default);
}
