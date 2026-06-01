using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LMS.Api.Data.Entities;

namespace LMS.Api.Data.Entities
{
    public class Quiz
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Column(TypeName = "int?")]
        public int? TimeLimitMinutes { get; set; }

        public Guid CourseOfferingId { get; set; }
        public CourseOffering? CourseOffering { get; set; }

        public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
    }
}
