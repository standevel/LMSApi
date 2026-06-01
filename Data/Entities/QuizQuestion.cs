using System;
using System.Collections.Generic;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz? Quiz { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

