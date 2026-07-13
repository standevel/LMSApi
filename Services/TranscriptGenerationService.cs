using ErrorOr;
using LMS.Api.Common.Errors;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LMS.Api.Services;

public class TranscriptGenerationService : BaseService, ITranscriptGenerationService
{
    private readonly LmsDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;

    public TranscriptGenerationService(LmsDbContext dbContext, IAuditService auditService, IFileStorageService fileStorageService) : base(auditService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
    }

    public async Task<ErrorOr<TranscriptDto>> GenerateTranscriptAsync(Guid studentId, bool isOfficial = true, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .Include(x => x.AcademicProgram)
            .Include(x => x.Level)
            .Include(x => x.AdmissionApplication)
            .ThenInclude(a => a!.AcademicSession)
            .Include(x => x.Faculty)
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        // Build course records
        var sysConfig = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();
            
        var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();

        var offerings = await _dbContext.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .Where(co => _dbContext.CourseEnrollments.Any(e => e.StudentId == studentId && e.CourseOfferingId == co.Id && e.Status == "Registered"))
            .Distinct()
            .ToListAsync(ct);

        var courseRecords = new List<TranscriptCourseRecord>();
        foreach (var offering in offerings)
        {
            // Fetch assessments for this course offering
            var assessments = await _dbContext.Assessments
                .Where(a => a.CourseOfferingId == offering.Id)
                .Include(a => a.AssessmentCategory)
                .ToListAsync(ct);

            if (!assessments.Any()) continue;

            var studentGrades = await _dbContext.Grades
                .Where(g => g.StudentId == studentId && assessments.Select(a => a.Id).Contains(g.AssessmentId))
                .ToListAsync(ct);

            if (!studentGrades.Any())
            {
                courseRecords.Add(new TranscriptCourseRecord(
                    offering.Id,
                    offering.Course?.Code ?? "N/A",
                    offering.Course?.Title ?? "N/A",
                    offering.Course?.CreditUnits ?? 0,
                    (int)offering.Semester,
                    offering.AcademicSession?.Name ?? "N/A",
                    null,
                    null,
                    await CalculateAttendancePercentage(offering.Id, studentId, ct)));
                continue;
            }

            var totalScore = CalculateCourseScore(assessments, studentGrades, sysConfig!, finalizedOnly: isOfficial);
            if (!totalScore.HasValue)
            {
                courseRecords.Add(new TranscriptCourseRecord(
                    offering.Id,
                    offering.Course?.Code ?? "N/A",
                    offering.Course?.Title ?? "N/A",
                    offering.Course?.CreditUnits ?? 0,
                    (int)offering.Semester,
                    offering.AcademicSession?.Name ?? "N/A",
                    null,
                    null,
                    await CalculateAttendancePercentage(offering.Id, studentId, ct)));
                continue;
            }

            var rStrategy = sysConfig!.RoundingStrategy;
            var decimalPlaces = sysConfig!.RoundingDecimalPlaces;
            var roundedScore = GradeCalculator.RoundScore(totalScore.Value, rStrategy, decimalPlaces);
            var letterGrade = CalculateLetterGrade(roundedScore, mappings, sysConfig);
            var gradePoints = ConvertToGradePoints(roundedScore, sysConfig);
            var attendancePercentage = await CalculateAttendancePercentage(offering.Id, studentId, ct);

            courseRecords.Add(new TranscriptCourseRecord(
                offering.Id,
                offering.Course?.Code ?? "N/A",
                offering.Course?.Title ?? "N/A",
                offering.Course?.CreditUnits ?? 0,
                (int)offering.Semester,
                offering.AcademicSession?.Name ?? "N/A",
                letterGrade,
                gradePoints,
                attendancePercentage));
        }

        // Calculate cumulative GPA
        decimal totalGpaPoints = 0;
        decimal totalGpaCredits = 0;
        int totalCreditsEarned = 0;

        foreach (var record in courseRecords)
        {
            if (record.GradePoints.HasValue)
            {
                totalGpaPoints += record.GradePoints.Value * record.CreditUnits;
                totalGpaCredits += record.CreditUnits;

                if (record.GradePoints.Value >= 1.0m)
                {
                    totalCreditsEarned += record.CreditUnits;
                }
            }
        }

        var cumulativeGpa = totalGpaCredits > 0 ? Math.Round(totalGpaPoints / totalGpaCredits, 2) : 0;

        // Get academic standing
        var standing = await _dbContext.AcademicStandings
            .Where(s => s.StudentId == studentId && (s.ExpiryDate == null || s.ExpiryDate > DateTime.UtcNow))
            .OrderByDescending(s => s.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        return new TranscriptDto(
            studentId,
            $"{student.FirstName} {student.LastName}",
            student.StudentNumber ?? "N/A",
            student.OfficialEmail,
            student.AcademicProgram?.Name ?? "N/A",
            student.Level?.Name ?? "N/A",
            student.AcademicProgram?.Type ?? Data.Enums.ProgramType.Undergraduate,
            student.AdmissionApplication?.DateOfBirth ?? DateTime.UtcNow.AddYears(-20),
            student.AdmissionApplication?.Nationality ?? "N/A",
            student.AdmissionApplication?.AcademicSession?.Name ?? "N/A",
            courseRecords.OrderBy(x => x.AcademicSessionName).ThenBy(x => x.Semester).ToList(),
            cumulativeGpa,
            totalCreditsEarned,
            standing?.StandingType.ToString() ?? "GoodStanding",
            isOfficial,
            "System",
            DateTime.UtcNow);
    }

    public async Task<ErrorOr<TranscriptRequestDto>> CreateTranscriptRequestAsync(Guid studentId, CreateTranscriptRequestDto request, Guid requestedBy, CancellationToken ct = default)
    {
        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == studentId, ct);
        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var config = await _dbContext.SystemTranscriptConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            config = new SystemTranscriptConfiguration
            {
                ChargeForTranscripts = true,
                OfficialTranscriptFee = 5000m
            };
            _dbContext.SystemTranscriptConfigurations.Add(config);
            await _dbContext.SaveChangesAsync(ct);
        }

