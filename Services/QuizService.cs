using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    public async Task<ErrorOr<QuizWithSettingsDto>> CreateQuizWithSettingsAsync(CreateQuizRequest request, Guid createdBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return Error.Validation("InvalidInput", "Title and Description are required.");

        var targetProgramIdsJson = await BuildTargetProgramIdsJsonAsync(request.CourseOfferingId, request.TargetProgramIds, ct);
        if (targetProgramIdsJson.IsError) return targetProgramIdsJson.Errors;

        var quiz = new Quiz
        {
            Title = request.Title,
            Description = request.Description,
            TimeLimitMinutes = request.TimeLimitMinutes,
            CourseOfferingId = request.CourseOfferingId,
            Status = request.Status ?? "Draft",
            OpenDateUtc = request.OpenDateUtc,
            CloseDateUtc = request.CloseDateUtc,
            PassThreshold = request.PassThreshold,
            AssessmentCategoryId = request.AssessmentCategoryId,
            TargetProgramIdsJson = targetProgramIdsJson.Value,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.Quizzes.Add(quiz);
        await _context.SaveChangesAsync(ct);

        // Create default quiz settings
        var settings = new QuizSetting
        {
            QuizId = quiz.Id,
            ShuffleQuestions = request.ShuffleQuestions,
            ShuffleOptions = request.ShuffleOptions,
            MaxAttempts = request.MaxAttempts,
            AllowPartialCredit = request.AllowPartialCredit,
            ScoreBestAttempt = request.ScoreBestAttempt,
            OpenDateUtc = request.OpenDateUtc,
            CloseDateUtc = request.CloseDateUtc,
            Status = request.Status ?? "Draft",
            PassThreshold = request.PassThreshold,
            FeedbackVisibility = request.FeedbackVisibility ?? "Immediate",
            UseRandomPool = request.UseRandomPool,
            PoolSize = request.PoolSize,
            PoolQuestionBankId = request.PoolQuestionBankId,
            RequireFullscreen = request.RequireFullscreen,
            AllowTabSwitchDetection = request.AllowTabSwitchDetection,
            MaxTabSwitches = request.MaxTabSwitches,
            AccessCode = request.AccessCode,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.QuizSettings.Add(settings);
        await _context.SaveChangesAsync(ct);

        return MapToWithSettingsDto(quiz, settings, includeCorrectAnswers: true, includeAccessCode: true);
    }

    public async Task<ErrorOr<QuizWithSettingsDto>> GetQuizWithSettingsAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Setting)
            .Include(q => q.Questions)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);

        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var settings = quiz.Setting;
        if (settings == null)
        {
            settings = new QuizSetting
            {
                QuizId = quiz.Id,
                MaxAttempts = 1,
                AllowPartialCredit = true,
                FeedbackVisibility = "Immediate"
            };
            _context.QuizSettings.Add(settings);
            await _context.SaveChangesAsync(ct);
        }

        var canManageQuiz = await CanManageQuizAsync(userId, quiz, ct);
        if (!canManageQuiz && userId.HasValue && !await StudentCanAccessQuizProgramAsync(userId.Value, quiz, ct))
            return Error.Forbidden("Quiz.Forbidden", "This quiz is not available to your program.");

        var questions = canManageQuiz
            ? quiz.Questions.ToList()
            : new List<QuizQuestion>();
        var timeLimitOverride = canManageQuiz || !userId.HasValue || !quiz.TimeLimitMinutes.HasValue
            ? (int?)null
            : quiz.TimeLimitMinutes.Value + await GetActiveTimeExtensionMinutesAsync(quiz.Id, userId.Value, DateTime.UtcNow, ct);

        return MapToWithSettingsDto(quiz, settings, includeCorrectAnswers: canManageQuiz, includeAccessCode: canManageQuiz, questionsOverride: questions, timeLimitOverrideMinutes: timeLimitOverride);
    }

    public async Task<ErrorOr<QuizSettingDto>> UpdateQuizSettingsAsync(Guid quizId, UpdateQuizSettingRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var settings = await _context.QuizSettings.FirstOrDefaultAsync(s => s.QuizId == quizId, ct);
        if (settings == null)
        {
            settings = new QuizSetting { QuizId = quizId, CreatedBy = updatedBy };
            _context.QuizSettings.Add(settings);
        }

        if (request.ShuffleQuestions.HasValue) settings.ShuffleQuestions = request.ShuffleQuestions.Value;
        if (request.ShuffleOptions.HasValue) settings.ShuffleOptions = request.ShuffleOptions.Value;
        if (request.MaxAttempts.HasValue) settings.MaxAttempts = request.MaxAttempts.Value;
        if (request.AllowPartialCredit.HasValue) settings.AllowPartialCredit = request.AllowPartialCredit.Value;
        if (request.ScoreBestAttempt.HasValue) settings.ScoreBestAttempt = request.ScoreBestAttempt.Value;
        // Assign dates directly — null is allowed to clear the field
        settings.OpenDateUtc = request.OpenDateUtc;
        settings.CloseDateUtc = request.CloseDateUtc;
        if (!string.IsNullOrEmpty(request.Status)) settings.Status = request.Status;
        if (request.PassThreshold.HasValue) settings.PassThreshold = request.PassThreshold.Value;
        if (request.UseRandomPool.HasValue) settings.UseRandomPool = request.UseRandomPool.Value;
        if (request.PoolSize.HasValue) settings.PoolSize = request.PoolSize.Value;
        if (request.PoolQuestionBankId.HasValue) settings.PoolQuestionBankId = request.PoolQuestionBankId.Value;
        if (!string.IsNullOrEmpty(request.FeedbackVisibility)) settings.FeedbackVisibility = request.FeedbackVisibility;
        if (request.RequireFullscreen.HasValue) settings.RequireFullscreen = request.RequireFullscreen.Value;
        if (request.AllowTabSwitchDetection.HasValue) settings.AllowTabSwitchDetection = request.AllowTabSwitchDetection.Value;
        if (request.MaxTabSwitches.HasValue) settings.MaxTabSwitches = request.MaxTabSwitches.Value;
        // Allow explicit empty string to clear the access code
        if (request.AccessCode is not null) settings.AccessCode = string.IsNullOrEmpty(request.AccessCode) ? null : request.AccessCode;
        if (request.RestrictToAllowedIps.HasValue) settings.RestrictToAllowedIps = request.RestrictToAllowedIps.Value;
        if (request.AllowedIpRanges is not null)
        {
            var normalizedRanges = IpRangeMatcher.NormalizeRanges(request.AllowedIpRanges);
            var rangeErrors = IpRangeMatcher.ValidateRanges(normalizedRanges);
            if (rangeErrors.Count > 0)
            {
                return Error.Validation("Quiz.InvalidIpRanges", string.Join(" ", rangeErrors));
            }

            settings.AllowedIpRangesJson = normalizedRanges.Count == 0 ? null : JsonSerializer.Serialize(normalizedRanges);
        }

        if (request.AllowedCbtHallIds is not null)
        {
            var hallIds = request.AllowedCbtHallIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (hallIds.Count > 0)
            {
                var existingHallIds = await _context.CbtHalls
                    .Where(hall => hallIds.Contains(hall.Id))
                    .Select(hall => hall.Id)
                    .ToListAsync(ct);

                if (existingHallIds.Count != hallIds.Count)
                {
                    return Error.Validation("Quiz.InvalidCbtHalls", "One or more selected CBT halls could not be found.");
                }
            }

            settings.AllowedCbtHallIdsJson = hallIds.Count == 0 ? null : JsonSerializer.Serialize(hallIds);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(ct);
        return MapSettingToDto(settings);
    }

    public async Task<ErrorOr<Deleted>> UpdateQuizStatusAsync(Guid quizId, string newStatus, Guid updatedBy, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var validStatuses = new[] { "Draft", "Scheduled", "Published", "Archived", "Closed" };
        if (!validStatuses.Contains(newStatus))
            return Error.Validation("InvalidStatus", $"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");

        quiz.Status = newStatus;
        quiz.UpdatedAt = DateTime.UtcNow;
        quiz.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<List<QuizDto>>> GetQuizzesByCourseAsync(Guid? courseOfferingId, Guid? userId, CancellationToken ct = default)
    {
        var requestedCourseOfferingId = courseOfferingId.HasValue && courseOfferingId.Value != Guid.Empty
            ? courseOfferingId.Value
            : (Guid?)null;

        if (!userId.HasValue)
        {
            var anonymousQuery = _context.Quizzes.AsQueryable();
            if (requestedCourseOfferingId.HasValue)
            {
                anonymousQuery = anonymousQuery.Where(q => q.CourseOfferingId == requestedCourseOfferingId.Value);
            }

            return await LoadQuizDtosAsync(anonymousQuery, includeCorrectAnswers: false, ct);
        }

        var actualUserId = userId.Value;
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == actualUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isAdmin = userRoles.Any(r =>
            string.Equals(r, LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, LmsRoles.HOD, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, LmsRoles.Dean, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, LmsRoles.ViceChancellor, StringComparison.OrdinalIgnoreCase));

        if (isAdmin)
        {
            var adminQuery = _context.Quizzes.AsQueryable();
            if (requestedCourseOfferingId.HasValue)
            {
                adminQuery = adminQuery.Where(q => q.CourseOfferingId == requestedCourseOfferingId.Value);
            }

            return await LoadQuizDtosAsync(adminQuery, includeCorrectAnswers: true, ct);
        }

        var isLecturer = userRoles.Any(r =>
            string.Equals(r, LmsRoles.Lecturer, StringComparison.OrdinalIgnoreCase));

        if (isLecturer)
        {
            var userIdStr = actualUserId.ToString();
            var userCourseOfferingIds = await _context.CourseOfferings
                .Where(co => co.LecturerId == actualUserId ||
                             _context.LectureTimetableSlots.Any(slot =>
                                 slot.CourseOfferingId == co.Id &&
                                 (slot.LecturerId == actualUserId ||
                                  (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(userIdStr)))))
                .Select(co => co.Id)
                .ToListAsync(ct);

            var lecturerQuery = _context.Quizzes
                .Where(q => q.CreatedBy == actualUserId || userCourseOfferingIds.Contains(q.CourseOfferingId));

            if (requestedCourseOfferingId.HasValue)
            {
                lecturerQuery = lecturerQuery.Where(q => q.CourseOfferingId == requestedCourseOfferingId.Value);
            }

            return await LoadQuizDtosAsync(lecturerQuery, includeCorrectAnswers: true, ct);
        }

        var isStudent = userRoles.Any(r =>
            string.Equals(r, LmsRoles.Student, StringComparison.OrdinalIgnoreCase));

        if (isStudent)
        {
            var enrolledCourseOfferingIds = await _context.CourseEnrollments
                .Where(ce => ce.StudentId == actualUserId && ce.Status == "Registered")
                .Select(ce => ce.CourseOfferingId)
                .ToListAsync(ct);

            var studentQuery = _context.Quizzes
                .Include(q => q.CourseOffering)
                .Where(q =>
                    enrolledCourseOfferingIds.Contains(q.CourseOfferingId) ||
                    _context.CourseEnrollments.Any(enrollment =>
                        enrollment.StudentId == actualUserId &&
                        enrollment.Status == "Registered" &&
                        enrollment.CourseOffering.CourseId == q.CourseOffering!.CourseId));

            if (requestedCourseOfferingId.HasValue)
            {
                var requestedCourseId = await _context.CourseOfferings
                    .Where(offering => offering.Id == requestedCourseOfferingId.Value)
                    .Select(offering => offering.CourseId)
                    .FirstOrDefaultAsync(ct);

                studentQuery = studentQuery.Where(q =>
                    q.CourseOfferingId == requestedCourseOfferingId.Value ||
                    q.CourseOffering!.CourseId == requestedCourseId);
            }

            var candidateQuizzes = await studentQuery
                .Include(q => q.Questions)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync(ct);

            var visibleQuizzes = new List<QuizDto>();
            foreach (var quiz in candidateQuizzes)
            {
                if (await StudentCanAccessQuizProgramAsync(actualUserId, quiz, ct))
                {
                    visibleQuizzes.Add(MapToDto(quiz, includeCorrectAnswers: false));
                }
            }

            return visibleQuizzes;
        }

        return new List<QuizDto>();
    }

    private async Task<List<QuizDto>> LoadQuizDtosAsync(IQueryable<Quiz> query, bool includeCorrectAnswers, CancellationToken ct)
    {
        var quizzes = await query
            .Include(q => q.Questions)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

        return quizzes.Select(quiz => MapToDto(quiz, includeCorrectAnswers)).ToList();
    }

    public async Task<ErrorOr<QuizDto>> GetQuizByIdAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var includeCorrectAnswers = await CanManageQuizAsync(userId, quiz, ct);
        if (!includeCorrectAnswers && userId.HasValue && !await StudentCanAccessQuizProgramAsync(userId.Value, quiz, ct))
            return Error.Forbidden("Quiz.Forbidden", "This quiz is not available to your program.");

        return MapToDto(quiz, includeCorrectAnswers);
    }

    public async Task<ErrorOr<Deleted>> UpdateQuizAsync(Guid quizId, string title, string description, int timeLimitMinutes, Guid? assessmentCategoryId, List<Guid>? targetProgramIds, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        quiz.Title = title;
        quiz.Description = description;
        quiz.TimeLimitMinutes = timeLimitMinutes;
        quiz.AssessmentCategoryId = assessmentCategoryId;
        if (targetProgramIds is not null)
        {
            var targetProgramIdsJson = await BuildTargetProgramIdsJsonAsync(quiz.CourseOfferingId, targetProgramIds, ct);
            if (targetProgramIdsJson.IsError) return targetProgramIdsJson.Errors;
            quiz.TargetProgramIdsJson = targetProgramIdsJson.Value;
        }
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

    public async Task<ErrorOr<List<QuizQuestionDto>>> GetQuestionsForQuizAsync(Guid quizId, Guid? userId = null, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var includeCorrectAnswers = await CanManageQuizAsync(userId, quiz, ct);
        if (!includeCorrectAnswers && userId.HasValue && !await StudentCanAccessQuizProgramAsync(userId.Value, quiz, ct))
            return Error.Forbidden("Quiz.Forbidden", "This quiz is not available to your program.");

        var questions = await _context.QuizQuestions
            .Include(q => q.Options)
            .Where(q => q.QuizId == quizId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(ct);

        if (!includeCorrectAnswers)
        {
            if (!userId.HasValue)
            {
                return Error.Unauthorized("Quiz.AttemptRequired", "Start the quiz before loading questions.");
            }

            var hasOpenAttempt = await _context.QuizAttempts
                .AnyAsync(attempt => attempt.QuizId == quizId && attempt.StudentId == userId.Value && !attempt.EndTime.HasValue, ct);

            if (!hasOpenAttempt)
            {
                return Error.Validation("Quiz.AttemptRequired", "Start the quiz before loading questions.");
            }

            var settings = await _context.QuizSettings.FirstOrDefaultAsync(setting => setting.QuizId == quizId, ct);
            questions = SelectQuestionsForStudent(questions, settings, userId.Value);
        }

        return questions.Select(question => MapQuestionToDto(question, includeCorrectAnswers)).ToList();
    }

    public async Task<ErrorOr<QuizQuestionDto>> AddQuestionToQuizAsync(Guid quizId, CreateQuizQuestionRequest request, CancellationToken ct = default)
    {
        var quizExists = await _context.Quizzes.AnyAsync(q => q.Id == quizId, ct);
        if (!quizExists) return Error.NotFound("Quiz.NotFound", "Quiz not found");
        if (string.IsNullOrWhiteSpace(request.QuestionText)) return Error.Validation("InvalidInput", "Question text is required.");

        var newQuestion = new QuizQuestion
        {
            QuizId = quizId,
            QuestionText = request.QuestionText,
            OrderIndex = request.OrderIndex,
            QuestionType = request.QuestionType,
            Points = request.Points <= 0 ? 1 : request.Points,
            Difficulty = request.Difficulty,
            Category = request.Category,
            Tags = request.Tags,
            Explanation = request.Explanation,
            Options = request.OptionTexts.Select((text, index) => new QuestionOption
            {
                OptionText = text,
                DisplayOrder = index + 1,
                IsCorrectAnswer = false
            }).ToList()
        };
        _context.QuizQuestions.Add(newQuestion);
        await _context.SaveChangesAsync(ct);
        return MapQuestionToDto(newQuestion);
    }

    public Task<ErrorOr<QuestionImportTemplateDto>> GenerateQuestionImportTemplateAsync(CancellationToken ct = default)
    {
        return Task.FromResult<ErrorOr<QuestionImportTemplateDto>>(
            QuestionExcelImportHelper.GenerateTemplate("Question_Import_Template.xlsx"));
    }

    public async Task<ErrorOr<QuestionImportResultDto>> ImportQuizQuestionsAsync(Guid quizId, IFormFile excelFile, bool previewOnly = false, CancellationToken ct = default)
    {
        var quizExists = await _context.Quizzes.AnyAsync(q => q.Id == quizId, ct);
        if (!quizExists) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var parsed = await QuestionExcelImportHelper.ParseAsync(excelFile, ct);
        if (parsed.IsError) return parsed.Errors;

        var result = new QuestionImportResultDto
        {
            TotalRows = parsed.Value.Count,
            PreviewOnly = previewOnly
        };

        var nextOrder = await _context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .MaxAsync(q => (int?)q.OrderIndex, ct) ?? 0;

        var questions = new List<QuizQuestion>();
        foreach (var row in parsed.Value)
        {
            var rowError = ValidateImportedQuestion(row);
            if (rowError is not null)
            {
                result.Errors.Add(new QuestionImportRowErrorDto { RowNumber = row.RowNumber, Message = rowError });
                continue;
            }

            nextOrder++;
            result.Questions.Add(QuestionExcelImportHelper.ToPreview(row));
            questions.Add(new QuizQuestion
            {
                QuizId = quizId,
                QuestionText = row.QuestionText,
                OrderIndex = nextOrder,
                QuestionType = row.QuestionType,
                Points = row.Points,
                Difficulty = row.Difficulty,
                Category = row.Category,
                Tags = row.Tags,
                Explanation = row.Explanation,
                Options = row.Options.Select(option => new QuestionOption
                {
                    OptionText = option.Text,
                    DisplayOrder = option.DisplayOrder,
                    IsCorrectAnswer = option.IsCorrect
                }).ToList()
            });
        }

        result.ImportedCount = previewOnly ? 0 : questions.Count;
        result.SkippedCount = result.Errors.Count;

        if (!previewOnly && questions.Count > 0)
        {
            _context.QuizQuestions.AddRange(questions);
            await _context.SaveChangesAsync(ct);
        }

        return result;
    }

    public async Task<ErrorOr<List<QuizQuestionDto>>> AddQuestionsFromBankAsync(Guid quizId, AddQuestionsFromBankRequest request, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var query = _context.QuestionBankItems
            .Include(item => item.Options)
            .Where(item => item.QuestionBankId == request.QuestionBankId);

        if (request.QuestionBankItemIds.Count > 0)
        {
            query = query.Where(item => request.QuestionBankItemIds.Contains(item.Id));
        }

        var items = await query.ToListAsync(ct);
        if (request.RandomizeSelection)
        {
            items = items.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        if (request.Limit.HasValue && request.Limit.Value > 0)
        {
            items = items.Take(request.Limit.Value).ToList();
        }

        var nextOrder = await _context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .MaxAsync(q => (int?)q.OrderIndex, ct) ?? 0;

        var questions = new List<QuizQuestion>();
        foreach (var item in items)
        {
            nextOrder++;
            var question = item.ToQuizQuestion(nextOrder);
            question.QuizId = quizId;
            questions.Add(question);
            item.TimesUsed++;
        }

        _context.QuizQuestions.AddRange(questions);
        await _context.SaveChangesAsync(ct);
        return questions.Select(question => MapQuestionToDto(question)).ToList();
    }

    public async Task<ErrorOr<QuizAttemptDto>> StartNewQuizAttemptAsync(Guid studentId, Guid quizId, string? accessCode = null, IReadOnlyCollection<string>? clientIps = null, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes
            .Include(q => q.Setting)
            .FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found.");

        if (!await CanManageQuizAsync(studentId, quiz, ct) && !await StudentCanAccessQuizProgramAsync(studentId, quiz, ct))
        {
            return Error.Forbidden("Quiz.Forbidden", "This quiz is not available to your program.");
        }

        var now = DateTime.UtcNow;
        var settings = quiz.Setting;
        var status = settings?.Status ?? quiz.Status;
        var openDate = settings?.OpenDateUtc ?? quiz.OpenDateUtc;
        var closeDate = settings?.CloseDateUtc ?? quiz.CloseDateUtc;

        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == studentId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);
        var canPreviewUnavailableQuiz = userRoles.Any(r => r == "SuperAdmin" || r == "Admin" || r == "Lecturer");

        if (!canPreviewUnavailableQuiz)
        {
            if (status is "Archived" or "Closed" or "Draft")
                return Error.Validation("Quiz.Unavailable", "This quiz is not currently available.");
            if (openDate.HasValue && now < openDate.Value)
                return Error.Validation("Quiz.NotOpen", "This quiz has not opened yet.");
            if (closeDate.HasValue && now > closeDate.Value)
                return Error.Validation("Quiz.Closed", "This quiz is closed.");
        }

        if (!string.IsNullOrWhiteSpace(settings?.AccessCode) &&
            !string.Equals(settings.AccessCode.Trim(), accessCode?.Trim(), StringComparison.Ordinal))
        {
            return Error.Validation("Quiz.InvalidAccessCode", "The access code is incorrect.");
        }

        if (settings?.RestrictToAllowedIps == true &&
            !await IsClientIpAllowedForQuizAsync(settings, clientIps, ct))
        {
            return Error.Validation("Quiz.IpRestricted", "This quiz can only be taken from an approved CBT hall or allowed network.");
        }

        var maxAttempts = settings?.MaxAttempts ?? 1;
        var completedAttempts = await _context.QuizAttempts
            .CountAsync(a => a.QuizId == quizId && a.StudentId == studentId && a.EndTime.HasValue, ct);
        if (completedAttempts >= maxAttempts)
            return Error.Validation("Quiz.MaxAttemptsReached", "You have used all allowed attempts for this quiz.");

        var openAttempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.QuizId == quizId && a.StudentId == studentId && !a.EndTime.HasValue, ct);
        if (openAttempt != null)
        {
            if (await IsAttemptTimeExpiredAsync(quiz, openAttempt, now, ct))
            {
                openAttempt.EndTime = now;
                openAttempt.TotalScore = 0m;
                await _context.SaveChangesAsync(ct);

                completedAttempts++;
                if (completedAttempts >= maxAttempts)
                {
                    return Error.Validation("Quiz.TimeExpired", "The time limit for your previous attempt has expired.");
                }
            }
            else
            {
                return new QuizAttemptDto(
                    openAttempt.Id.ToString(),
                    openAttempt.StudentId.ToString(),
                    openAttempt.QuizId.ToString(),
                    openAttempt.StartTime,
                    openAttempt.EndTime,
                    openAttempt.TotalScore,
                    new List<QuizAnswerDetailDto>());
            }
        }

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
        return new QuizAttemptDto(
            attempt.Id.ToString(),
            attempt.StudentId.ToString(),
            attempt.QuizId.ToString(),
            attempt.StartTime,
            attempt.EndTime,
            attempt.TotalScore,
            new List<QuizAnswerDetailDto>());
    }

    public async Task<ErrorOr<QuizResultDto>> SubmitAnswersForGradingAsync(Guid attemptId, Dictionary<Guid, string> answers, Guid? userId = null, CancellationToken ct = default)
    {
        var attempt = await _context.QuizAttempts
            .Include(a => a.Answers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found.");
        if (!await CanAccessAttemptAsync(userId, attempt, ct))
            return Error.Forbidden("Attempt.Forbidden", "You cannot submit this quiz attempt.");
        if (attempt.EndTime.HasValue)
            return Error.Validation("Attempt.AlreadySubmitted", "This quiz attempt has already been submitted.");

        var quiz = await _context.Quizzes
            .Include(q => q.CourseOffering)
            .Include(q => q.Setting)
            .FirstOrDefaultAsync(q => q.Id == attempt.QuizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found.");

        var now = DateTime.UtcNow;
        var settings = quiz.Setting;
        var closeDate = settings?.CloseDateUtc ?? quiz.CloseDateUtc;
        if (closeDate.HasValue && now > closeDate.Value)
            return Error.Validation("Quiz.Closed", "This quiz is closed and can no longer be submitted.");

        if (quiz.TimeLimitMinutes.HasValue && quiz.TimeLimitMinutes.Value > 0)
        {
            if (await IsAttemptTimeExpiredAsync(quiz, attempt, now, ct))
                return Error.Validation("Quiz.TimeExpired", "The time limit for this quiz has expired.");
        }

        var quizQuestions = await _context.QuizQuestions
            .Include(q => q.Options)
            .Where(q => q.QuizId == quiz.Id)
            .ToListAsync(ct);
        quizQuestions = SelectQuestionsForStudent(quizQuestions, settings, attempt.StudentId);

        decimal earnedPoints = 0;
        decimal possiblePoints = quizQuestions.Sum(q => q.Points > 1 ? q.Points : 1);
        
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
                        earnedPoints += Math.Max(question.Points, 1);
                    }
                }
                else
                {
                    quizAnswer.TextAnswer = submittedValue;
                }

                _context.QuizAnswers.Add(quizAnswer);
            }
        }

        decimal score = possiblePoints > 0 ? (earnedPoints / possiblePoints) * 100m : 0m;

        attempt.EndTime = DateTime.UtcNow;
        attempt.TotalScore = score;
        
        // Find or create Assessment Category
        AssessmentCategory? category = null;
        if (quiz.AssessmentCategoryId.HasValue)
        {
            category = await _context.AssessmentCategories.FirstOrDefaultAsync(c => c.Id == quiz.AssessmentCategoryId.Value, ct);
        }

        if (category == null)
        {
            category = await _context.AssessmentCategories
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

        return await BuildQuizResultDtoAsync(attempt, quiz, Math.Round(earnedPoints, 2), Math.Round(possiblePoints, 2), userId, ct);
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

    public async Task<ErrorOr<QuizAttemptDto>> GetQuizAttemptAsync(Guid attemptId, Guid? userId = null, CancellationToken ct = default)
    {
        var attempt = await _context.QuizAttempts
            .Include(a => a.Answers)
                .ThenInclude(a => a.SelectedOption)
            .Include(a => a.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == attemptId, ct);

        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found");
        if (!await CanAccessAttemptAsync(userId, attempt, ct))
            return Error.Forbidden("Attempt.Forbidden", "You cannot access this quiz attempt.");

        return new QuizAttemptDto(
            attempt.Id.ToString(),
            attempt.StudentId.ToString(),
            attempt.QuizId.ToString(),
            attempt.StartTime,
            attempt.EndTime,
            attempt.TotalScore,
            attempt.Answers?.Select(ans => new QuizAnswerDetailDto(
                ans.QuestionId,
                ans.Question?.QuestionText ?? "",
                ans.SelectedOptionId,
                ans.SelectedOptionId.HasValue ? ans.SelectedOption?.OptionText : null,
                ans.SelectedOptionId.HasValue && ans.SelectedOption?.IsCorrectAnswer == true,
                ans.TextAnswer
            )).ToList() ?? new());
    }

    public async Task<ErrorOr<QuizResultDto>> GetQuizResultAsync(Guid attemptId, Guid? userId = null, CancellationToken ct = default)
    {
        var attempt = await _context.QuizAttempts.FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt == null) return Error.NotFound("Attempt.NotFound", "Quiz attempt not found");
        if (!await CanAccessAttemptAsync(userId, attempt, ct))
            return Error.Forbidden("Attempt.Forbidden", "You cannot access this quiz result.");
        if (!attempt.EndTime.HasValue)
            return Error.Validation("Attempt.NotSubmitted", "This quiz attempt has not been submitted.");

        var quiz = await _context.Quizzes
            .Include(q => q.Setting)
            .FirstOrDefaultAsync(q => q.Id == attempt.QuizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var questionPoints = SelectQuestionsForStudent(
                await _context.QuizQuestions
                    .Where(q => q.QuizId == attempt.QuizId)
                    .ToListAsync(ct),
                quiz.Setting,
                attempt.StudentId)
            .Select(q => q.Points)
            .ToList();
        var possiblePoints = questionPoints.Sum(p => (decimal)Math.Max(p, 1));
        var earnedPoints = possiblePoints > 0 ? attempt.TotalScore / 100m * possiblePoints : 0m;

        return await BuildQuizResultDtoAsync(attempt, quiz, Math.Round(earnedPoints, 2), Math.Round(possiblePoints, 2), userId, ct);
    }

    private async Task<List<string>> GetUserRolesAsync(Guid userId, CancellationToken ct)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);
    }

    private static bool IsQuizManagerRole(IEnumerable<string> roles)
    {
        return roles.Any(role =>
            string.Equals(role, LmsRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, LmsRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, LmsRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, LmsRoles.HOD, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, LmsRoles.Dean, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, LmsRoles.ViceChancellor, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> CanManageQuizAsync(Guid? userId, Quiz quiz, CancellationToken ct)
    {
        if (!userId.HasValue) return false;

        var roles = await GetUserRolesAsync(userId.Value, ct);
        if (!IsQuizManagerRole(roles)) return false;

        if (!roles.Any(role => string.Equals(role, LmsRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var userIdStr = userId.Value.ToString();
        return quiz.CreatedBy == userId.Value ||
               await _context.CourseOfferings.AnyAsync(co => co.Id == quiz.CourseOfferingId && co.LecturerId == userId.Value, ct) ||
               await _context.LectureTimetableSlots.AnyAsync(slot =>
                   slot.CourseOfferingId == quiz.CourseOfferingId &&
                   (slot.LecturerId == userId.Value ||
                    (slot.CoLecturersJson != null && slot.CoLecturersJson.Contains(userIdStr))), ct);
    }

    private async Task<bool> CanAccessAttemptAsync(Guid? userId, QuizAttempt attempt, CancellationToken ct)
    {
        if (!userId.HasValue) return false;
        if (attempt.StudentId == userId.Value) return true;

        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == attempt.QuizId, ct);
        return quiz != null && await CanManageQuizAsync(userId, quiz, ct);
    }

    private async Task<ErrorOr<string>> BuildTargetProgramIdsJsonAsync(Guid courseOfferingId, IEnumerable<Guid>? programIds, CancellationToken ct)
    {
        var ids = (programIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return "[]";
        }

        var courseId = await _context.CourseOfferings
            .Where(offering => offering.Id == courseOfferingId)
            .Select(offering => offering.CourseId)
            .FirstOrDefaultAsync(ct);

        if (courseId == Guid.Empty)
        {
            return Error.NotFound("Quiz.CourseOfferingNotFound", "Course offering not found.");
        }

        var validProgramIds = await _context.CourseOfferings
            .Where(offering => offering.CourseId == courseId && ids.Contains(offering.ProgramId))
            .Select(offering => offering.ProgramId)
            .Distinct()
            .ToListAsync(ct);

        if (validProgramIds.Count != ids.Count)
        {
            return Error.Validation("Quiz.InvalidPrograms", "One or more selected programs do not offer this course.");
        }

        return JsonSerializer.Serialize(ids);
    }

    private async Task<bool> StudentCanAccessQuizProgramAsync(Guid studentId, Quiz quiz, CancellationToken ct)
    {
        var courseId = quiz.CourseOffering?.CourseId ?? await _context.CourseOfferings
            .Where(offering => offering.Id == quiz.CourseOfferingId)
            .Select(offering => offering.CourseId)
            .FirstOrDefaultAsync(ct);

        if (courseId == Guid.Empty)
        {
            return false;
        }

        var targetProgramIds = DeserializeGuidList(quiz.TargetProgramIdsJson);
        if (targetProgramIds.Count == 0)
        {
            return await _context.CourseEnrollments.AnyAsync(enrollment =>
                enrollment.StudentId == studentId &&
                enrollment.Status == "Registered" &&
                enrollment.CourseOffering.CourseId == courseId, ct);
        }

        return await _context.CourseEnrollments.AnyAsync(enrollment =>
            enrollment.StudentId == studentId &&
            enrollment.Status == "Registered" &&
            enrollment.CourseOffering.CourseId == courseId &&
            targetProgramIds.Contains(enrollment.CourseOffering.ProgramId), ct);
    }

    private async Task<int> GetActiveTimeExtensionMinutesAsync(Guid quizId, Guid studentId, DateTime now, CancellationToken ct)
    {
        return await _context.QuizTimeExtensions
            .Where(extension =>
                extension.QuizId == quizId &&
                extension.StudentId == studentId &&
                extension.Status == "Active" &&
                (!extension.EffectiveFrom.HasValue || extension.EffectiveFrom.Value <= now) &&
                (!extension.EffectiveUntil.HasValue || extension.EffectiveUntil.Value >= now))
            .SumAsync(extension => (int?)extension.AdditionalMinutes, ct) ?? 0;
    }

    private async Task<bool> IsAttemptTimeExpiredAsync(Quiz quiz, QuizAttempt attempt, DateTime now, CancellationToken ct)
    {
        if (!quiz.TimeLimitMinutes.HasValue || quiz.TimeLimitMinutes.Value <= 0)
        {
            return false;
        }

        var additionalMinutes = await GetActiveTimeExtensionMinutesAsync(quiz.Id, attempt.StudentId, now, ct);
        var expiresAt = attempt.StartTime.AddMinutes(quiz.TimeLimitMinutes.Value + additionalMinutes).AddSeconds(30);
        return now > expiresAt;
    }

    private async Task<QuizResultDto> BuildQuizResultDtoAsync(QuizAttempt attempt, Quiz quiz, decimal earnedPoints, decimal possiblePoints, Guid? userId, CancellationToken ct)
    {
        var canManageQuiz = await CanManageQuizAsync(userId, quiz, ct);
        var (isVisible, message) = await IsScoreVisibleAsync(quiz, attempt.StudentId, canManageQuiz, DateTime.UtcNow, ct);
        return isVisible
            ? new QuizResultDto(attempt.Id, earnedPoints, possiblePoints, isScoreVisible: true)
            : new QuizResultDto(attempt.Id, 0m, 0m, isScoreVisible: false, visibilityMessage: message);
    }

    private async Task<(bool IsVisible, string? Message)> IsScoreVisibleAsync(Quiz quiz, Guid studentId, bool canManageQuiz, DateTime now, CancellationToken ct)
    {
        if (canManageQuiz) return (true, null);

        var setting = quiz.Setting;
        var visibility = setting?.FeedbackVisibility ?? "Immediate";
        if (string.Equals(visibility, "Immediate", StringComparison.OrdinalIgnoreCase))
            return (true, null);

        if (string.Equals(visibility, "AfterClose", StringComparison.OrdinalIgnoreCase))
        {
            var closeDate = setting?.CloseDateUtc ?? quiz.CloseDateUtc;
            return closeDate.HasValue && now >= closeDate.Value
                ? (true, null)
                : (false, "Your score will be available after the quiz closes.");
        }

        if (string.Equals(visibility, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            var hasReleasedFeedback = await _context.QuizFeedbacks
                .AnyAsync(feedback => feedback.QuizId == quiz.Id && feedback.StudentId == studentId, ct);
            return hasReleasedFeedback
                ? (true, null)
                : (false, "Your score is awaiting manual release.");
        }

        return (false, "Scores are not visible for this quiz.");
    }

    private static List<QuizQuestion> SelectQuestionsForStudent(IEnumerable<QuizQuestion> questions, QuizSetting? settings, Guid studentId)
    {
        var ordered = questions.OrderBy(q => q.OrderIndex).ToList();
        if (settings?.UseRandomPool != true || !settings.PoolSize.HasValue || settings.PoolSize.Value <= 0 || ordered.Count <= settings.PoolSize.Value)
        {
            return ordered;
        }

        return ordered
            .OrderBy(question => StableQuestionSeed(studentId, settings.QuizId, question.Id))
            .Take(settings.PoolSize.Value)
            .OrderBy(question => question.OrderIndex)
            .ToList();
    }

    private static string StableQuestionSeed(Guid studentId, Guid quizId, Guid questionId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{studentId:N}:{quizId:N}:{questionId:N}"));
        return Convert.ToHexString(bytes);
    }

    private QuizDto MapToDto(Quiz quiz, bool includeCorrectAnswers = true) => new QuizDto
    {
        Id = quiz.Id,
        Title = quiz.Title,
        Description = quiz.Description,
        TimeLimitMinutes = quiz.TimeLimitMinutes ?? 0,
        CourseOfferingId = quiz.CourseOfferingId,
        AssessmentCategoryId = quiz.AssessmentCategoryId,
        Status = quiz.Status,
        OpenDateUtc = quiz.OpenDateUtc,
        CloseDateUtc = quiz.CloseDateUtc,
        PassThreshold = quiz.PassThreshold,
        TargetProgramIds = DeserializeGuidList(quiz.TargetProgramIdsJson),
        CreatedAt = quiz.CreatedAt,
        Questions = quiz.Questions?.Select(question => MapQuestionToDto(question, includeCorrectAnswers)).ToList() ?? new List<QuizQuestionDto>()
    };

    private QuizQuestionDto MapQuestionToDto(QuizQuestion q, bool includeCorrectAnswers = true) => new QuizQuestionDto
    {
        Id = q.Id,
        QuestionText = q.QuestionText,
        OrderIndex = q.OrderIndex,
        QuestionType = q.QuestionType,
        Points = q.Points,
        Difficulty = q.Difficulty,
        Category = q.Category,
        Tags = q.Tags,
        Explanation = q.Explanation,
        Options = q.Options?.OrderBy(o => o.DisplayOrder).Select(option => MapOptionToDto(option, includeCorrectAnswers)).ToList() ?? new List<QuestionOptionDto>()
    };

    private QuizWithSettingsDto MapToWithSettingsDto(Quiz quiz, QuizSetting settings, bool includeCorrectAnswers = true, bool includeAccessCode = true, IReadOnlyCollection<QuizQuestion>? questionsOverride = null, int? timeLimitOverrideMinutes = null) => new QuizWithSettingsDto
    {
        Id = quiz.Id,
        Title = quiz.Title,
        Description = quiz.Description,
        TimeLimitMinutes = timeLimitOverrideMinutes ?? quiz.TimeLimitMinutes ?? 0,
        CourseOfferingId = quiz.CourseOfferingId,
        AssessmentCategoryId = quiz.AssessmentCategoryId,
        Status = quiz.Status,
        OpenDateUtc = quiz.OpenDateUtc,
        CloseDateUtc = quiz.CloseDateUtc,
        PassThreshold = quiz.PassThreshold,
        TargetProgramIds = DeserializeGuidList(quiz.TargetProgramIdsJson),
        CreatedAt = quiz.CreatedAt,
        Settings = MapSettingToDto(settings, includeAccessCode),
        Questions = (questionsOverride?.AsEnumerable() ?? quiz.Questions).Select(question => MapQuestionToDto(question, includeCorrectAnswers)).ToList()
    };

    private QuizSettingDto MapSettingToDto(QuizSetting settings, bool includeAccessCode = true) => new QuizSettingDto
    {
        Id = settings.Id,
        QuizId = settings.QuizId,
        ShuffleQuestions = settings.ShuffleQuestions,
        ShuffleOptions = settings.ShuffleOptions,
        MaxAttempts = settings.MaxAttempts,
        AllowPartialCredit = settings.AllowPartialCredit,
        ScoreBestAttempt = settings.ScoreBestAttempt,
        OpenDateUtc = settings.OpenDateUtc,
        CloseDateUtc = settings.CloseDateUtc,
        Status = settings.Status,
        PassThreshold = settings.PassThreshold,
        UseRandomPool = settings.UseRandomPool,
        PoolSize = settings.PoolSize,
        PoolQuestionBankId = settings.PoolQuestionBankId,
        FeedbackVisibility = settings.FeedbackVisibility,
        RequireFullscreen = settings.RequireFullscreen,
        AllowTabSwitchDetection = settings.AllowTabSwitchDetection,
        MaxTabSwitches = settings.MaxTabSwitches,
        AccessCode = includeAccessCode ? settings.AccessCode : (string.IsNullOrWhiteSpace(settings.AccessCode) ? null : "***"),
        RestrictToAllowedIps = settings.RestrictToAllowedIps,
        AllowedIpRanges = DeserializeStringList(settings.AllowedIpRangesJson),
        AllowedCbtHallIds = DeserializeGuidList(settings.AllowedCbtHallIdsJson),
        CreatedAt = settings.CreatedAt,
        CreatedBy = settings.CreatedBy,
        UpdatedAt = settings.UpdatedAt,
        UpdatedBy = settings.UpdatedBy
    };

    private async Task<bool> IsClientIpAllowedForQuizAsync(QuizSetting settings, IReadOnlyCollection<string>? clientIps, CancellationToken ct)
    {
        var parsedClientIps = (clientIps ?? Array.Empty<string>())
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Select(ip => IPAddress.TryParse(ip, out var parsedIp) ? parsedIp : null)
            .Where(ip => ip is not null)
            .Cast<IPAddress>()
            .Distinct()
            .ToList();

        if (parsedClientIps.Count == 0)
        {
            return false;
        }

        var allowedRanges = DeserializeStringList(settings.AllowedIpRangesJson);
        var selectedHallIds = DeserializeGuidList(settings.AllowedCbtHallIdsJson);
        if (selectedHallIds.Count > 0)
        {
            var hallRangeJson = await _context.CbtHalls
                .Where(hall => hall.IsActive && selectedHallIds.Contains(hall.Id))
                .Select(hall => hall.IpRangesJson)
                .ToListAsync(ct);

            foreach (var json in hallRangeJson)
            {
                allowedRanges.AddRange(DeserializeStringList(json));
            }
        }

        return parsedClientIps.Any(clientIp => IpRangeMatcher.MatchesAny(clientIp, allowedRanges));
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static List<Guid> DeserializeGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? new List<Guid>();
        }
        catch (JsonException)
        {
            return new List<Guid>();
        }
    }

    // ==================== Question Option Management ====================

    public async Task<ErrorOr<QuestionOptionDto>> AddOptionToQuestionAsync(Guid questionId, string optionText, int displayOrder, CancellationToken ct = default)
    {
        var question = await _context.QuizQuestions.FindAsync(questionId, ct);
        if (question == null) return Error.NotFound("Question.NotFound", "Quiz question not found");

        var maxOrder = await _context.QuestionOptions
            .Where(o => o.QuizQuestionId == questionId)
            .MaxAsync(o => (int?)o.DisplayOrder, ct);
        
        var option = new QuestionOption
        {
            QuizQuestionId = questionId,
            OptionText = optionText,
            DisplayOrder = displayOrder > 0 ? displayOrder : (maxOrder ?? 0) + 1
        };

        _context.QuestionOptions.Add(option);
        await _context.SaveChangesAsync(ct);
        
        return MapOptionToDto(option);
    }

    public async Task<ErrorOr<QuestionOptionDto>> UpdateOptionAsync(Guid optionId, string optionText, bool isCorrectAnswer, CancellationToken ct = default)
    {
        var option = await _context.QuestionOptions.FindAsync(optionId, ct);
        if (option == null) return Error.NotFound("Option.NotFound", "Question option not found");

        option.OptionText = optionText;
        option.IsCorrectAnswer = isCorrectAnswer;
        
        await _context.SaveChangesAsync(ct);
        
        return MapOptionToDto(option);
    }

    public async Task<ErrorOr<Deleted>> DeleteOptionAsync(Guid optionId, CancellationToken ct = default)
    {
        var option = await _context.QuestionOptions.FindAsync(optionId, ct);
        if (option == null) return Error.NotFound("Option.NotFound", "Question option not found");

        _context.QuestionOptions.Remove(option);
        await _context.SaveChangesAsync(ct);
        
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> SetCorrectOptionAsync(Guid questionId, Guid optionId, CancellationToken ct = default)
    {
        var option = await _context.QuestionOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.QuizQuestionId == questionId, ct);
        
        if (option == null) return Error.NotFound("Option.NotFound", "Option not found for this question");

        var question = await _context.QuizQuestions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.Id == questionId, ct);
        
        if (question == null) return Error.NotFound("Question.NotFound", "Quiz question not found");

        foreach (var opt in question.Options)
        {
            opt.IsCorrectAnswer = opt.Id == optionId;
        }
        
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    // ==================== Quiz Analytics ====================

    public async Task<ErrorOr<List<QuizAttemptWithStudentDto>>> GetQuizAttemptsAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var attempts = await _context.QuizAttempts
            .Include(a => a.Student)
            .Include(a => a.Answers)
            .Where(a => a.QuizId == quizId)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync(ct);

        var questions = await _context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .ToDictionaryAsync(q => q.Id, q => q.QuestionText, ct);

        var options = await _context.QuestionOptions
            .Where(o => _context.QuizQuestions.Any(q => q.Id == o.QuizQuestionId && q.QuizId == quizId))
            .ToDictionaryAsync(o => o.Id, o => new { o.OptionText, o.IsCorrectAnswer }, ct);

        var result = attempts.Select(a => new QuizAttemptWithStudentDto(
            a.Id,
            a.StudentId,
            ((a.Student?.FirstName ?? "") + " " + (a.Student?.LastName ?? "")).Trim() ?? "Unknown",
            a.Student?.OfficialEmail ?? "",
            a.StartTime,
            a.EndTime,
            a.TotalScore,
            a.Answers.Select(ans => new QuizAnswerDetailDto(
                ans.QuestionId,
                questions.GetValueOrDefault(ans.QuestionId, ""),
                ans.SelectedOptionId,
                ans.SelectedOptionId.HasValue ? options.GetValueOrDefault(ans.SelectedOptionId.Value)?.OptionText : null,
                ans.SelectedOptionId.HasValue && options.TryGetValue(ans.SelectedOptionId.Value, out var opt) && opt.IsCorrectAnswer,
                ans.TextAnswer
            )).ToList()
        )).ToList();

        return result;
    }

    public async Task<ErrorOr<QuizStatisticsDto>> GetQuizStatisticsAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        // Load all data in single queries to avoid N+2
        var questions = await _context.QuizQuestions
            .Where(q => q.QuizId == quizId)
            .ToListAsync(ct);

        var attempts = await _context.QuizAttempts
            .Where(a => a.QuizId == quizId)
            .ToListAsync(ct);

        var attemptIds = attempts.Select(a => a.Id).ToList();
        var answers = await _context.QuizAnswers
            .Include(a => a.SelectedOption)
            .Where(a => attemptIds.Contains(a.AttemptId))
            .ToListAsync(ct);

        var totalAttempts = attempts.Count;
        var completedAttempts = attempts.Count(a => a.EndTime.HasValue);
        var completed = attempts.Where(a => a.EndTime.HasValue).ToList();
        var scores = completed.Select(a => a.TotalScore).ToList();
        
        var averageScore = scores.Any() ? scores.Average() : 0m;
        var highestScore = scores.Any() ? scores.Max() : 0m;
        var lowestScore = scores.Any() ? scores.Min() : 0m;
        var passThreshold = quiz.PassThreshold ?? 50m;
        var passRate = completed.Any()
            ? (decimal)completed.Count(a => a.TotalScore >= passThreshold) / completed.Count * 100m
            : 0m;

        var questionPerformance = questions.Select(q =>
        {
            var questionAnswers = answers.Where(a => a.QuestionId == q.Id).ToList();
            var totalAttemptsForQuestion = questionAnswers.Count;
            var correctAnswers = questionAnswers.Count(a => 
                a.SelectedOptionId.HasValue && a.SelectedOptionId.Value != Guid.Empty && 
                a.SelectedOption != null && a.SelectedOption.IsCorrectAnswer);

            return new QuestionPerformanceDto(
                q.Id,
                q.QuestionText,
                correctAnswers,
                totalAttemptsForQuestion,
                totalAttemptsForQuestion > 0 ? (decimal)correctAnswers / totalAttemptsForQuestion * 100m : 0m
            );
        }).ToList();

        return new QuizStatisticsDto(
            quizId,
            quiz.Title,
            totalAttempts,
            completedAttempts,
            Math.Round(averageScore, 2),
            highestScore,
            lowestScore,
            Math.Round(passRate, 2),
            questionPerformance
        );
    }

    private static QuestionOptionDto MapOptionToDto(QuestionOption option, bool includeCorrectAnswer = true) => new QuestionOptionDto
    {
        Id = option.Id,
        OptionText = option.OptionText,
        IsCorrectAnswer = includeCorrectAnswer && option.IsCorrectAnswer,
        DisplayOrder = option.DisplayOrder
    };

    // ==================== Quiz Feedback ====================

    public async Task<ErrorOr<List<QuizFeedbackDto>>> GetQuizFeedbacksAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var feedbacks = await _context.QuizFeedbacks
            .Include(f => f.Student)
            .Include(f => f.Question)
            .Where(f => f.QuizId == quizId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return feedbacks.Select(f => new QuizFeedbackDto
        {
            Id = f.Id,
            QuizId = f.QuizId,
            QuestionId = f.QuestionId,
            QuestionText = f.Question?.QuestionText,
            StudentId = f.StudentId,
            StudentName = $"{f.Student?.FirstName ?? ""} {f.Student?.LastName ?? ""}".Trim(),
            FeedbackText = f.FeedbackText,
            FeedbackType = f.FeedbackType,
            GradingNotes = f.GradingNotes,
            ManualOverrideScore = f.ManualOverrideScore,
            CreatedAt = f.CreatedAt
        }).ToList();
    }

    public async Task<ErrorOr<QuizFeedbackDto>> CreateQuizFeedbackAsync(Guid quizId, Guid studentId, Guid? questionId, string feedbackText, string feedbackType, string? gradingNotes, decimal? manualOverrideScore, Guid createdBy, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null) return Error.NotFound("Student.NotFound", "Student not found");

        var feedback = new QuizFeedback
        {
            QuizId = quizId,
            QuestionId = questionId,
            StudentId = studentId,
            FeedbackText = feedbackText,
            FeedbackType = feedbackType,
            GradingNotes = gradingNotes,
            ManualOverrideScore = manualOverrideScore,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.QuizFeedbacks.Add(feedback);
        await _context.SaveChangesAsync(ct);

        return new QuizFeedbackDto
        {
            Id = feedback.Id,
            QuizId = feedback.QuizId,
            QuestionId = feedback.QuestionId,
            QuestionText = feedback.Question?.QuestionText,
            StudentId = feedback.StudentId,
            StudentName = $"{feedback.Student?.FirstName ?? ""} {feedback.Student?.LastName ?? ""}".Trim(),
            FeedbackText = feedback.FeedbackText,
            FeedbackType = feedback.FeedbackType,
            GradingNotes = feedback.GradingNotes,
            ManualOverrideScore = feedback.ManualOverrideScore,
            CreatedAt = feedback.CreatedAt
        };
    }

    public async Task<ErrorOr<Deleted>> UpdateQuizFeedbackAsync(Guid feedbackId, string? feedbackText, string? gradingNotes, decimal? manualOverrideScore, Guid updatedBy, CancellationToken ct = default)
    {
        var feedback = await _context.QuizFeedbacks.FirstOrDefaultAsync(f => f.Id == feedbackId, ct);
        if (feedback == null) return Error.NotFound("Feedback.NotFound", "Quiz feedback not found");

        if (!string.IsNullOrEmpty(feedbackText)) feedback.FeedbackText = feedbackText;
        if (gradingNotes != null) feedback.GradingNotes = gradingNotes;
        if (manualOverrideScore.HasValue) feedback.ManualOverrideScore = manualOverrideScore.Value;
        feedback.UpdatedAt = DateTime.UtcNow;
        feedback.UpdatedBy = updatedBy;

        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    public async Task<ErrorOr<Deleted>> DeleteQuizFeedbackAsync(Guid feedbackId, CancellationToken ct = default)
    {
        var feedback = await _context.QuizFeedbacks.FirstOrDefaultAsync(f => f.Id == feedbackId, ct);
        if (feedback == null) return Error.NotFound("Feedback.NotFound", "Quiz feedback not found");

        _context.QuizFeedbacks.Remove(feedback);
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    // ==================== Time Extension ====================

    public async Task<ErrorOr<List<TimeExtensionDto>>> GetQuizTimeExtensionsAsync(Guid quizId, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var extensions = await _context.QuizTimeExtensions
            .Include(te => te.Student)
            .Where(te => te.QuizId == quizId)
            .OrderByDescending(te => te.CreatedAt)
            .ToListAsync(ct);

        return extensions.Select(te => new TimeExtensionDto
        {
            Id = te.Id,
            QuizId = te.QuizId,
            StudentId = te.StudentId,
            StudentName = $"{te.Student?.FirstName ?? ""} {te.Student?.LastName ?? ""}".Trim(),
            StudentEmail = te.Student?.OfficialEmail ?? "",
            AdditionalMinutes = te.AdditionalMinutes,
            EffectiveFrom = te.EffectiveFrom,
            EffectiveUntil = te.EffectiveUntil,
            Status = te.Status,
            ApprovedBy = te.ApprovedBy,
            ApprovedAt = te.ApprovedAt,
            Reason = te.Reason,
            CreatedAt = te.CreatedAt
        }).ToList();
    }

    public async Task<ErrorOr<TimeExtensionDto>> CreateTimeExtensionAsync(Guid quizId, Guid studentId, int additionalMinutes, DateTime? effectiveFrom, DateTime? effectiveUntil, string reason, string approvedBy, CancellationToken ct = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct);
        if (quiz == null) return Error.NotFound("Quiz.NotFound", "Quiz not found");

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null) return Error.NotFound("Student.NotFound", "Student not found");

        // Check if extension already exists for this quiz+student
        var existing = await _context.QuizTimeExtensions
            .FirstOrDefaultAsync(te => te.QuizId == quizId && te.StudentId == studentId, ct);
        if (existing != null)
        {
            return Error.Validation("DuplicateExtension", $"Time extension already exists for this student on this quiz.");
        }

        var timeExtension = new QuizTimeExtension
        {
            QuizId = quizId,
            StudentId = studentId,
            AdditionalMinutes = additionalMinutes,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            Status = "Active",
            ApprovedBy = approvedBy,
            ApprovedAt = DateTime.UtcNow,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.QuizTimeExtensions.Add(timeExtension);
        await _context.SaveChangesAsync(ct);

        return new TimeExtensionDto
        {
            Id = timeExtension.Id,
            QuizId = timeExtension.QuizId,
            StudentId = timeExtension.StudentId,
            StudentName = $"{timeExtension.Student?.FirstName ?? ""} {timeExtension.Student?.LastName ?? ""}".Trim(),
            StudentEmail = timeExtension.Student?.OfficialEmail ?? "",
            AdditionalMinutes = timeExtension.AdditionalMinutes,
            EffectiveFrom = timeExtension.EffectiveFrom,
            EffectiveUntil = timeExtension.EffectiveUntil,
            Status = timeExtension.Status,
            ApprovedBy = timeExtension.ApprovedBy,
            ApprovedAt = timeExtension.ApprovedAt,
            Reason = timeExtension.Reason,
            CreatedAt = timeExtension.CreatedAt
        };
    }

    public async Task<ErrorOr<Deleted>> RevokeTimeExtensionAsync(Guid timeExtensionId, CancellationToken ct = default)
    {
        var timeExtension = await _context.QuizTimeExtensions.FirstOrDefaultAsync(te => te.Id == timeExtensionId, ct);
        if (timeExtension == null) return Error.NotFound("TimeExtension.NotFound", "Time extension not found");

        timeExtension.Status = "Revoked";
        await _context.SaveChangesAsync(ct);
        return Result.Deleted;
    }

    private static string? ValidateImportedQuestion(QuestionImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.QuestionText))
        {
            return "Question text is required.";
        }

        var optionTypes = new[] { "MultipleChoice", "SingleChoice", "TrueFalse", "MCQ" };
        if (optionTypes.Contains(row.QuestionType, StringComparer.OrdinalIgnoreCase))
        {
            if (row.Options.Count < 2)
            {
                return "At least two options are required for choice questions.";
            }

            if (!row.Options.Any(option => option.IsCorrect))
            {
                return "At least one correct option is required for choice questions.";
            }
        }

        return null;
    }
}
