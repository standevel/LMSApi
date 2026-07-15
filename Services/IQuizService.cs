using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IQuizService
{
    // Quiz CRUD
    Task<ErrorOr<QuizDto>> CreateQuizAsync(Guid courseOfferingId, string title, string description, int timeLimitMinutes, CancellationToken ct = default);
    Task<ErrorOr<QuizWithSettingsDto>> CreateQuizWithSettingsAsync(CreateQuizRequest request, Guid createdBy, CancellationToken ct = default);
    Task<ErrorOr<List<QuizDto>>> GetQuizzesByCourseAsync(Guid? courseOfferingId, Guid? userId, CancellationToken ct = default);
    Task<ErrorOr<QuizDto>> GetQuizByIdAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default);
    Task<ErrorOr<QuizWithSettingsDto>> GetQuizWithSettingsAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UpdateQuizAsync(Guid quizId, string title, string description, int timeLimitMinutes, Guid? assessmentCategoryId, List<Guid>? targetProgramIds, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuizAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UpdateQuizStatusAsync(Guid quizId, string newStatus, Guid updatedBy, CancellationToken ct = default);
    
    // Quiz Settings
    Task<ErrorOr<QuizSettingDto>> UpdateQuizSettingsAsync(Guid quizId, UpdateQuizSettingRequest request, Guid updatedBy, CancellationToken ct = default);
    
    // Question Management
    Task<ErrorOr<List<QuizQuestionDto>>> GetQuestionsForQuizAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default);
    Task<ErrorOr<QuizQuestionDto>> AddQuestionToQuizAsync(Guid quizId, CreateQuizQuestionRequest request, CancellationToken ct = default);
    Task<ErrorOr<QuestionImportTemplateDto>> GenerateQuestionImportTemplateAsync(CancellationToken ct = default);
    Task<ErrorOr<QuestionImportResultDto>> ImportQuizQuestionsAsync(Guid quizId, IFormFile excelFile, bool previewOnly = false, CancellationToken ct = default);
    Task<ErrorOr<List<QuizQuestionDto>>> AddQuestionsFromBankAsync(Guid quizId, AddQuestionsFromBankRequest request, CancellationToken ct = default);
    Task<ErrorOr<QuizQuestionDto>> UpdateQuizQuestionAsync(Guid questionId, string questionText, int orderIndex, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuizQuestionAsync(Guid questionId, CancellationToken ct = default);
    
    // Question Option Management
    Task<ErrorOr<QuestionOptionDto>> AddOptionToQuestionAsync(Guid questionId, string optionText, int displayOrder, CancellationToken ct = default);
    Task<ErrorOr<QuestionOptionDto>> UpdateOptionAsync(Guid optionId, string optionText, bool isCorrectAnswer, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteOptionAsync(Guid optionId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> SetCorrectOptionAsync(Guid questionId, Guid optionId, CancellationToken ct = default);
    
    // Quiz Attempt
    Task<ErrorOr<QuizAttemptDto>> StartNewQuizAttemptAsync(Guid studentId, Guid quizId, string? accessCode = null, IReadOnlyCollection<string>? clientIps = null, CancellationToken ct = default);
    Task<ErrorOr<QuizResultDto>> SubmitAnswersForGradingAsync(Guid attemptId, Dictionary<Guid, string> answers, Guid? userId = null, CancellationToken ct = default);
    Task<ErrorOr<QuizAttemptDto>> GetQuizAttemptAsync(Guid attemptId, Guid? userId = null, CancellationToken ct = default);
    Task<ErrorOr<QuizResultDto>> GetQuizResultAsync(Guid attemptId, Guid? userId = null, CancellationToken ct = default);
    
    // Quiz Analytics
    Task<ErrorOr<List<QuizAttemptWithStudentDto>>> GetQuizAttemptsAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<QuizStatisticsDto>> GetQuizStatisticsAsync(Guid quizId, CancellationToken ct = default);
    
    // Quiz Feedback
    Task<ErrorOr<List<QuizFeedbackDto>>> GetQuizFeedbacksAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<QuizFeedbackDto>> CreateQuizFeedbackAsync(Guid quizId, Guid studentId, Guid? questionId, string feedbackText, string feedbackType, string? gradingNotes, decimal? manualOverrideScore, Guid createdBy, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> UpdateQuizFeedbackAsync(Guid feedbackId, string? feedbackText, string? gradingNotes, decimal? manualOverrideScore, Guid updatedBy, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> DeleteQuizFeedbackAsync(Guid feedbackId, CancellationToken ct = default);
    
    // Time Extension
    Task<ErrorOr<List<TimeExtensionDto>>> GetQuizTimeExtensionsAsync(Guid quizId, CancellationToken ct = default);
    Task<ErrorOr<TimeExtensionDto>> CreateTimeExtensionAsync(Guid quizId, Guid studentId, int additionalMinutes, DateTime? effectiveFrom, DateTime? effectiveUntil, string reason, string approvedBy, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> RevokeTimeExtensionAsync(Guid timeExtensionId, CancellationToken ct = default);

    Task<ErrorOr<int>> ImportQuizzesFromOfferingAsync(Guid sourceOfferingId, Guid targetOfferingId, Guid userId, CancellationToken ct = default);
}
