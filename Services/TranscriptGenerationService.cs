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
    private readonly ILetterTemplateService _templateService;
    private readonly IGradeCalculationEngine _gradeCalculationEngine;

    public TranscriptGenerationService(
        LmsDbContext dbContext,
        IAuditService auditService,
        IFileStorageService fileStorageService,
        ILetterTemplateService templateService,
        IGradeCalculationEngine gradeCalculationEngine) : base(auditService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _templateService = templateService;
        _gradeCalculationEngine = gradeCalculationEngine;
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
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == studentId, ct);
            if (user != null)
            {
                student = await _dbContext.Students
                    .Include(x => x.AcademicProgram)
                    .Include(x => x.Level)
                    .Include(x => x.AdmissionApplication)
                    .ThenInclude(a => a!.AcademicSession)
                    .Include(x => x.Faculty)
                    .FirstOrDefaultAsync(s => s.OfficialEmail == user.Email || (user.EntraObjectId != null && s.EntraObjectId == user.EntraObjectId), ct);
            }
        }

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var appUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == student.Id 
                || (!string.IsNullOrEmpty(student.OfficialEmail) && u.Email == student.OfficialEmail)
                || (student.EntraObjectId != null && u.EntraObjectId == student.EntraObjectId), ct);

        var studentUserId = appUser?.Id ?? student.Id;
        var studentIds = new List<Guid> { student.Id, studentUserId }.Distinct().ToList();

        // Build course records
        var sysConfig = await _dbContext.SystemGradingConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct) ?? new SystemGradingConfiguration();
            
        var mappings = string.IsNullOrEmpty(sysConfig?.LetterGradesMappingJson) || sysConfig.LetterGradesMappingJson == "[]"
            ? new List<LMS.Api.Contracts.GradeMappingDto>()
            : System.Text.Json.JsonSerializer.Deserialize<List<LMS.Api.Contracts.GradeMappingDto>>(sysConfig.LetterGradesMappingJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
              ?? new List<LMS.Api.Contracts.GradeMappingDto>();

        var enrolledOfferingIds = await _dbContext.CourseEnrollments
            .Where(e => studentIds.Contains(e.StudentId) && e.Status != "Dropped")
            .Select(e => e.CourseOfferingId)
            .ToListAsync(ct);

        var gradedOfferingIds = await _dbContext.Grades
            .Where(g => studentIds.Contains(g.StudentId))
            .Select(g => g.Assessment.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct);

        var resultOfferingIds = await _dbContext.StudentCourseResults
            .Where(r => studentIds.Contains(r.StudentId))
            .Select(r => r.CourseOfferingId)
            .Distinct()
            .ToListAsync(ct);

        var relevantOfferingIds = enrolledOfferingIds
            .Union(gradedOfferingIds)
            .Union(resultOfferingIds)
            .Distinct()
            .ToList();

        var offerings = await _dbContext.CourseOfferings
            .Include(co => co.Course)
            .Include(co => co.AcademicSession)
            .Where(co => relevantOfferingIds.Contains(co.Id))
            .Distinct()
            .ToListAsync(ct);

        var publishedOfferingIds = await _dbContext.GradePublications
            .Where(gp => gp.IsVisibleToStudents && relevantOfferingIds.Contains(gp.CourseOfferingId))
            .Select(gp => gp.CourseOfferingId)
            .ToListAsync(ct);

        var savedResults = await _dbContext.StudentCourseResults
            .Where(r => studentIds.Contains(r.StudentId) && relevantOfferingIds.Contains(r.CourseOfferingId))
            .ToListAsync(ct);

        var courseRecords = new List<TranscriptCourseRecord>();
        foreach (var offering in offerings)
        {
            var saved = savedResults.FirstOrDefault(r => r.CourseOfferingId == offering.Id);
            bool isPublished = publishedOfferingIds.Contains(offering.Id) || (saved?.IsPublished == true);

            // If official transcript is requested and results are neither published in publications nor saved results, do not disclose draft
            if (!isPublished && isOfficial)
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
                    0,
                    null));
                continue;
            }

            string? letterGrade = null;
            decimal? gradePoints = null;
            decimal? score = null;

            if (saved != null)
            {
                score = saved.TotalScore;
                letterGrade = saved.LetterGrade;
                gradePoints = saved.GradePoints;
            }
            else
            {
                // Calculate from assessments and grades
                var assessments = await _dbContext.Assessments
                    .Where(a => a.CourseOfferingId == offering.Id)
                    .Include(a => a.AssessmentCategory)
                    .ToListAsync(ct);

                var categories = await _dbContext.AssessmentCategories
                    .Where(c => c.CourseOfferingId == offering.Id)
                    .ToListAsync(ct);

                var studentGrades = await _dbContext.Grades
                    .Where(g => studentIds.Contains(g.StudentId) && assessments.Select(a => a.Id).Contains(g.AssessmentId))
                    .ToListAsync(ct);

                if (assessments.Any() && studentGrades.Any())
                {
                    var calc = _gradeCalculationEngine.CalculateStudentGrade(
                        studentUserId,
                        assessments,
                        categories,
                        studentGrades,
                        sysConfig);

                    score = calc.TotalScore;
                    letterGrade = calc.LetterGrade;
                    gradePoints = calc.GradePoints;
                }
            }

            // Ensure letter grade and grade points consistency if one was missing but score exists
            if (score.HasValue && (string.IsNullOrEmpty(letterGrade) || !gradePoints.HasValue))
            {
                var rStrategy = sysConfig.RoundingStrategy;
                var decimalPlaces = sysConfig.RoundingDecimalPlaces;
                var roundedScore = GradeCalculator.RoundScore(score.Value, rStrategy, decimalPlaces);
                letterGrade ??= CalculateLetterGrade(roundedScore, mappings, sysConfig);
                gradePoints ??= ConvertToGradePoints(roundedScore, sysConfig);
            }

            courseRecords.Add(new TranscriptCourseRecord(
                offering.Id,
                offering.Course?.Code ?? "N/A",
                offering.Course?.Title ?? "N/A",
                offering.Course?.CreditUnits ?? (saved?.CreditUnits ?? 0),
                (int)offering.Semester,
                offering.AcademicSession?.Name ?? "N/A",
                letterGrade,
                gradePoints,
                0,
                score));
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
            .Where(s => studentIds.Contains(s.StudentId) && (s.ExpiryDate == null || s.ExpiryDate > DateTime.UtcNow))
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

    private async Task<string> ResolveStudentNameAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student != null)
        {
            return $"{student.FirstName} {student.LastName}".Trim();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId, ct);

        if (user != null)
        {
            var linkedStudent = await _dbContext.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.OfficialEmail == user.Email || (user.EntraObjectId != null && s.EntraObjectId == user.EntraObjectId), ct);

            if (linkedStudent != null)
            {
                return $"{linkedStudent.FirstName} {linkedStudent.LastName}".Trim();
            }

            return user.DisplayName;
        }

        return "Unknown";
    }

    public async Task<ErrorOr<TranscriptRequestDto>> CreateTranscriptRequestAsync(Guid studentId, CreateTranscriptRequestDto request, Guid requestedBy, CancellationToken ct = default)
    {
        var student = await _dbContext.Students.FirstOrDefaultAsync(x => x.Id == studentId, ct);
        if (student == null)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == studentId, ct);
            if (user != null)
            {
                student = await _dbContext.Students.FirstOrDefaultAsync(s => s.OfficialEmail == user.Email || (user.EntraObjectId != null && s.EntraObjectId == user.EntraObjectId), ct);
            }
        }

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
            StudentId = student.Id,
            Status = TranscriptStatus.Pending,
            IsOfficial = request.IsOfficial,
            DeliveryEmail = request.DeliveryEmail,
            DeliveryMethod = request.DeliveryMethod ?? "Email",
            InstitutionName = request.InstitutionName,
            InstitutionEmail = request.InstitutionEmail,
            InstitutionAddress = request.InstitutionAddress,
            Remarks = request.Remarks,
            FeeAmount = config.ChargeForTranscripts && request.IsOfficial ? config.OfficialTranscriptFee : 0m,
            FeePaid = false,
            CreatedById = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TranscriptRequests.Add(transcriptRequest);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("CreateTranscriptRequest", "TranscriptRequest", transcriptRequest.Id.ToString(),
            $"Created transcript request for student {student.Id}", ct);

        return MapToTranscriptRequestDto(transcriptRequest, $"{student.FirstName} {student.LastName}".Trim());
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

        var studentName = await ResolveStudentNameAsync(request.StudentId, ct);
        return MapToTranscriptRequestDto(request, studentName);
    }

    public async Task<ErrorOr<List<TranscriptRequestDto>>> GetStudentTranscriptRequestsAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        var appUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentId || (student != null && (!string.IsNullOrEmpty(student.OfficialEmail) && u.Email == student.OfficialEmail)), ct);

        var ids = new List<Guid> { studentId };
        if (student != null) ids.Add(student.Id);
        if (appUser != null) ids.Add(appUser.Id);
        ids = ids.Distinct().ToList();

        var requests = await _dbContext.TranscriptRequests
            .Where(x => ids.Contains(x.StudentId))
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var resolvedName = student != null ? $"{student.FirstName} {student.LastName}".Trim() : appUser?.DisplayName;
        return requests.Select(r => MapToTranscriptRequestDto(r, resolvedName)).ToList();
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

        var studentIds = requests.Select(r => r.StudentId).Distinct().ToList();
        var students = await _dbContext.Students
            .AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), ct);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => studentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return requests.Select(req =>
        {
            string? name = null;
            if (students.TryGetValue(req.StudentId, out var sName) && !string.IsNullOrWhiteSpace(sName))
            {
                name = sName;
            }
            else if (!string.IsNullOrWhiteSpace(req.Student?.DisplayName) && req.Student.DisplayName != "Unknown")
            {
                name = req.Student.DisplayName;
            }
            else if (users.TryGetValue(req.StudentId, out var uName) && !string.IsNullOrWhiteSpace(uName))
            {
                name = uName;
            }

            return MapToTranscriptRequestDto(req, name);
        }).ToList();
    }

    public async Task<ErrorOr<TranscriptRequestDto>> ProcessTranscriptRequestAsync(Guid requestId, Guid processedBy, CancellationToken ct = default)
    {
        var request = await _dbContext.TranscriptRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (request == null)
            return DomainErrors.Reporting.TranscriptNotFound;

        request.Status = TranscriptStatus.Ready;
        request.ProcessedBy = processedBy;
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        // Generate the transcript document PDF and save it
        var transcript = await GenerateTranscriptAsync(request.StudentId, request.IsOfficial, ct);
        string studentName = !transcript.IsError ? transcript.Value.StudentName : await ResolveStudentNameAsync(request.StudentId, ct);

        if (!transcript.IsError)
        {
            var template = await _templateService.GetTemplateByTypeAsync("Undergraduate");
            var allTemplates = (await _templateService.GetAllTemplatesAsync()).ToList();
            var fallbackTemplate = allTemplates.FirstOrDefault(t => !string.IsNullOrEmpty(t.SignatureBase64) || !string.IsNullOrEmpty(t.LogoBase64));
            var certConfig = await _dbContext.SystemCertificateConfigurations.FirstOrDefaultAsync(ct);

            var logoBase64 = !string.IsNullOrEmpty(template?.LogoBase64)
                ? template.LogoBase64
                : fallbackTemplate?.LogoBase64;

            var sigBase64 = !string.IsNullOrEmpty(template?.SignatureBase64)
                ? template.SignatureBase64
                : (!string.IsNullOrEmpty(fallbackTemplate?.SignatureBase64)
                    ? fallbackTemplate.SignatureBase64
                    : certConfig?.RegistrarSignatureBase64);

            var sigName = !string.IsNullOrEmpty(template?.SignatoryName)
                ? template.SignatoryName
                : (fallbackTemplate?.SignatoryName ?? "Registrar");

            var sigPos = !string.IsNullOrEmpty(template?.SignatoryPosition)
                ? template.SignatoryPosition
                : (fallbackTemplate?.SignatoryPosition ?? "Registrar");

            template = new LetterTemplateResponse(
                template?.Id ?? Guid.NewGuid(),
                template?.Name ?? "Undergraduate Transcript",
                "Undergraduate",
                template?.HeaderTitle ?? fallbackTemplate?.HeaderTitle ?? "WIGWE UNIVERSITY",
                template?.HeaderSubtitle ?? fallbackTemplate?.HeaderSubtitle ?? "OFFICE OF THE REGISTRAR • ACADEMIC RECORDS",
                template?.HeaderContact ?? fallbackTemplate?.HeaderContact ?? "Rivers State, Nigeria • www.wigweuniversity.edu.ng",
                template?.HeaderDate ?? fallbackTemplate?.HeaderDate,
                logoBase64,
                sigBase64,
                null,
                true,
                sigName,
                sigPos,
                null,
                null
            );

            var pdfBytes = GenerateTranscriptPdfBytes(transcript.Value, template);
            using var pdfStream = new MemoryStream(pdfBytes);
            var relativePath = await _fileStorageService.SaveFileAsync("transcripts", request.Id.ToString(), $"{request.Id}.pdf", pdfStream);
            request.DocumentUrl = $"/uploads/{relativePath}";
        }

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("ProcessTranscriptRequest", "TranscriptRequest", request.Id.ToString(),
            $"Processed transcript request by {processedBy}", ct);

        return MapToTranscriptRequestDto(request, studentName);
    }

    private byte[] GenerateTranscriptPdfBytes(TranscriptDto transcript, LetterTemplateResponse? template = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.6f, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana));

                page.Header().Column(col =>
                {
                    col.Spacing(10);

                    // Top Letterhead with Logo & University Title (matching Offer Letter)
                    col.Item().BorderBottom(1).BorderColor("#E2E8F0").PaddingBottom(12).Row(row =>
                    {
                        row.RelativeItem().Row(innerRow =>
                        {
                            byte[]? logoBytes = null;
                            if (template != null && !string.IsNullOrEmpty(template.LogoBase64))
                            {
                                try
                                {
                                    logoBytes = Convert.FromBase64String(template.LogoBase64.Contains(",") ? template.LogoBase64.Split(',')[1] : template.LogoBase64);
                                }
                                catch { /* Fallback */ }
                            }

                            if (logoBytes == null)
                            {
                                var candidates = new[]
                                {
                                    Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png"),
                                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "logo.png"),
                                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "logo.png"),
                                    "/Users/mac/Apps/LMS APP/LMSApi/Assets/logo.png"
                                };

                                foreach (var path in candidates)
                                {
                                    if (File.Exists(path))
                                    {
                                        try { logoBytes = File.ReadAllBytes(path); break; } catch { }
                                    }
                                }
                            }

                            if (logoBytes != null)
                            {
                                innerRow.AutoItem().Height(65).Image(logoBytes);
                            }

                            var headerTitle = template?.HeaderTitle ?? "WIGWE UNIVERSITY";
                            var headerSubtitle = "OFFICE OF THE REGISTRAR • ACADEMIC RECORDS";
                            var headerContact = template?.HeaderContact ?? "Rivers State, Nigeria • www.wigweuniversity.edu.ng";

                            innerRow.RelativeItem().PaddingLeft(10).Column(innerCol =>
                            {
                                innerCol.Item().PaddingTop(3).Text(headerTitle.ToUpper()).FontSize(18).Bold().FontColor("#0F172A");
                                innerCol.Item().Text(headerSubtitle.ToUpper()).FontSize(9).Bold().FontColor("#D4AF37").LetterSpacing(0.15f);
                                innerCol.Item().Text(headerContact).FontSize(8).FontColor("#64748B");
                                innerCol.Item().PaddingTop(3).Text(transcript.IsOfficial ? "OFFICIAL ACADEMIC TRANSCRIPT" : "UNOFFICIAL STUDENT ACADEMIC RECORD")
                                    .FontSize(11).Bold().FontColor(transcript.IsOfficial ? "#004D36" : "#475569");
                            });
                        });

                        row.AutoItem().AlignRight().Column(dateCol =>
                        {
                            dateCol.Item().Text("DATE ISSUED").FontSize(7.5f).Bold().FontColor("#94A3B8").LetterSpacing(0.1f);
                            dateCol.Item().Text(DateTime.UtcNow.ToString("MMMM dd, yyyy")).FontSize(10).Bold().FontColor("#1E293B");
                            dateCol.Item().PaddingTop(4).Text(transcript.IsOfficial ? "STATUS: OFFICIAL" : "STATUS: STUDENT COPY")
                                .FontSize(8).Bold().FontColor(transcript.IsOfficial ? "#059669" : "#64748B");
                        });
                    });

                    // Student Information Details Grid
                    col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Student Name: {transcript.StudentName}").Bold().FontSize(10).FontColor("#0F172A");
                            c.Item().Text($"Matric / Student No: {transcript.StudentNumber}").FontSize(9).FontColor("#334155");
                            c.Item().Text($"Email: {transcript.Email}").FontSize(9).FontColor("#334155");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Academic Program: {transcript.ProgramName}").Bold().FontSize(10).FontColor("#0F172A");
                            c.Item().Text($"Current Level: {transcript.LevelName}").FontSize(9).FontColor("#334155");
                            c.Item().Text($"Academic Standing: {transcript.StandingType}").FontSize(9).FontColor("#334155");
                        });
                    });
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(14);
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
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(50);
                                columns.ConstantColumn(50);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background("#0F172A").Padding(5).Text("Course Code").FontColor(Colors.White).Bold();
                                header.Cell().Background("#0F172A").Padding(5).Text("Course Title").FontColor(Colors.White).Bold();
                                header.Cell().Background("#0F172A").Padding(5).Text("Credits").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Score").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Grade").FontColor(Colors.White).Bold().AlignCenter();
                                header.Cell().Background("#0F172A").Padding(5).Text("Points").FontColor(Colors.White).Bold().AlignCenter();
                            });

                            foreach (var course in sessionGroup)
                            {
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CourseCode);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CourseTitle);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.CreditUnits.ToString()).AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.Score.HasValue ? course.Score.Value.ToString("0") : "-").AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.GradeLetter ?? "-").AlignCenter();
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(course.GradePoints.HasValue ? course.GradePoints.Value.ToString("0.00") : "-").AlignCenter();
                            }
                        });
                    }

                    // Bottom Row: Registrar Signature & Academic GPA Totals
                    col.Item().PaddingTop(10).Row(botRow =>
                    {
                        // Left: Registrar Signature Block
                        botRow.RelativeItem().Column(sigCol =>
                        {
                            sigCol.Item().Text(transcript.IsOfficial ? "Certified Official Record:" : "Unofficial Record:").FontSize(9).FontColor("#334155");

                            if (transcript.IsOfficial && template != null && !string.IsNullOrEmpty(template.SignatureBase64))
                            {
                                try
                                {
                                    var sigBytes = Convert.FromBase64String(template.SignatureBase64.Contains(",") ? template.SignatureBase64.Split(',')[1] : template.SignatureBase64);
                                    sigCol.Item().PaddingVertical(4).Height(55).Image(sigBytes);
                                }
                                catch
                                {
                                    sigCol.Item().Height(40);
                                }
                            }
                            else
                            {
                                sigCol.Item().Height(40);
                            }

                            var signatoryName = template != null && !string.IsNullOrEmpty(template.SignatoryName)
                                ? template.SignatoryName
                                : "Registrar";
                            var signatoryPosition = template != null && !string.IsNullOrEmpty(template.SignatoryPosition)
                                ? template.SignatoryPosition
                                : "Registrar";

                            sigCol.Item().Text(signatoryName).FontSize(9).Bold().FontColor("#1E293B");
                            sigCol.Item().Text(signatoryPosition.ToUpper()).FontSize(8).Bold().FontColor("#64748B");
                        });

                        // Right: Cumulative Credits & GPA Box
                        botRow.AutoItem().Border(1).BorderColor("#004D36").Background("#F0FDF4").Padding(12).Column(sc =>
                        {
                            sc.Item().Text($"Total Credits Earned: {transcript.TotalCreditsEarned}").Bold().FontSize(9);
                            sc.Item().Text($"Cumulative GPA: {transcript.CumulativeGpa:0.00}").Bold().FontColor("#004D36").FontSize(12);
                        });
                    });
                });

                page.Footer().Column(fcol =>
                {
                    // University Quad-color Accent Bar (matching Offer Letter)
                    fcol.Item().Height(4).Row(row =>
                    {
                        row.RelativeItem().Background("#10B981");
                        row.RelativeItem().Background("#059669");
                        row.RelativeItem().Background("#0F172A");
                        row.RelativeItem().Background("#D4AF37");
                    });
                    fcol.Item().PaddingVertical(6).Row(row =>
                    {
                        row.RelativeItem().Text($"Status: {(transcript.IsOfficial ? "OFFICIAL TRANSCRIPT • AUTHENTICATED BY REGISTRY" : "STUDENT COPY • UNOFFICIAL")}").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Generated on: ").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                            x.Span(transcript.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                            x.Span(" | Page ").FontSize(7.5f).FontColor(Colors.Grey.Medium);
                            x.CurrentPageNumber().FontSize(7.5f).FontColor(Colors.Grey.Medium);
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
            return Math.Clamp(usableGrades.Sum(x => x.MarksObtained), 0m, 100m);
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

    private TranscriptRequestDto MapToTranscriptRequestDto(TranscriptRequest request, string? resolvedStudentName = null)
    {
        var studentName = resolvedStudentName;
        if (string.IsNullOrWhiteSpace(studentName) || studentName == "Unknown")
        {
            studentName = !string.IsNullOrWhiteSpace(request.Student?.DisplayName) ? request.Student.DisplayName : "Unknown";
        }

        return new TranscriptRequestDto(
            request.Id,
            request.StudentId,
            studentName,
            request.IsOfficial,
            request.Status,
            request.DeliveryEmail,
            request.DeliveryMethod ?? "Email",
            request.FeeAmount,
            request.FeePaid,
            request.DocumentUrl,
            !string.IsNullOrWhiteSpace(request.Processor?.DisplayName) ? request.Processor.DisplayName : null,
            request.CreatedAt,
            request.CompletedAt,
            request.InstitutionName,
            request.InstitutionEmail,
            request.InstitutionAddress);
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