        var transcriptRequest = new TranscriptRequest
        {
            StudentId = studentId,
            Status = TranscriptStatus.Pending,
            IsOfficial = request.IsOfficial,
            DeliveryEmail = request.DeliveryEmail,
            DeliveryMethod = request.DeliveryMethod ?? "Email",
            Remarks = request.Remarks,
            FeeAmount = config.ChargeForTranscripts && request.IsOfficial ? config.OfficialTranscriptFee : 0m,
            FeePaid = false,
            CreatedById = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TranscriptRequests.Add(transcriptRequest);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("CreateTranscriptRequest", "TranscriptRequest", transcriptRequest.Id.ToString(),
            $"Created transcript request for student {studentId}", ct);

        return MapToTranscriptRequestDto(transcriptRequest);
    }

    public async Task<ErrorOr<TranscriptRequestDto>> GetTranscriptRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await _dbContext.TranscriptRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (request == null)
            return DomainErrors.Reporting.TranscriptNotFound;

        return MapToTranscriptRequestDto(request);
    }

    public async Task<ErrorOr<List<TranscriptRequestDto>>> GetStudentTranscriptRequestsAsync(Guid studentId, CancellationToken ct = default)
    {
        var requests = await _dbContext.TranscriptRequests
            .Where(x => x.StudentId == studentId)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToTranscriptRequestDto).ToList();
    }

    public async Task<ErrorOr<List<TranscriptRequestDto>>> GetAllTranscriptRequestsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var requests = await _dbContext.TranscriptRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return requests.Select(MapToTranscriptRequestDto).ToList();
    }

    public async Task<ErrorOr<TranscriptRequestDto>> ProcessTranscriptRequestAsync(Guid requestId, Guid processedBy, CancellationToken ct = default)
    {
        var request = await _dbContext.TranscriptRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (request == null)
            return DomainErrors.Reporting.TranscriptNotFound;

        if (request.Status != TranscriptStatus.Pending && request.Status != TranscriptStatus.Processing)
            return Error.Conflict("Transcript.AlreadyProcessed", "Transcript request has already been processed");

        request.Status = TranscriptStatus.Ready;
        request.ProcessedBy = processedBy;
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        // Generate the transcript document PDF and save it
        var transcript = await GenerateTranscriptAsync(request.StudentId, request.IsOfficial, ct);
        if (!transcript.IsError)
        {
            var pdfBytes = GenerateTranscriptPdfBytes(transcript.Value);
            using var pdfStream = new MemoryStream(pdfBytes);
            var relativePath = await _fileStorageService.SaveFileAsync("transcripts", request.Id.ToString(), $"{request.Id}.pdf", pdfStream);
            request.DocumentUrl = $"/uploads/{relativePath}";
        }

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("ProcessTranscriptRequest", "TranscriptRequest", request.Id.ToString(),
            $"Processed transcript request by {processedBy}", ct);

        return MapToTranscriptRequestDto(request);
    }

    private byte[] GenerateTranscriptPdfBytes(TranscriptDto transcript)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                page.Header().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().AlignCenter().Text("WIGWE UNIVERSITY").FontSize(18).Bold().FontColor("#0F172A");
                    col.Item().AlignCenter().Text("ACADEMIC TRANSCRIPT").FontSize(12).Bold().FontColor("#059669");

                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Student Name: {transcript.StudentName}").Bold();
                            c.Item().Text($"Student Number: {transcript.StudentNumber}");
                            c.Item().Text($"Email: {transcript.Email}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Academic Program: {transcript.ProgramName}").Bold();
                            c.Item().Text($"Current Level: {transcript.LevelName}");
                            c.Item().Text($"Academic Standing: {transcript.StandingType}");
                        });
                    });
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(15);
                    var sessions = transcript.CourseRecords.GroupBy(c => c.AcademicSessionName);

                    foreach (var sessionGroup in sessions)
                    {
                        col.Item().Text(sessionGroup.Key).FontSize(11).Bold().FontColor("#0F172A").Underline();

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(80);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#0F172A").Padding(5).Text("Course Code").FontColor(Colors.White).Bold();
                                header.Cell().Background("#0F172A").Padding(5).Text("Course Title").FontColor(Colors.White).Bold();
                                header.Cell().Background("#0F172A").Padding(5).Text("Credits").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Grade").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Points").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Attendance").FontColor(Colors.White).Bold().AlignCenter();
                            });

                            foreach (var course in sessionGroup)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CourseCode);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CourseTitle);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CreditUnits.ToString()).AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.GradeLetter ?? "-").AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.GradePoints.HasValue ? course.GradePoints.Value.ToString("0.00") : "-").AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text($"{course.AttendancePercentage}%").AlignCenter();
                            }
                        });
                    }

                    col.Item().AlignRight().Border(1).BorderColor("#059669").Padding(10).Column(sc =>
                    {
                        sc.Item().Text($"Total Earned Credits: {transcript.TotalCreditsEarned}").Bold();
                        sc.Item().Text($"Cumulative GPA: {transcript.CumulativeGpa:0.00}").Bold().FontColor("#059669").FontSize(11);
                    });
                });

                page.Footer().Column(fcol =>
                {
                    fcol.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    fcol.Item().PaddingVertical(5).Row(row =>
                    {
                        row.RelativeItem().Text($"Status: {(transcript.IsOfficial ? "OFFICIAL TRANSCRIPT" : "UNOFFICIAL COPY")}").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Generated on: ").FontSize(8).FontColor(Colors.Grey.Medium);
                            x.Span(transcript.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(8).FontColor(Colors.Grey.Medium);
                            x.Span(" | Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private async Task<int> CalculateAttendancePercentage(Guid courseOfferingId, Guid studentId, CancellationToken ct)
    {
        var totalSessions = await _dbContext.LectureSessions
            .CountAsync(s => s.CourseOfferingId == courseOfferingId, ct);

        if (totalSessions == 0) return 0;

        var attendedSessions = await _dbContext.SessionAttendances
            .CountAsync(a => a.LectureSession.CourseOfferingId == courseOfferingId
                && a.StudentId == studentId
                && a.IsPresent, ct);

        return totalSessions > 0 ? (int)((decimal)attendedSessions / totalSessions * 100) : 0;
    }

    private string CalculateLetterGrade(decimal marks, List<LMS.Api.Contracts.GradeMappingDto>? mappings = null, SystemGradingConfiguration? sysConfig = null)
    {
        var rStrategy = sysConfig?.RoundingStrategy ?? RoundingStrategy.Standard;
        var decimalPlaces = sysConfig?.RoundingDecimalPlaces ?? 0;
        var graceThreshold = sysConfig?.GraceThreshold ?? 0.0m;

        var result = GradeCalculator.CalculateGrade(marks, rStrategy, decimalPlaces, graceThreshold, mappings ?? new List<LMS.Api.Contracts.GradeMappingDto>());
        return result.LetterGrade;
    }

    private static decimal? CalculateCourseScore(
        IReadOnlyCollection<Assessment> assessments,
        IReadOnlyCollection<Grade> grades,
        SystemGradingConfiguration sysConfig,
        bool finalizedOnly)
    {
        var usableGrades = finalizedOnly
            ? grades.Where(g => g.IsLocked).ToList()
            : grades.ToList();

        if (usableGrades.Count == 0)
        {
            return null;
        }

        var percentages = assessments
            .Select(assessment =>
            {
                var grade = usableGrades.FirstOrDefault(g => g.AssessmentId == assessment.Id);
                if (grade == null || assessment.MaxMarks <= 0)
                {
                    return null;
                }

                return new AssessmentPercentage(
                    assessment.AssessmentCategoryId,
                    assessment.AssessmentCategory.Weight,
                    grade.MarksObtained / assessment.MaxMarks * 100m);
            })
            .Where(x => x != null)
            .Cast<AssessmentPercentage>()
            .ToList();

        if (percentages.Count == 0)
        {
            return null;
        }

        if (sysConfig.DefaultGradingStyle == GradingStyle.Unweighted)
        {
            return percentages.Average(x => x.Percentage);
        }

        return percentages
            .GroupBy(x => x.CategoryId)
            .Sum(category =>
            {
                var categoryAverage = category.Average(x => x.Percentage);
                var categoryWeight = category.First().CategoryWeight;
                return categoryAverage * categoryWeight / 100m;
            });
    }

    private decimal ConvertToGradePoints(decimal marks, SystemGradingConfiguration sysConfig)
    {
        var mappings = string.IsNullOrEmpty(sysConfig.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();
              
        var rStrategy = sysConfig.RoundingStrategy;
        var decimalPlaces = sysConfig.RoundingDecimalPlaces;
        var graceThreshold = sysConfig.GraceThreshold;

        if (mappings != null && mappings.Any())
        {
            var result = GradeCalculator.CalculateGrade(marks, rStrategy, decimalPlaces, graceThreshold, mappings);
            return result.GradePoints;
        }

        var defaults5 = new List<(decimal Min, string Letter, decimal Points)>
        {
            (70m, "A", 5.0m), (60m, "B", 4.0m), (50m, "C", 3.0m), (45m, "D", 2.0m), (40m, "E", 1.0m), (0m, "F", 0.0m)
        };
        var defaults4 = new List<(decimal Min, string Letter, decimal Points)>
        {
            (70m, "A", 4.0m), (65m, "B+", 3.75m), (60m, "B", 3.5m), (55m, "C+", 3.0m), (50m, "C", 2.5m), (45m, "D", 2.0m), (40m, "E", 1.0m), (0m, "F", 0.0m)
        };

        var targetDefaults = sysConfig.GpaScale == 5.0m ? defaults5 : defaults4;

        decimal score = GradeCalculator.RoundScore(marks, rStrategy, decimalPlaces);
        if (graceThreshold > 0)
        {
            foreach (var d in targetDefaults)
            {
                if (score < d.Min && d.Min - score <= graceThreshold)
                {
                    score = d.Min;
                    break;
                }
            }
        }

        var matched = targetDefaults.FirstOrDefault(x => score >= x.Min);
        return matched.Points;
    }

    private sealed record AssessmentPercentage(Guid CategoryId, decimal CategoryWeight, decimal Percentage);

    private TranscriptRequestDto MapToTranscriptRequestDto(TranscriptRequest request)
    {
        return new TranscriptRequestDto(
            request.Id,
            request.StudentId,
            request.Student?.DisplayName ?? "Unknown",
            request.IsOfficial,
            request.Status,
            request.DeliveryEmail,
            request.DeliveryMethod ?? "Email",
            request.FeeAmount,
            request.FeePaid,
            request.DocumentUrl,
            !string.IsNullOrWhiteSpace(request.Processor?.DisplayName) ? request.Processor.DisplayName : null,
            request.CreatedAt,
            request.CompletedAt);
    }

    public async Task<ErrorOr<SystemTranscriptConfigurationDto>> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await _dbContext.SystemTranscriptConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            config = new SystemTranscriptConfiguration
            {
                ChargeForTranscripts = true,
                OfficialTranscriptFee = 5000m
            };
            _dbContext.SystemTranscriptConfigurations.Add(config);
            await _dbContext.SaveChangesAsync(ct);
        }

        return MapToSystemTranscriptConfigurationDto(config);
    }

    public async Task<ErrorOr<SystemTranscriptConfigurationDto>> UpdateConfigurationAsync(UpdateSystemTranscriptConfigurationRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await _dbContext.SystemTranscriptConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            config = new SystemTranscriptConfiguration();
            _dbContext.SystemTranscriptConfigurations.Add(config);
        }

        if (request.ChargeForTranscripts.HasValue)
        {
            config.ChargeForTranscripts = request.ChargeForTranscripts.Value;
        }

        if (request.OfficialTranscriptFee.HasValue)
        {
            config.OfficialTranscriptFee = request.OfficialTranscriptFee.Value;
        }

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedById = userId;

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("UpdateTranscriptConfiguration", "SystemTranscriptConfiguration", config.Id.ToString(),
            "Updated system transcript configuration settings", ct);

        return MapToSystemTranscriptConfigurationDto(config);
    }

    private static SystemTranscriptConfigurationDto MapToSystemTranscriptConfigurationDto(SystemTranscriptConfiguration config)
    {
        return new SystemTranscriptConfigurationDto(
            config.Id,
            config.ChargeForTranscripts,
            config.OfficialTranscriptFee,
            config.UpdatedAt);
    }
}
