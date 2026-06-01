using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IQuizService
{
    Task<ErrorOr<QuizDto>> CreateQuizAsync(Guid courseOfferingId, string title, string description, int timeLimitMinutes, CancellationToken ct = default);
    Task<ErrorOr<List<QuizDto>>> GetQuizzesByCourseAsync(Guid courseOfferingId, CancellationToken ct = default);
    Task<ErrorOr<QuizDto>> GetQuizByIdAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UpdateQuizAsync(Guid quizId, string title, string description, int timeLimitMinutes, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuizAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<List<QuizQuestionDto>>> GetQuestionsForQuizAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<QuizQuestionDto>> AddQuestionToQuizAsync(Guid quizId, string questionText, int orderIndex, string questionType, CancellationToken ct = default);
    Task<ErrorOr<QuizQuestionDto>> UpdateQuizQuestionAsync(Guid questionId, string questionText, int orderIndex, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuizQuestionAsync(Guid questionId, CancellationToken ct = default);
    Task<ErrorOr<QuizAttemptDto>> StartNewQuizAttemptAsync(Guid studentId, Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<QuizResultDto>> SubmitAnswersForGradingAsync(Guid attemptId, Dictionary<Guid, string> answers, CancellationToken ct = default);
    Task<ErrorOr<QuizAttemptDto>> GetQuizAttemptAsync(Guid attemptId, CancellationToken ct = default);
}