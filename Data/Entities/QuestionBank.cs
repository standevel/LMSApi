using System;
using System.Collections.Generic;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities;

public class QuestionBank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? CourseOfferingId { get; set; }
    public CourseOffering? CourseOffering { get; set; }
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}
