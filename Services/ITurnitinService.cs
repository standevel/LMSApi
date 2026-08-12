using LMS.Api.Contracts;
using ErrorOr;

namespace LMS.Api.Services;

public interface ITurnitinService
{
    Task<ErrorOr<TurnitinCheckResultDto>> CheckSubmissionAsync(Guid submissionId, CancellationToken ct = default);
    Task<ErrorOr<TurnitinCheckResultDto>> GetSubmissionReportAsync(Guid submissionId, CancellationToken ct = default);
}
