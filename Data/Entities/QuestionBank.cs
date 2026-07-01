using System;
using System.Collections.Generic;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class QuestionBank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Shared across courses - null means shared
    public Guid? CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }

    // Question bank items (actual questions stored in the bank)
    public ICollection<QuestionBankItem> Items { get; set; } = new List<QuestionBankItem>();

    // Quiz items that were built from this bank (reference only)
    public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();

    // Metadata
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}