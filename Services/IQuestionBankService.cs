using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IQuestionBankService
{
    Task<ErrorOr<QuestionBankDto>> CreateQuestionBankAsync(string name, string description, Guid? courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<List<QuestionBankDto>>> GetQuestionBanksByCourseAsync(Guid? courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankDto>> GetQuestionBankByIdAsync(Guid questionBankId, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankDto>> UpdateQuestionBankAsync(Guid questionBankId, string name, string description, Guid? courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuestionBankAsync(Guid questionBankId, CancellationToken ct = default);
    Task<ErrorOr<List<QuestionBankItemDto>>> GetQuestionBankItemsAsync(Guid questionBankId, string? search, string? type, string? difficulty, string? category, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankItemDto>> CreateQuestionBankItemAsync(Guid questionBankId, CreateQuestionBankItemRequest request, Guid createdBy, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankItemDto>> UpdateQuestionBankItemAsync(Guid itemId, UpdateQuestionBankItemRequest request, Guid updatedBy, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankItemDto>> DuplicateQuestionBankItemAsync(Guid itemId, Guid createdBy, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuestionBankItemAsync(Guid itemId, CancellationToken ct = default);
}
