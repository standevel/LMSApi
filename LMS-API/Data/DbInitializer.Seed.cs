using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Data;

/// <summary>
/// Additional seed data for international/direct entry/transfer/exchange student support.
/// Partial class that extends DbInitializer.
/// </summary>
public sealed partial class DbInitializer
{
    private async Task SeedCountriesAsync(CancellationToken ct)
    {
        if (await dbContext.Countries.AnyAsync(ct))
        {
            logger.LogInformation("Countries already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Countries...");

        var countries = new (string Code, string Name, string? DisplayName, Region Region, string? CallingCode, int Order)[]
        {
            ("NG", "Nigeria", "Nigeria", Region.Africa, "+234", 1),
            ("GH", "Ghana", "Ghana", Region.Africa, "+233", 2),
            ("KE", "Kenya", "Kenya", Region.Africa, "+254", 3),
            ("TZ", "Tanzania", "Tanzania", Region.Africa, "+255", 4),
            ("ZA", "South Africa", "South Africa", Region.Africa, "+27", 5),
            ("EG", "Egypt", "Egypt", Region.Africa, "+20", 6),
            ("CM", "Cameroon", "Cameroon", Region.Africa, "+237", 7),
            ("SN", "Senegal", "Senegal", Region.Africa, "+221", 8),
            ("CI", "Cote d'Ivoire", "Cote d'Ivoire", Region.Africa, "+225", 9),
            ("UG", "Uganda", "Uganda", Region.Africa, "+256", 10),
            ("US", "United States", "United States of America", Region.Americas, "+1", 11),
            ("CA", "Canada", "Canada", Region.Americas, "+1", 12),
            ("BR", "Brazil", "Brazil", Region.Americas, "+55", 13),
            ("GB", "United Kingdom", "United Kingdom", Region.Europe, "+44", 14),
            ("DE", "Germany", "Deutschland", Region.Europe, "+49", 15),
            ("FR", "France", "France", Region.Europe, "+33", 16),
            ("IN", "India", "India", Region.Asia, "+91", 17),
            ("CN", "China", "China", Region.Asia, "+86", 18),
            ("JP", "Japan", "Japan", Region.Asia, "+81", 19),
            ("AE", "UAE", "United Arab Emirates", Region.MiddleEast, "+971", 20),
            ("SA", "Saudi Arabia", "Saudi Arabia", Region.MiddleEast, "+966", 21),
            ("IL", "Israel", "Israel", Region.MiddleEast, "+972", 22),
            ("AU", "Australia", "Australia", Region.Oceania, "+61", 23),
            ("NZ", "New Zealand", "New Zealand", Region.Oceania, "+64", 24)
        };

        var existingCodes = await dbContext.Countries.AsNoTracking().Select(c => c.Code).ToListAsync(ct);
        var toAdd = new List<Country>();

        foreach (var (Code, Name, DisplayName, RegionCode, CallingCode, Order) in countries)
        {
            if (existingCodes.Contains(Code)) continue;
            toAdd.Add(new Country
            {
                Code = Code,
                Name = Name,
                DisplayName = DisplayName,
                Region = RegionCode,
                CallingCode = CallingCode,
                IsActive = true,
                DisplayOrder = Order
            });
        }

        if (toAdd.Count > 0)
        {
            dbContext.Countries.AddRange(toAdd);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Added {Count} countries", toAdd.Count);
        }
        else
        {
            logger.LogInformation("All countries already seeded. Skipping.");
        }
    }

    private async Task SeedGradingScalesAsync(CancellationToken ct)
    {
        if (await dbContext.GradingScales.AnyAsync(ct))
        {
            logger.LogInformation("Grading scales already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Grading Scales...");

        var aLevelGrades = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { Grade = "A*", MinScore = 75, MaxScore = 100, Points = 4.0 },
            new { Grade = "A", MinScore = 65, MaxScore = 74, Points = 3.5 },
            new { Grade = "B", MinScore = 55, MaxScore = 64, Points = 3.0 },
            new { Grade = "C", MinScore = 45, MaxScore = 54, Points = 2.0 },
            new { Grade = "D", MinScore = 35, MaxScore = 44, Points = 1.0 },
            new { Grade = "E", MinScore = 25, MaxScore = 34, Points = 0.5 },
            new { Grade = "U", MinScore = 0, MaxScore = 24, Points = 0.0 }
        });

        dbContext.GradingScales.AddRange(
            new GradingScale
            {
                Name = "A-Level (Cambridge)",
                CountryCode = "GB",
                QualificationType = "A-Level",
                GradesJson = aLevelGrades,
                IsActive = true
            },
            new GradingScale
            {
                Name = "IJMB",
                CountryCode = "NG",
                QualificationType = "IJMB",
                GradesJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { Grade = "A", MinScore = 70, MaxScore = 100, Points = 3.0 },
                    new { Grade = "B", MinScore = 60, MaxScore = 69, Points = 2.5 },
                    new { Grade = "C", MinScore = 50, MaxScore = 59, Points = 2.0 },
                    new { Grade = "D", MinScore = 40, MaxScore = 49, Points = 1.0 },
                    new { Grade = "E", MinScore = 30, MaxScore = 39, Points = 0.5 },
                    new { Grade = "F", MinScore = 0, MaxScore = 29, Points = 0.0 }
                }),
                IsActive = true
            },
            new GradingScale
            {
                Name = "IB Diploma",
                CountryCode = null,
                QualificationType = "IB",
                GradesJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { Grade = "7", MinScore = 93, MaxScore = 100, Points = 4.0 },
                    new { Grade = "6", MinScore = 86, MaxScore = 92, Points = 3.5 },
                    new { Grade = "5", MinScore = 76, MaxScore = 85, Points = 3.0 },
                    new { Grade = "4", MinScore = 66, MaxScore = 75, Points = 2.0 },
                    new { Grade = "3", MinScore = 56, MaxScore = 65, Points = 1.0 },
                    new { Grade = "2", MinScore = 46, MaxScore = 55, Points = 0.5 },
                    new { Grade = "1", MinScore = 0, MaxScore = 45, Points = 0.0 }
                }),
                IsActive = true
            },
            new GradingScale
            {
                Name = "HND/ND (Nigerian)",
                CountryCode = "NG",
                QualificationType = "HND",
                GradesJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { Grade = "First Class", MinScore = 70, MaxScore = 100, Points = 4.0 },
                    new { Grade = "Second Class Upper", MinScore = 60, MaxScore = 69, Points = 3.5 },
                    new { Grade = "Second Class Lower", MinScore = 50, MaxScore = 59, Points = 3.0 },
                    new { Grade = "Third Class", MinScore = 45, MaxScore = 49, Points = 2.0 },
                    new { Grade = "Pass", MinScore = 40, MaxScore = 44, Points = 1.0 },
                    new { Grade = "Fail", MinScore = 0, MaxScore = 39, Points = 0.0 }
                }),
                IsActive = true
            },
            new GradingScale
            {
                Name = "BTEC",
                CountryCode = "GB",
                QualificationType = "BTEC",
                GradesJson = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { Grade = "Distinction*", MinScore = 80, MaxScore = 100, Points = 4.0 },
                    new { Grade = "Distinction", MinScore = 70, MaxScore = 79, Points = 3.5 },
                    new { Grade = "Merit", MinScore = 60, MaxScore = 69, Points = 3.0 },
                    new { Grade = "Pass", MinScore = 50, MaxScore = 59, Points = 2.0 }
                }),
                IsActive = true
            }
        );

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Added grading scales");
    }

    private async Task SeedCreditTransferRulesAsync(CancellationToken ct)
    {
        if (await dbContext.CreditTransferRules.AnyAsync(ct))
        {
            logger.LogInformation("Credit transfer rules already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Credit Transfer Rules...");

        var programs = await dbContext.Programs.AsNoTracking().ToListAsync(ct);
        if (!programs.Any())
        {
            logger.LogInformation("No programs found. Skipping credit transfer rules.");
            return;
        }

        // Default rule for all programs (null country = applies to all)
        foreach (var program in programs)
        {
            dbContext.CreditTransferRules.Add(new CreditTransferRule
            {
                ProgramId = program.Id,
                SourceCountryCode = null,
                CreditsPerYear = 15m,
                MaxTransferablePercentage = 50m,
                MaxTransferableCredits = 60,
                MinCGPA = 2.5m,
                IsActive = true
            });

            // Country-specific rules for common source countries
            var commonCountries = new[] { "US", "GB", "CA", "NG", "GH", "KE", "IN", "CN", "AE", "SA" };
            foreach (var countryCode in commonCountries)
            {
                dbContext.CreditTransferRules.Add(new CreditTransferRule
                {
                    ProgramId = program.Id,
                    SourceCountryCode = countryCode,
                    CreditsPerYear = 15m,
                    MaxTransferablePercentage = 50m,
                    MaxTransferableCredits = 60,
                    MinCGPA = 2.5m,
                    IsActive = true
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Added credit transfer rules");
    }

    private async Task SeedProgramCreditMappingsAsync(CancellationToken ct)
    {
        if (await dbContext.ProgramCreditMappings.AnyAsync(ct))
        {
            logger.LogInformation("Program credit mappings already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding Program Credit Mappings...");

        var programs = await dbContext.Programs.AsNoTracking().ToListAsync(ct);

        foreach (var program in programs)
        {
            dbContext.ProgramCreditMappings.Add(new ProgramCreditMapping
            {
                ProgramId = program.Id,
                CreditsPerLevel = 30,
                MaxTransferablePercentage = 50m,
                MaxTransferableCredits = 60,
                MinCreditsAtLMS = 60,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Added program credit mappings");
    }
}
