using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

public sealed class CertificateService : BaseService, ICertificateService
{
    private readonly LmsDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;

    public CertificateService(LmsDbContext dbContext, IAuditService auditService, IFileStorageService fileStorageService) 
        : base(auditService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
    }

    public async Task<ErrorOr<CertificateRequestDto>> CreateCertificateRequestAsync(Guid studentId, CreateCertificateRequestDto request, Guid requestedBy, CancellationToken ct = default)
    {
        var student = await _dbContext.Students
            .FirstOrDefaultAsync(x => x.Id == studentId, ct);

        if (student == null)
            return DomainErrors.Reporting.StudentNotFound;

        var config = await GetOrInitConfigurationAsync(ct);

        var credentialId = $"WWU-CERT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        var certificateRequest = new CertificateRequest
        {
            StudentId = studentId,
            CertificateType = request.CertificateType,
            Status = CertificateStatus.Pending,
            DeliveryMethod = request.DeliveryMethod ?? "Email",
            DeliveryEmail = request.DeliveryEmail,
            Remarks = request.Remarks,
            FeeAmount = config.ChargeForCertificates ? config.OfficialCertificateFee : 0m,
            FeePaid = false,
            CredentialId = credentialId,
            CreatedById = requestedBy,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.CertificateRequests.Add(certificateRequest);
        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("CreateCertificateRequest", "CertificateRequest", certificateRequest.Id.ToString(),
            $"Created certificate request of type {request.CertificateType} for student {studentId}", ct);

        var requestWithDetails = await GetRequestWithDetailsAsync(certificateRequest.Id, ct);
        return MapToCertificateRequestDto(requestWithDetails!);
    }

    public async Task<ErrorOr<CertificateRequestDto>> ProcessCertificateRequestAsync(Guid requestId, Guid processedBy, bool bypassGraduationCheck = false, CancellationToken ct = default)
    {
        var request = await GetRequestWithDetailsAsync(requestId, ct);
        if (request == null)
            return DomainErrors.Reporting.CertificateNotFound;

        if (request.Status != CertificateStatus.Pending && request.Status != CertificateStatus.Processing)
            return Error.Conflict("Certificate.AlreadyProcessed", "Certificate request has already been processed");

        // Graduation verification check
        if (request.CertificateType == CertificateType.Graduation && !bypassGraduationCheck)
        {
            var latestDegreeAudit = await _dbContext.DegreeAudits
                .Where(x => x.StudentId == request.StudentId)
                .OrderByDescending(x => x.GeneratedAt)
                .FirstOrDefaultAsync(ct);

            if (latestDegreeAudit == null || latestDegreeAudit.Status != DegreeAuditStatus.Complete)
            {
                return DomainErrors.Reporting.GraduationCheckFailed;
            }
        }

        request.Status = CertificateStatus.Ready;
        request.ProcessedBy = processedBy;
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        // Generate PDF
        var config = await GetOrInitConfigurationAsync(ct);
        var pdfBytes = GenerateCertificatePdfBytes(request, config);
        using var pdfStream = new MemoryStream(pdfBytes);
        var relativePath = await _fileStorageService.SaveFileAsync("certificates", request.Id.ToString(), $"{request.Id}.pdf", pdfStream);
        request.DocumentUrl = $"/uploads/{relativePath}";

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("ProcessCertificateRequest", "CertificateRequest", request.Id.ToString(),
            $"Processed certificate request by {processedBy} (BypassGraduationCheck={bypassGraduationCheck})", ct);

        return MapToCertificateRequestDto(request);
    }

    public async Task<ErrorOr<CertificateRequestDto>> GetCertificateRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await GetRequestWithDetailsAsync(requestId, ct);
        if (request == null)
            return DomainErrors.Reporting.CertificateNotFound;

        return MapToCertificateRequestDto(request);
    }

    public async Task<ErrorOr<List<CertificateRequestDto>>> GetStudentCertificateRequestsAsync(Guid studentId, CancellationToken ct = default)
    {
        var requests = await _dbContext.CertificateRequests
            .Where(x => x.StudentId == studentId)
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return requests.Select(MapToCertificateRequestDto).ToList();
    }

