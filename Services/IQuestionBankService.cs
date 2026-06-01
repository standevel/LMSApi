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
    Task<ErrorOr<List<QuestionBankDto>>> GetQuestionBanksByCourseAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<QuestionBankDto>> GetQuestionBankByIdAsync(Guid questionBankId, CancellationToken ct = default);
}