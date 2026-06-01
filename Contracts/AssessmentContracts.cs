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
    public ICollection<QuizQuestionDto> Questions { get; set; } = new List<QuizQuestionDto>();
}

public class CreateQuizRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public Guid CourseOfferingId { get; set; }
}

public class UpdateQuizRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
}

public class QuizQuestionDto
{
    public Guid Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public List<QuestionOptionDto> Options { get; set; } = new List<QuestionOptionDto>();
}

public class QuestionOptionDto
{
    public Guid Id { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrectAnswer { get; set; }
}

public class CreateQuizQuestionRequest
{
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
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

public class QuizAttemptDto
{
    public string AttemptId { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public QuizAttemptDto(string attemptId, DateTime startTimeUtc)
    {
        AttemptId = attemptId;
        StartTimeUtc = startTimeUtc;
    }
}

public class QuizResultDto
{
    public Guid AttemptId { get; set; }
    public decimal FinalScore { get; set; }
    public decimal MaxPossibleScore { get; set; }
    public QuizResultDto(Guid attemptId, decimal finalScore, decimal maxPossibleScore)
    {
        AttemptId = attemptId;
        FinalScore = finalScore;
        MaxPossibleScore = maxPossibleScore;
    }
}

public class QuestionBankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? CourseOfferingId { get; set; }
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
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
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
