using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public class QuizService : BaseService, IQuizService
{
    private readonly LmsDbContext _context;

    public QuizService(LmsDbContext context, IAuditService auditService) : base(auditService)
    {
        _context = context;
    }

    public async Task<ErrorOr<QuizDto>> CreateQuizAsync(Guid courseOfferingId, string title, string description, int timeLimitMinutes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return Error.Validation("InvalidInput", "Title and Description are required.");

        var quiz = new Quiz
        {
            Title = title,
            Description = description,
            TimeLimitMinutes = timeLimitMinutes,
            CourseOfferingId = courseOfferingId
        };

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync(ct);
        return MapToDto(quiz);
    }

    public async Task<ErrorOr<List<QuizDto>>> GetQuizzesByCourseAsync(Guid courseOfferingId, CancellationToken ct = default)
    {
        var quizzes = await _context.Quizzes
            .Where(q => q.CourseOfferingId == courseOfferingId)
            .ToListAsync(ct);
        return quizzes.Select(MapToDto).ToList();
    }

    public async Task<ErrorOr<QuizDto>> GetQuizByIdAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        return quiz == null ? Error.NotFound("Quiz.NotFound", "Quiz not found") : MapToDto(quiz);
    }

    public async Task<ErrorOr<Deleted>> UpdateQuizAsync(Guid quizId, string title, string description, int timeLimitMinutes, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        quiz.Title = title;
        quiz.Description = description;
        quiz.TimeLimitMinutes = timeLimitMinutes;
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteQuizAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        _context.Quizzes.Remove(quiz);
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<QuizQuestionDto>>> GetQuestionsForQuizAsync(Guid quizId, CancellationToken ct = default)
    {
        var questions = await _context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(ct);
        return questions.Select(MapQuestionToDto).ToList();
    }

    public async Task<ErrorOr<QuizQuestionDto>> AddQuestionToQuizAsync(Guid quizId, string questionText, int orderIndex, string questionType, CancellationToken ct = default)
    {
        var newQuestion = new QuizQuestion
        {
            QuizId = quizId,
            QuestionText = questionText,
            OrderIndex = orderIndex,
            QuestionType = questionType
        };
        _context.QuizQuestions.Add(newQuestion);
        await _context.SaveChangesAsync(ct);
        return MapQuestionToDto(newQuestion);
    }

    public async Task<ErrorOr<QuizAttemptDto>> StartNewQuizAttemptAsync(Guid studentId, Guid quizId, CancellationToken ct = default)
    {
        var attempt = new QuizAttempt
        {
            StudentId = studentId,
            QuizId = quizId,
            StartTime = DateTime.UtcNow,
            EndTime = null,
            TotalScore = 0m
        };
        _context.QuizAttempts.Add(attempt);
        await _context.SaveChangesAsync(ct);
        return new QuizAttemptDto(attempt.Id.ToString(), attempt.StartTime);
    }

    public async Task<ErrorOr<QuizResultDto>> SubmitAnswersForGradingAsync(Guid attemptId, Dictionary<Guid, string> answers, CancellationToken ct = default)
    {
        var attempt = await _context.QuizAttempts.FindAsync(new object[] { attemptId }, ct);
        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found.");

        attempt.EndTime = DateTime.UtcNow;
        attempt.TotalScore = 0; // Simplified scoring
        await _context.SaveChangesAsync(ct);
        return new QuizResultDto(attemptId, attempt.TotalScore, 100);
    }

    public async Task<ErrorOr<QuizQuestionDto>> UpdateQuizQuestionAsync(Guid questionId, string questionText, int orderIndex, CancellationToken ct = default)
    {
        var question = await _context.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question == null) return Error.NotFound("Question.NotFound", "Quiz question not found");

        question.QuestionText = questionText;
        question.OrderIndex = orderIndex;
        await _context.SaveChangesAsync(ct);
        return MapQuestionToDto(question);
    }

    public async Task<ErrorOr<Deleted>> DeleteQuizQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        var question = await _context.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question == null) return Error.NotFound("Question.NotFound", "Quiz question not found");

        _context.QuizQuestions.Remove(question);
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<QuizAttemptDto>> GetQuizAttemptAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _context.QuizAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);

        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found");

        return new QuizAttemptDto(attempt.Id.ToString(), attempt.StartTime);
    }

    private QuizDto MapToDto(Quiz quiz) => new QuizDto
    {
        Id = quiz.Id,
        Title = quiz.Title,
        Description = quiz.Description,
        TimeLimitMinutes = quiz.TimeLimitMinutes ?? 0,
        CourseOfferingId = quiz.CourseOfferingId
    };

    private QuizQuestionDto MapQuestionToDto(QuizQuestion q) => new QuizQuestionDto
    {
        Id = q.Id,
        QuestionText = q.QuestionText,
        OrderIndex = q.OrderIndex,
        QuestionType = q.QuestionType
    };
}
