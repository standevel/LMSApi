using System;
using System.Collections.Generic;
using System.Linq;
using LMS.Api.Data.Entities;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public static class GradeCalculator
{
    public static decimal RoundScore(decimal score, RoundingStrategy strategy, int decimalPlaces)
    {
        if (strategy == RoundingStrategy.None)
            return score;

        decimal factor = (decimal)Math.Pow(10, decimalPlaces);
        return strategy switch
        {
            RoundingStrategy.Standard => Math.Round(score, decimalPlaces, MidpointRounding.AwayFromZero),
            RoundingStrategy.Ceiling => Math.Ceiling(score * factor) / factor,
            RoundingStrategy.Floor => Math.Floor(score * factor) / factor,
            _ => Math.Round(score, decimalPlaces, MidpointRounding.AwayFromZero)
        };
    }

    public static (decimal Score, string LetterGrade, decimal GradePoints) CalculateGrade(
        decimal rawScore, 
        RoundingStrategy strategy, 
        int decimalPlaces, 
        decimal graceThreshold, 
        List<GradeMappingDto> mappings)
    {
        // 1. Apply decimal rounding first
        decimal score = RoundScore(rawScore, strategy, decimalPlaces);

        if (mappings == null || !mappings.Any())
        {
            // Fallback default rules: A=70, B=60, C=50, D=45, E=40, F=0
            var defaults = new List<(decimal Min, string Letter, decimal Points)>
            {
                (70m, "A", 5.0m),
                (60m, "B", 4.0m),
                (50m, "C", 3.0m),
                (45m, "D", 2.0m),
                (40m, "E", 1.0m),
                (0m, "F", 0.0m)
            };

            if (graceThreshold > 0)
            {
                foreach (var d in defaults)
                {
                    if (score < d.Min && d.Min - score <= graceThreshold)
                    {
                        score = d.Min;
                        break;
                    }
                }
            }

            var matchedDefault = defaults.FirstOrDefault(x => score >= x.Min);
            return (score, matchedDefault.Letter ?? "F", matchedDefault.Points);
        }

        // Apply grace threshold using custom database mappings
        if (graceThreshold > 0)
        {
            // Sort mappings ascending to find the next higher threshold
            var sortedMappings = mappings.OrderBy(m => m.MinPercentage).ToList();
            foreach (var m in sortedMappings)
            {
                if (score < m.MinPercentage && m.MinPercentage - score <= graceThreshold)
                {
                    score = m.MinPercentage;
                    break;
                }
            }
        }

        var match = mappings.OrderByDescending(m => m.MinPercentage)
            .FirstOrDefault(m => score >= m.MinPercentage);

        return (score, match?.LetterGrade ?? "F", match != null ? match.GradePoints : 0.0m);
    }
}
