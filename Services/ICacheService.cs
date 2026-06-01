using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;

namespace LMS.Api.Services;

public interface ICacheService
{
    Task<ErrorOr<T>> GetAsync<T>(string key, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> RemoveAsync(string key, CancellationToken ct = default);
    Task<ErrorOr<bool>> ExistsAsync(string key, CancellationToken ct = default);
}