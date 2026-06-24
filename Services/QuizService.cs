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
        var attempt = await _context.QuizAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found.");

        var quiz = await _context.Quizzes
            .Include(q => q.CourseOffering)
            .FirstOrDefaultAsync(q => q.Id == attempt.QuizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found.");

        var quizQuestions = await _context.QuizQuestions
            .Include(q => q.Options)
            .Where(q => q.QuizId == quiz.Id)
            .ToListAsync(ct);

        int correctCount = 0;
        
        // Remove existing answers for this attempt if any
        if (attempt.Answers != null && attempt.Answers.Any())
        {
            _context.QuizAnswers.RemoveRange(attempt.Answers);
            attempt.Answers.Clear();
        }

        foreach (var question in quizQuestions)
        {
            if (answers.TryGetValue(question.Id, out var submittedValue) && !string.IsNullOrWhiteSpace(submittedValue))
            {
                var quizAnswer = new QuizAnswer
                {
                    AttemptId = attempt.Id,
                    QuestionId = question.Id
                };

                bool isMultipleChoice = string.Equals(question.QuestionType, "MultipleChoice", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(question.QuestionType, "SingleChoice", StringComparison.OrdinalIgnoreCase) ||
                                         (question.Options != null && question.Options.Any());

                if (isMultipleChoice && Guid.TryParse(submittedValue, out var selectedOptionId))
                {
                    quizAnswer.SelectedOptionId = selectedOptionId;
                    var option = question.Options?.FirstOrDefault(o => o.Id == selectedOptionId);
                    if (option != null && option.IsCorrectAnswer)
                    {
                        correctCount++;
                    }
                }
                else
                {
                    quizAnswer.TextAnswer = submittedValue;
                }

                _context.QuizAnswers.Add(quizAnswer);
            }
        }

        decimal totalQuestions = quizQuestions.Count;
        decimal score = totalQuestions > 0 ? ((decimal)correctCount / totalQuestions) * 100m : 0m;

        attempt.EndTime = DateTime.UtcNow;
        attempt.TotalScore = score;
        
        // Find or create Assessment Category for CA1 (Quiz)
        var category = await _context.AssessmentCategories
            .FirstOrDefaultAsync(c => c.CourseOfferingId == quiz.CourseOfferingId && c.CategoryType == AssessmentCategoryType.CA1, ct);
        if (category == null)
        {
            category = new AssessmentCategory
            {
                CourseOfferingId = quiz.CourseOfferingId,
                CategoryType = AssessmentCategoryType.CA1,
                CategoryName = "CA1",
                Weight = 15m,
                MaxMarks = 100m,
                IsExamCategory = false,
                DisplayOrder = (int)AssessmentCategoryType.CA1
            };
            _context.AssessmentCategories.Add(category);
            await _context.SaveChangesAsync(ct);
        }

        // Find or create Assessment for this Quiz
        var assessment = await _context.Assessments
            .FirstOrDefaultAsync(a => a.CourseOfferingId == quiz.CourseOfferingId && a.AssessmentCategoryId == category.Id && a.Title == quiz.Title, ct);
        if (assessment == null)
        {
            assessment = new Assessment
            {
                CourseOfferingId = quiz.CourseOfferingId,
                AssessmentCategoryId = category.Id,
                Title = quiz.Title,
                Description = quiz.Description,
                MaxMarks = 100m,
                AssessmentDate = DateTime.UtcNow
            };
            _context.Assessments.Add(assessment);
            await _context.SaveChangesAsync(ct);
        }

        // Find or create Grade entry
        var grade = await _context.Grades
            .FirstOrDefaultAsync(g => g.AssessmentId == assessment.Id && g.StudentId == attempt.StudentId, ct);
        if (grade == null)
        {
            grade = new Grade
            {
                AssessmentId = assessment.Id,
                StudentId = attempt.StudentId,
                MarksObtained = score,
                IsLocked = false,
                Remarks = $"Autograded score for quiz: {quiz.Title}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Grades.Add(grade);
        }
        else if (!grade.IsLocked)
        {
            grade.MarksObtained = score;
            grade.Remarks = $"Autograded score updated for quiz: {quiz.Title}";
            grade.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        await LogActionAsync("SubmitQuizAnswers", "QuizAttempt", attempt.Id.ToString(), $"Submitted and autograded quiz answers. Score: {score}", ct);

        return new QuizResultDto(attemptId, score, 100);
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
