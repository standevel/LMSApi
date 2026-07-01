using System;
using System.Collections.Generic;
using ErrorOr;

namespace LMS.Api.Contracts;

public class QuizDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public Guid CourseOfferingId { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public decimal? PassThreshold { get; set; }
    public List<Guid> TargetProgramIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public ICollection<QuizQuestionDto> Questions { get; set; } = new List<QuizQuestionDto>();
}

public class CreateQuizRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public Guid CourseOfferingId { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    
    // Phase 1+2: Quiz settings
    public string? Status { get; set; } = "Draft"; // Draft, Scheduled, Published, Archived, Closed
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public decimal? PassThreshold { get; set; }
    public List<Guid> TargetProgramIds { get; set; } = new();
    
    // Shuffle settings
    public bool ShuffleQuestions { get; set; } = false;
    public bool ShuffleOptions { get; set; } = false;
    
    // Attempt control
    public int MaxAttempts { get; set; } = 1;
    public bool AllowPartialCredit { get; set; } = true;
    public bool ScoreBestAttempt { get; set; } = false;
    
    // Feedback control
    public string? FeedbackVisibility { get; set; } = "Immediate"; // Immediate, AfterClose, Manual, Never
    
    // Random pool
    public bool UseRandomPool { get; set; } = false;
    public int? PoolSize { get; set; }
    public Guid? PoolQuestionBankId { get; set; }
    
    // Access control
    public string? AccessCode { get; set; }
    
    // Exam integrity
    public bool RequireFullscreen { get; set; } = false;
    public bool AllowTabSwitchDetection { get; set; } = true;
    public int MaxTabSwitches { get; set; } = 3;
}

public class UpdateQuizRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    public List<Guid>? TargetProgramIds { get; set; }
}

public class QuizQuestionDto
{
    public Guid Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public int Points { get; set; } = 1;
    public string? Difficulty { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public string? Explanation { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new List<QuestionOptionDto>();
}

public class QuestionOptionDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrectAnswer { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateQuizQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public int Points { get; set; } = 1;
    public string? Difficulty { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public string? Explanation { get; set; }
    public List<string> OptionTexts { get; set; } = new List<string>();
}

public class UpdateQuizQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
}

public class QuizAnswerDto
{
    public Guid QuestionId { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public string TextAnswer { get; set; } = string.Empty;
}

public class StartQuizAttemptRequest
{
    public string? AccessCode { get; set; }
}

public class QuizAttemptDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string QuizId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal TotalScore { get; set; }
    public List<QuizAnswerDetailDto> Answers { get; set; } = new();
    public QuizAttemptDto(string attemptId, string studentId, string quizId, DateTime startTime, DateTime? endTime, decimal totalScore, List<QuizAnswerDetailDto> answers)
    {
        AttemptId = attemptId;
        StudentId = studentId;
        QuizId = quizId;
        StartTime = startTime;
        EndTime = endTime;
        TotalScore = totalScore;
        Answers = answers;
    }
}

public class QuizResultDto
{
    public Guid AttemptId { get; set; }
    public decimal FinalScore { get; set; }
    public decimal MaxPossibleScore { get; set; }
    public bool IsScoreVisible { get; set; } = true;
    public string? VisibilityMessage { get; set; }
    public QuizResultDto(Guid attemptId, decimal finalScore, decimal maxPossibleScore, bool isScoreVisible = true, string? visibilityMessage = null)
    {
        AttemptId = attemptId;
        FinalScore = finalScore;
        MaxPossibleScore = maxPossibleScore;
        IsScoreVisible = isScoreVisible;
        VisibilityMessage = visibilityMessage;
    }
}

public class QuestionBankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? CourseOfferingId { get; set; }
    public int ItemCount { get; set; }
    public List<QuestionBankItemDto> Items { get; set; } = new();
    public QuestionBankDto(Guid id, string name, string description, Guid? courseOfferingId)
    {
        Id = id;
        Name = name;
        Description = description;
        CourseOfferingId = courseOfferingId;
    }
}

public class CreateQuestionBankRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? CourseOfferingId { get; set; }
}

