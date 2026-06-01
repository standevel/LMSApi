using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAnnouncementService
{
    Task<ErrorOr<AnnouncementDto>> CreateAsync(CreateAnnouncementRequest request, CancellationToken ct = default);
    Task<ErrorOr<AnnouncementDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<List<AnnouncementDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ErrorOr<AnnouncementDto>> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
}