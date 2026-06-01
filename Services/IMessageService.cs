using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IMessageService
{
    Task<ErrorOr<MessageDto>> CreateAsync(CreateMessageRequest request, CancellationToken ct = default);
    Task<ErrorOr<List<MessageDto>>> GetByRecipientIdAsync(Guid recipientId, CancellationToken ct = default);
    Task<ErrorOr<MessageDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<MessageDto>> MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteAsync(Guid id, CancellationToken ct = default);
}