public class ExamProctoringSessionDto
{
    public string Id { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid QuizId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ViolationCount { get; set; }
    public int TabSwitchCount { get; set; }
    public int FullscreenLossCount { get; set; }
    public decimal IntegrityScore { get; set; } = 100m;
    public bool CameraPermissionGranted { get; set; }
    public string? SelfieCaptureUrl { get; set; }
    public string? BrowserInfo { get; set; }
    public string? ScreenResolution { get; set; }
    public string? IPAddress { get; set; }
    public string? StudentName { get; set; }
    public string? StudentEmail { get; set; }
    public string? QuizTitle { get; set; }
    public List<ProctoringViolationDto> Violations { get; set; } = new();

    public ExamProctoringSessionDto(string id, DateTime startTimeUtc, string status)
    {
        Id = id;
        StartTimeUtc = startTimeUtc;
        Status = status;
    }
    public ExamProctoringSessionDto(string id, DateTime startTimeUtc, DateTime endTimeUtc, string status)
    {
        Id = id;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        Status = status;
    }
}

public class ProctoringHeartbeatRequest
{
    public Guid SessionId { get; set; }
    public DateTime HeartbeatTimeUtc { get; set; }
    public string UserIPAddress { get; set; } = string.Empty;
}

public class CreateQuestionOptionRequest
{
    public string OptionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class UpdateQuestionOptionRequest
{
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrectAnswer { get; set; }
}

public record QuizAttemptWithStudentDto(
    Guid AttemptId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    DateTime StartTime,
    DateTime? EndTime,
    decimal TotalScore,
    List<QuizAnswerDetailDto> Answers);

public record QuizAnswerDetailDto(
    Guid QuestionId,
    string QuestionText,
    Guid? SelectedOptionId,
    string? SelectedOptionText,
    bool IsCorrect,
    string? TextAnswer);

public record QuestionPerformanceDto(
    Guid QuestionId,
    string QuestionText,
    int CorrectAnswersCount,
    int TotalAttemptsForQuestion,
    decimal SuccessRate);

public record QuizStatisticsDto(
    Guid QuizId,
    string QuizTitle,
    int TotalAttempts,
    int CompletedAttempts,
    decimal AverageScore,
    decimal HighestScore,
    decimal LowestScore,
    decimal PassRate,
    List<QuestionPerformanceDto> QuestionPerformance);

// ===== Phase 1+2: Enhanced Quiz Management DTOs =====

// Quiz Settings DTOs
public class QuizSettingDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public int MaxAttempts { get; set; }
    public bool AllowPartialCredit { get; set; }
    public bool ScoreBestAttempt { get; set; }
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal? PassThreshold { get; set; }
    public bool UseRandomPool { get; set; }
    public int? PoolSize { get; set; }
    public Guid? PoolQuestionBankId { get; set; }
    public string FeedbackVisibility { get; set; } = "Immediate";
    public bool RequireFullscreen { get; set; }
    public bool AllowTabSwitchDetection { get; set; }
    public int MaxTabSwitches { get; set; }
    public string? AccessCode { get; set; }
    public bool RestrictToAllowedIps { get; set; }
    public List<string> AllowedIpRanges { get; set; } = new();
    public List<Guid> AllowedCbtHallIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class UpdateQuizSettingRequest
{
    public bool? ShuffleQuestions { get; set; }
    public bool? ShuffleOptions { get; set; }
    public int? MaxAttempts { get; set; }
    public bool? AllowPartialCredit { get; set; }
    public bool? ScoreBestAttempt { get; set; }
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public string? Status { get; set; }
    public decimal? PassThreshold { get; set; }
    public bool? UseRandomPool { get; set; }
    public int? PoolSize { get; set; }
    public Guid? PoolQuestionBankId { get; set; }
    public string? FeedbackVisibility { get; set; }
    public bool? RequireFullscreen { get; set; }
    public bool? AllowTabSwitchDetection { get; set; }
    public int? MaxTabSwitches { get; set; }
    public string? AccessCode { get; set; }
    public bool? RestrictToAllowedIps { get; set; }
    public List<string>? AllowedIpRanges { get; set; }
    public List<Guid>? AllowedCbtHallIds { get; set; }
}

public class CbtHallDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> IpRanges { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class CreateCbtHallRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> IpRanges { get; set; } = new();
}

public class UpdateCbtHallRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> IpRanges { get; set; } = new();
}

public class UpdateCbtHallStatusRequest
{
    public bool IsActive { get; set; }
}

// Question Bank Item DTOs
public class QuestionBankItemDto
{
    public Guid Id { get; set; }
    public Guid QuestionBankId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "MCQ";
    public int? Points { get; set; }
    public List<QuestionBankOptionDto> Options { get; set; } = new();
    public string? CorrectAnswer { get; set; }
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
    public string? Tags { get; set; }
    public string? Explanation { get; set; }
    public string? Feedback { get; set; }
    public int TimesUsed { get; set; }
    public decimal AverageScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuestionBankOptionDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsCorrectAnswer { get; set; }
}

public class CreateQuestionBankItemRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "MCQ";
    public int? Points { get; set; }
    public List<CreateQuestionBankOptionRequest> Options { get; set; } = new();
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
    public string? Tags { get; set; }
    public string? Explanation { get; set; }
    public string? Feedback { get; set; }
}

public class CreateQuestionBankOptionRequest
{
    public string OptionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsCorrectAnswer { get; set; }
}

