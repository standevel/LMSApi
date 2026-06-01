using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IRateLimitingService
{
    Task<ErrorOr<ApiRateLimitDto>> GetRateLimitStatusAsync(string clientId, string endpoint, string method, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> IncrementRequestCountAsync(string clientId, string endpoint, string method, CancellationToken ct = default);
    Task<bool> IsRateLimitExceededAsync(string clientId, string endpoint, string method, CancellationToken ct = default);
}