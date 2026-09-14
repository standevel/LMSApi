using System;
using System.Collections.Generic;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public record CalculatedAssessmentItem(
    Guid AssessmentId,
    string CategoryName,
    string Title,
    decimal MarksObtained,
    decimal MaxMarks,
    decimal CategoryWeight,
    decimal WeightedScore);

public record CalculatedStudentGrade(
    Guid StudentId,
    decimal? Ca1Score,
    decimal? Ca2Score,
    decimal? Ca3Score,
    decimal? ExamScore,
    decimal TotalScore,
    string LetterGrade,
    decimal GradePoints,
    List<CalculatedAssessmentItem> AssessmentItems);

public interface IGradeCalculationEngine
{
    CalculatedStudentGrade CalculateStudentGrade(
        Guid studentId,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<AssessmentCategory> categories,
        IReadOnlyCollection<Grade> studentGrades,
        SystemGradingConfiguration sysConfig);

    Dictionary<Guid, CalculatedStudentGrade> CalculateCourseGrades(
        IReadOnlyCollection<Guid> studentIds,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<AssessmentCategory> categories,
        IReadOnlyCollection<Grade> allGrades,
        SystemGradingConfiguration sysConfig);
}
