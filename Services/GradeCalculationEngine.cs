using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LMS.Api.Contracts;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public class GradeCalculationEngine : IGradeCalculationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CalculatedStudentGrade CalculateStudentGrade(
        Guid studentId,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<AssessmentCategory> categories,
        IReadOnlyCollection<Grade> studentGrades,
        SystemGradingConfiguration sysConfig)
    {
        var mappings = DeserializeMappings(sysConfig.LetterGradesMappingJson);
        // Defensive deduplication: if categories contain duplicate entries for the same CategoryType,
        // retain the canonical category (preferring the one that has assessments with grades).
        var distinctCategories = categories
            .GroupBy(c => c.CategoryType)
            .Select(g => g.Count() == 1
                ? g.First()
                : g.OrderByDescending(c => assessments.Any(a => a.AssessmentCategoryId == c.Id && studentGrades.Any(gr => gr.AssessmentId == a.Id)))
                   .ThenByDescending(c => assessments.Count(a => a.AssessmentCategoryId == c.Id))
                   .ThenBy(c => c.DisplayOrder)
                   .First())
            .ToList();

        var resolvedCategoryWeights = ResolveCategoryWeights(distinctCategories, sysConfig);

        var assessmentItems = new List<CalculatedAssessmentItem>();
        decimal? ca1Score = null;
        decimal? ca2Score = null;
        decimal? ca3Score = null;
        decimal? examScore = null;

        decimal weightedTotal = 0m;
        decimal simpleSumTotal = 0m;
        var isUnweighted = sysConfig.DefaultGradingStyle == GradingStyle.Unweighted;

        foreach (var category in distinctCategories.OrderBy(c => c.DisplayOrder))
        {
            var categoryAssessments = assessments
                .Where(a => a.AssessmentCategoryId == category.Id)
                .ToList();

            if (!categoryAssessments.Any())
            {
                SetCategoryScore(category.CategoryType, 0m);
                continue;
            }

            var categoryWeight = resolvedCategoryWeights.GetValueOrDefault(category.Id, 0m);
            decimal categoryTotalMarks = 0m;
            decimal categoryTotalMaxMarks = 0m;

            // First pass: compute total marks and total max marks for the category
            foreach (var assessment in categoryAssessments)
            {
                var grade = studentGrades.FirstOrDefault(g => g.AssessmentId == assessment.Id);
                var marks = grade?.MarksObtained ?? 0m;
                categoryTotalMarks += marks;
                categoryTotalMaxMarks += assessment.MaxMarks;
            }

            simpleSumTotal += categoryTotalMarks;

            decimal categoryPercentage = categoryTotalMaxMarks > 0m
                ? (categoryTotalMarks / categoryTotalMaxMarks) * 100m
                : 0m;

            decimal categoryContribution = sysConfig.RoundingStrategy == RoundingStrategy.Ceiling
                ? Math.Clamp(GradeCalculator.RoundScore(categoryPercentage * categoryWeight / 100m, sysConfig.RoundingStrategy, sysConfig.RoundingDecimalPlaces), 0m, category.MaxMarks)
                : Math.Round(categoryPercentage * categoryWeight / 100m, 2);

            SetCategoryScore(
                category.CategoryType,
                isUnweighted
                    ? (sysConfig.RoundingStrategy == RoundingStrategy.Ceiling
                        ? Math.Clamp(GradeCalculator.RoundScore(categoryTotalMarks, sysConfig.RoundingStrategy, sysConfig.RoundingDecimalPlaces), 0m, category.MaxMarks)
                        : Math.Round(categoryTotalMarks, 2))
                    : categoryContribution);

            // Second pass: compute assessment-level items
            foreach (var assessment in categoryAssessments)
            {
                var grade = studentGrades.FirstOrDefault(g => g.AssessmentId == assessment.Id);
                var marks = grade?.MarksObtained ?? 0m;

                decimal assessmentEffectiveWeight;
                decimal weightedScore;

                if (isUnweighted)
                {
                    assessmentEffectiveWeight = assessment.MaxMarks;
                    weightedScore = marks;
                }
                else
                {
                    assessmentEffectiveWeight = categoryTotalMaxMarks > 0m
                        ? (assessment.MaxMarks / categoryTotalMaxMarks) * categoryWeight
                        : 0m;

                    weightedScore = assessment.MaxMarks > 0m
                        ? (marks / assessment.MaxMarks) * assessmentEffectiveWeight
                        : 0m;
                }

                assessmentItems.Add(new CalculatedAssessmentItem(
                    assessment.Id,
                    category.CategoryName,
                    assessment.Title,
                    marks,
                    assessment.MaxMarks,
                    Math.Round(assessmentEffectiveWeight, 2),
                    Math.Round(weightedScore, 2)));
            }

            weightedTotal += categoryPercentage * categoryWeight / 100m;
        }

        decimal rawScore = Math.Clamp(
            isUnweighted ? simpleSumTotal : weightedTotal,
            0m,
            100m);

        var gradeResult = GradeCalculator.CalculateGrade(
            rawScore,
            sysConfig.RoundingStrategy,
            sysConfig.RoundingDecimalPlaces,
            sysConfig.GraceThreshold,
            mappings);

        return new CalculatedStudentGrade(
            studentId,
            ca1Score,
            ca2Score,
            ca3Score,
            examScore,
            gradeResult.Score,
            gradeResult.LetterGrade,
            gradeResult.GradePoints,
            assessmentItems);

        void SetCategoryScore(AssessmentCategoryType type, decimal score)
        {
            switch (type)
            {
                case AssessmentCategoryType.CA1:
                    ca1Score = score;
                    break;
                case AssessmentCategoryType.CA2:
                    ca2Score = score;
                    break;
                case AssessmentCategoryType.CA3:
                    ca3Score = score;
                    break;
                case AssessmentCategoryType.Exam:
                    examScore = score;
                    break;
            }
        }
    }

    public Dictionary<Guid, CalculatedStudentGrade> CalculateCourseGrades(
        IReadOnlyCollection<Guid> studentIds,
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<AssessmentCategory> categories,
        IReadOnlyCollection<Grade> allGrades,
        SystemGradingConfiguration sysConfig)
    {
        var results = new Dictionary<Guid, CalculatedStudentGrade>(studentIds.Count);

        // Group grades by student for O(1) lookup
        var gradesByStudent = allGrades
            .GroupBy(g => g.StudentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<Grade>)g.ToList());

        foreach (var studentId in studentIds)
        {
            var studentGrades = gradesByStudent.TryGetValue(studentId, out var grades)
                ? grades
                : Array.Empty<Grade>();

            results[studentId] = CalculateStudentGrade(
                studentId,
                assessments,
                categories,
                studentGrades,
                sysConfig);
        }

        return results;
    }

    private static Dictionary<Guid, decimal> ResolveCategoryWeights(
        IReadOnlyCollection<AssessmentCategory> categories,
        SystemGradingConfiguration sysConfig)
    {
        var result = new Dictionary<Guid, decimal>();
        var totalConfiguredWeight = categories.Sum(c => c.Weight);

        bool useCourseCategoryWeights = totalConfiguredWeight > 0m;

        foreach (var cat in categories)
        {
            if (useCourseCategoryWeights)
            {
                result[cat.Id] = cat.Weight;
            }
            else
            {
                result[cat.Id] = cat.CategoryType switch
                {
                    AssessmentCategoryType.CA1 => sysConfig.DefaultCA1Weight,
                    AssessmentCategoryType.CA2 => sysConfig.DefaultCA2Weight,
                    AssessmentCategoryType.CA3 => sysConfig.DefaultCA3Weight,
                    AssessmentCategoryType.Exam => sysConfig.DefaultExamWeight,
                    _ => cat.IsExamCategory ? sysConfig.DefaultExamWeight : 0m
                };
            }
        }

        return result;
    }

    private static List<GradeMappingDto> DeserializeMappings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<GradeMappingDto>();

        try
        {
            return JsonSerializer.Deserialize<List<GradeMappingDto>>(json, JsonOptions) ?? new List<GradeMappingDto>();
        }
        catch
        {
            return new List<GradeMappingDto>();
        }
    }
}