    public async Task<ErrorOr<List<CertificateRequestDto>>> GetAllCertificateRequestsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var requests = await _dbContext.CertificateRequests
            .Include(x => x.Student)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return requests.Select(MapToCertificateRequestDto).ToList();
    }

    public async Task<ErrorOr<SystemCertificateConfigurationDto>> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = await GetOrInitConfigurationAsync(ct);
        return MapToSystemCertificateConfigurationDto(config);
    }

    public async Task<ErrorOr<SystemCertificateConfigurationDto>> UpdateConfigurationAsync(UpdateSystemCertificateConfigurationRequest request, Guid userId, CancellationToken ct = default)
    {
        var config = await GetOrInitConfigurationAsync(ct);

        if (request.ChargeForCertificates.HasValue)
            config.ChargeForCertificates = request.ChargeForCertificates.Value;

        if (request.OfficialCertificateFee.HasValue)
            config.OfficialCertificateFee = request.OfficialCertificateFee.Value;

        if (request.SignatoryName != null)
            config.SignatoryName = request.SignatoryName;

        if (request.SignatoryPosition != null)
            config.SignatoryPosition = request.SignatoryPosition;

        if (request.SignatorySignatureBase64 != null)
            config.SignatorySignatureBase64 = request.SignatorySignatureBase64;

        if (request.RegistrarName != null)
            config.RegistrarName = request.RegistrarName;

        if (request.RegistrarPosition != null)
            config.RegistrarPosition = request.RegistrarPosition;

        if (request.RegistrarSignatureBase64 != null)
            config.RegistrarSignatureBase64 = request.RegistrarSignatureBase64;

        config.UpdatedAt = DateTime.UtcNow;
        config.UpdatedById = userId;

        await _dbContext.SaveChangesAsync(ct);

        await LogActionAsync("UpdateCertificateConfiguration", "SystemCertificateConfiguration", config.Id.ToString(),
            "Updated system certificate configurations", ct);

        return MapToSystemCertificateConfigurationDto(config);
    }

    public async Task<ErrorOr<CertificateVerificationDto>> VerifyCertificateAsync(string credentialId, CancellationToken ct = default)
    {
        var request = await _dbContext.CertificateRequests
            .Include(x => x.Student)
            .ThenInclude(s => s!.AcademicProgram)
            .FirstOrDefaultAsync(x => x.CredentialId == credentialId && x.Status == CertificateStatus.Ready, ct);

        if (request == null)
        {
            return new CertificateVerificationDto(
                credentialId,
                "N/A",
                "N/A",
                "N/A",
                DateTime.MinValue,
                false,
                "Invalid"
            );
        }

        var studentName = request.Student != null ? $"{request.Student.FirstName} {request.Student.LastName}" : "Unknown Student";
        var programName = request.Student?.AcademicProgram?.Name ?? "Unknown Program";
        var programType = request.Student?.AcademicProgram?.Type.ToString() ?? "Undergraduate";
        var classification = $"{programType} Degree in {programName}";

        return new CertificateVerificationDto(
            credentialId,
            studentName,
            programName,
            classification,
            request.CompletedAt ?? request.CreatedAt,
            true,
            "Verified"
        );
    }

    #region Helper Methods

    private async Task<CertificateRequest?> GetRequestWithDetailsAsync(Guid requestId, CancellationToken ct)
    {
        return await _dbContext.CertificateRequests
            .Include(x => x.Student)
            .ThenInclude(s => s!.AcademicProgram)
            .Include(x => x.Creator)
            .Include(x => x.Processor)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);
    }

    private async Task<SystemCertificateConfiguration> GetOrInitConfigurationAsync(CancellationToken ct)
    {
        var config = await _dbContext.SystemCertificateConfigurations
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        if (config == null)
        {
            config = new SystemCertificateConfiguration
            {
                ChargeForCertificates = true,
                OfficialCertificateFee = 10000m,
                SignatoryName = "Prof. Vice Chancellor",
                SignatoryPosition = "Vice Chancellor",
                RegistrarName = "Dr. Registrar",
                RegistrarPosition = "Registrar"
            };
            _dbContext.SystemCertificateConfigurations.Add(config);
            await _dbContext.SaveChangesAsync(ct);
        }

        return config;
    }

    private CertificateRequestDto MapToCertificateRequestDto(CertificateRequest request)
    {
        var studentName = request.Student != null ? $"{request.Student.FirstName} {request.Student.LastName}" : "Unknown";
        return new CertificateRequestDto(
            request.Id,
            request.StudentId,
            studentName,
            request.CertificateType,
            request.Status,
            request.DeliveryMethod,
            request.DeliveryEmail,
            request.FeeAmount,
            request.FeePaid,
            request.DocumentUrl,
            request.CredentialId,
            request.Processor?.DisplayName,
            request.CreatedAt,
            request.CompletedAt
        );
    }

    private SystemCertificateConfigurationDto MapToSystemCertificateConfigurationDto(SystemCertificateConfiguration config)
    {
        return new SystemCertificateConfigurationDto(
            config.Id,
            config.ChargeForCertificates,
            config.OfficialCertificateFee,
            config.SignatoryName,
            config.SignatoryPosition,
            config.SignatorySignatureBase64,
            config.RegistrarName,
            config.RegistrarPosition,
            config.RegistrarSignatureBase64,
            config.UpdatedAt
        );
    }

    private byte[] GenerateCertificatePdfBytes(CertificateRequest request, SystemCertificateConfiguration config)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var studentName = request.Student != null ? $"{request.Student.FirstName} {request.Student.LastName}".ToUpper() : "STUDENT NAME";
        var programName = request.Student?.AcademicProgram?.Name ?? "ACADEMIC PROGRAM";
        var dateText = (request.CompletedAt ?? DateTime.UtcNow).ToString("MMMM dd, yyyy");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily(Fonts.Verdana).FontSize(11).FontColor("#1E293B"));

                page.Content()
                    .Border(4)
                    .BorderColor("#004D36") // Outer Border: Wigwe Green
                    .Padding(8)
                    .Border(1.5f)
                    .BorderColor("#EAB308") // Inner Border: Wigwe Gold
                    .Padding(25)
                    .Column(col =>
                    {
                        col.Spacing(15);

                        // Header Section
                        col.Item().AlignCenter().Text("WIGWE UNIVERSITY").FontSize(26).Bold().FontColor("#004D36").LetterSpacing(0.08f);
                        col.Item().AlignCenter().Text("RIVERS STATE, NIGERIA").FontSize(10).Bold().FontColor("#EAB308").LetterSpacing(0.2f);

                        // Seal or Crest Spacing
                        col.Item().Height(25);

                        // Certificate Title
                        var titleText = request.CertificateType switch
                        {
                            CertificateType.Graduation => "CERTIFICATE OF GRADUATION",
                            CertificateType.Completion => "CERTIFICATE OF COMPLETION",
                            CertificateType.HonorRoll => "CERTIFICATE OF ACADEMIC EXCELLENCE",
                            _ => "CERTIFICATE OF ACHIEVEMENT"
                        };
                        col.Item().AlignCenter().Text(titleText).FontSize(18).Bold().FontColor("#0F172A").LetterSpacing(0.05f);

                        col.Item().AlignCenter().Text("This is to certify that").FontSize(11).Italic().FontColor("#64748B");

                        // Recipient Name
                        col.Item().AlignCenter().Text(studentName).FontSize(24).Bold().FontColor("#004D36").Underline();

                        // Certification Sub-text
                        col.Item().PaddingHorizontal(30).AlignCenter().Text(t =>
                        {
                            if (request.CertificateType == CertificateType.Graduation)
                            {
                                t.Span("has successfully completed the approved curriculum of study and satisfied all requirements for the award of the degree of ").FontColor("#334155").LineHeight(1.5f);
                                t.Span(programName).Bold().FontColor("#EAB308");
                                t.Span(" and is hereby admitted to that degree with all the rights, honors, and privileges appertaining thereto.").FontColor("#334155").LineHeight(1.5f);
                            }
                            else if (request.CertificateType == CertificateType.Completion)
                            {
                                t.Span("has successfully completed all assignments, projects, and curriculum requirements for the professional course of study in ").FontColor("#334155").LineHeight(1.5f);
                                t.Span(programName).Bold().FontColor("#004D36");
                                t.Span(".").FontColor("#334155");
                            }
                            else
                            {
                                t.Span("has demonstrated outstanding academic performance, leadership, and exemplary character and is hereby named to the Honor Roll in ").FontColor("#334155").LineHeight(1.5f);
                                t.Span(programName).Bold().FontColor("#004D36");
                                t.Span(".").FontColor("#334155");
                            }
                        });

                        col.Item().AlignCenter().Text($"Given under the seal of the University this {dateText}.").FontSize(10).FontColor("#64748B");

                        // Signatures
                        col.Item().ExtendVertical().AlignBottom().Row(row =>
                        {
                            // Left Signature (VC)
                            row.RelativeItem().Column(sigCol =>
                            {
                                sigCol.Spacing(2);
                                sigCol.Item().AlignCenter().Width(120).Height(40).Element(e =>
                                {
                                    if (!string.IsNullOrEmpty(config.SignatorySignatureBase64))
                                    {
                                        try
                                        {
                                            var cleanBase64 = config.SignatorySignatureBase64.Contains(",") 
                                                ? config.SignatorySignatureBase64.Split(',')[1] 
                                                : config.SignatorySignatureBase64;
                                            var bytes = Convert.FromBase64String(cleanBase64);
                                            e.Image(bytes);
                                        }
                                        catch
                                        {
                                            e.LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                                        }
                                    }
                                    else
                                    {
                                        e.LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                                    }
                                });
                                sigCol.Item().AlignCenter().Text(config.SignatoryName).FontSize(10).Bold().FontColor("#1E293B");
                                sigCol.Item().AlignCenter().Text(config.SignatoryPosition.ToUpper()).FontSize(8).Bold().FontColor("#94A3B8");
                            });

                            // Spacer
                            row.ConstantItem(100);

                            // Right Signature (Registrar)
                            row.RelativeItem().Column(sigCol =>
                            {
                                sigCol.Spacing(2);
                                sigCol.Item().AlignCenter().Width(120).Height(40).Element(e =>
                                {
                                    if (!string.IsNullOrEmpty(config.RegistrarSignatureBase64))
                                    {
                                        try
                                        {
                                            var cleanBase64 = config.RegistrarSignatureBase64.Contains(",") 
                                                ? config.RegistrarSignatureBase64.Split(',')[1] 
                                                : config.RegistrarSignatureBase64;
                                            var bytes = Convert.FromBase64String(cleanBase64);
                                            e.Image(bytes);
                                        }
                                        catch
                                        {
                                            e.LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                                        }
                                    }
                                    else
                                    {
                                        e.LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                                    }
                                });
                                sigCol.Item().AlignCenter().Text(config.RegistrarName).FontSize(10).Bold().FontColor("#1E293B");
                                sigCol.Item().AlignCenter().Text(config.RegistrarPosition.ToUpper()).FontSize(8).Bold().FontColor("#94A3B8");
                            });
                        });

                        col.Item().Height(10);

                        // Credential Verify Footer
                        col.Item().Row(footerRow =>
                        {
                            footerRow.RelativeItem().Text($"Credential ID: {request.CredentialId}").FontSize(7.5f).FontColor("#94A3B8");
                            footerRow.RelativeItem().AlignRight().Text("Verify authenticity at: www.wigweuniversity.edu.ng/verify-certificate").FontSize(7.5f).FontColor("#94A3B8");
                        });
                    });
            });
        });

        return doc.GeneratePdf();
    }

    public async Task<ErrorOr<BatchProcessResultDto>> ProcessCertificateRequestsBatchAsync(
        List<Guid> requestIds, 
        Guid processedBy, 
        bool bypassGraduationCheck = false, 
        CancellationToken ct = default)
    {
        var errors = new List<BatchProcessErrorDto>();
        int successCount = 0;

        foreach (var id in requestIds)
        {
            var result = await ProcessCertificateRequestAsync(id, processedBy, bypassGraduationCheck, ct);
            if (result.IsError)
            {
                errors.Add(new BatchProcessErrorDto(id, result.FirstError.Description));
            }
            else
            {
                successCount++;
            }
        }

        return new BatchProcessResultDto(
            requestIds.Count,
            successCount,
            errors.Count,
            errors
        );
    }

    #endregion
}