public class UpdateQuestionBankItemRequest
{
    public string? QuestionText { get; set; }
    public string? QuestionType { get; set; }
    public int? Points { get; set; }
    public List<UpdateQuestionBankOptionRequest>? Options { get; set; }
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
    public string? Tags { get; set; }
    public string? Explanation { get; set; }
    public string? Feedback { get; set; }
}

public class UpdateQuestionBankOptionRequest
{
    public Guid OptionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrectAnswer { get; set; }
}

public class AddQuestionsFromBankRequest
{
    public Guid QuestionBankId { get; set; }
    public List<Guid> QuestionBankItemIds { get; set; } = new();
    public bool RandomizeSelection { get; set; }
    public int? Limit { get; set; }
}

// Time Extension DTOs
public class TimeExtensionDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public int AdditionalMinutes { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string Status { get; set; } = "Active";
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTimeExtensionRequest
{
    public Guid StudentId { get; set; }
    public int AdditionalMinutes { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public string? Reason { get; set; }
}

// Quiz Feedback DTOs
public class QuizFeedbackDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public Guid? QuestionId { get; set; }
    public string? QuestionText { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string FeedbackText { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = "General";
    public string? GradingNotes { get; set; }
    public decimal? ManualOverrideScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateQuizFeedbackRequest
{
    public Guid StudentId { get; set; }
    public Guid? QuestionId { get; set; }
    public string FeedbackText { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = "General";
    public string? GradingNotes { get; set; }
    public decimal? ManualOverrideScore { get; set; }
}

// Quiz with settings DTO
public class QuizWithSettingsDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public Guid CourseOfferingId { get; set; }
    public Guid? AssessmentCategoryId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? OpenDateUtc { get; set; }
    public DateTime? CloseDateUtc { get; set; }
    public decimal? PassThreshold { get; set; }
    public List<Guid> TargetProgramIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public QuizSettingDto? Settings { get; set; }
    public List<QuizQuestionDto> Questions { get; set; } = new();
}

// Student quiz start DTO
public class StudentQuizStartDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public int? ExtendedTimeMinutes { get; set; }
    public int TotalTimeMinutes { get; set; }
    public DateTime OpenDateUtc { get; set; }
    public DateTime CloseDateUtc { get; set; }
    public string Status { get; set; } = "Draft";
    public bool CanAttempt { get; set; }
    public string? AttemptMessage { get; set; }
    public int CurrentAttemptNumber { get; set; }
    public int MaxAttempts { get; set; }
    public bool HasActiveAttempt { get; set; }
    public string? AccessCodeRequired { get; set; }
}

// ==================== Proctoring DTOs ====================

public class ProctoringSessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentEmail { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizTitle { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public string Status { get; set; } = "Active";
    public int ViolationCount { get; set; }
    public int TabSwitchCount { get; set; }
    public int FullscreenLossCount { get; set; }
    public decimal IntegrityScore { get; set; }
    public bool CameraPermissionGranted { get; set; }
    public string? SelfieCaptureUrl { get; set; }
    public string? BrowserInfo { get; set; }
    public string? ScreenResolution { get; set; }
    public string? IPAddress { get; set; }
    public int TotalViolations { get; set; }
    public List<ProctoringViolationDto> Violations { get; set; } = new();
}

public class ProctoringViolationDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ScreenshotUrl { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int Severity { get; set; }
}

public class StartProctoringRequest
{
    public Guid QuizId { get; set; }
    public string? BrowserInfo { get; set; }
    public string? ScreenResolution { get; set; }
    public string? UserAgent { get; set; }
    public string? IPAddress { get; set; }
    public bool CameraPermissionGranted { get; set; }
    public string? SelfieCaptureUrl { get; set; }
}

public class RecordViolationRequest
{
    public Guid SessionId { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ScreenshotUrl { get; set; }
    public int Severity { get; set; } = 1;
}

public class UpdateProctoringHeartbeatRequest
{
    public Guid SessionId { get; set; }
    public DateTime HeartbeatTimeUtc { get; set; }
    public string UserIPAddress { get; set; } = string.Empty;
    public int TabSwitchCount { get; set; }
    public bool IsFullscreen { get; set; } = true;
    public int FullscreenLossCount { get; set; }
}

public class ProctoringLecturerDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int ActiveSessions { get; set; }
    public int CompletedSessions { get; set; }
    public List<StudentProctoringSummary> StudentSummaries { get; set; } = new();
}

public class StudentProctoringSummary
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public string? SessionStatus { get; set; }
    public int ViolationCount { get; set; }
    public int TabSwitchCount { get; set; }
    public int FullscreenLossCount { get; set; }
    public decimal IntegrityScore { get; set; }
    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public bool CameraPermissionGranted { get; set; }
    public string? SelfieCaptureUrl { get; set; }
    public bool HasActiveAttempt { get; set; }
}
