using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LMS.Api.Services;

public sealed class TranscriptCoverLetterPdfService : ITranscriptCoverLetterPdfService
{
    private record LetterSectionDto(string Id, string Type, string Title, string? Content, bool IsVisible);

    private readonly ILetterTemplateService _templateService;
    private readonly LmsDbContext _dbContext;

    public TranscriptCoverLetterPdfService(ILetterTemplateService templateService, LmsDbContext dbContext)
    {
        _templateService = templateService;
        _dbContext = dbContext;
    }

    public async Task<byte[]> GenerateCoverLetterPdfAsync(TranscriptRequest request, string? templateType = "TranscriptCoverLetter")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var template = await _templateService.GetTemplateByTypeAsync(templateType ?? "TranscriptCoverLetter");
        var student = await _dbContext.Students
            .Include(s => s.AcademicProgram)
            .Include(s => s.AdmissionApplication)
            .ThenInclude(a => a!.AcademicSession)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        var user = student == null
            ? await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.StudentId)
            : null;

        var studentName = student != null
            ? $"{student.FirstName} {student.LastName}".Trim()
            : (user?.DisplayName ?? "APPLICANT");

        var matricNumber = student?.StudentNumber ?? "WU/STU/TEMP";
        var entrySession = student?.AdmissionApplication?.AcademicSession?.Name ?? "2022/2023";
        var currentSession = DateTime.UtcNow.Year + "/" + (DateTime.UtcNow.Year + 1);

        var institutionName = !string.IsNullOrWhiteSpace(request.InstitutionName) ? request.InstitutionName : "XYZ University";
        var institutionAddress = !string.IsNullOrWhiteSpace(request.InstitutionAddress) ? request.InstitutionAddress : "(Recipient University's Address)";
        var institutionEmail = !string.IsNullOrWhiteSpace(request.InstitutionEmail) ? request.InstitutionEmail : "";

        var placeholders = new Dictionary<string, string>
        {
            { "{studentName}", studentName },
            { "{matricNumber}", matricNumber },
            { "{entrySession}", entrySession },
            { "{exitSession}", currentSession },
            { "{academicSession}", entrySession },
            { "{programName}", student?.AcademicProgram?.Name ?? "Academic Program" },
            { "{date}", request.CreatedAt.ToString("dd MMMM, yyyy") },
            { "{institutionName}", institutionName },
            { "{institutionAddress}", institutionAddress },
            { "{institutionEmail}", institutionEmail },
            { "{deliveryEmail}", request.DeliveryEmail ?? institutionEmail }
        };

        return RenderDocument(template, placeholders);
    }

    public async Task<byte[]> GenerateSampleCoverLetterPdfAsync(string? templateType = "TranscriptCoverLetter")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var template = await _templateService.GetTemplateByTypeAsync(templateType ?? "TranscriptCoverLetter");

        var placeholders = new Dictionary<string, string>
        {
            { "{studentName}", "CHIBUIKE DANIEL OKORO" },
            { "{matricNumber}", "WU/2022/CSC/042" },
            { "{entrySession}", "2022/2023" },
            { "{exitSession}", "2025/2026" },
            { "{academicSession}", "2022/2023 to 2025/2026" },
            { "{programName}", "B.Sc. Computer Science" },
            { "{date}", DateTime.UtcNow.ToString("dd MMMM, yyyy") },
            { "{institutionName}", "XYZ University" },
            { "{institutionAddress}", "(Recipient University's Address)" },
            { "{institutionEmail}", "admissions@xyz.edu" },
            { "{deliveryEmail}", "admissions@xyz.edu" }
        };

        return RenderDocument(template, placeholders);
    }

    private byte[] RenderDocument(LetterTemplateResponse? template, Dictionary<string, string> placeholders)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.7f, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(Fonts.Verdana));

                page.Content().Column(col =>
                {
                    // Render Sections if configured in template
                    var renderedCustomSections = false;
                    if (template != null && !string.IsNullOrWhiteSpace(template.SectionsJson))
                    {
                        try
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var sections = JsonSerializer.Deserialize<List<LetterSectionDto>>(template.SectionsJson, options);
                            if (sections != null && sections.Any(s => s.IsVisible))
                            {
                                renderedCustomSections = true;
                                foreach (var section in sections.Where(s => s.IsVisible))
                                {
                                    RenderSection(col, section, template, placeholders);
                                }
                            }
                        }
                        catch
                        {
                            renderedCustomSections = false;
                        }
                    }

                    if (!renderedCustomSections)
                    {
                        RenderDefaultLayout(col, template, placeholders);
                    }
                });

                page.Footer().Column(fcol =>
                {
                    fcol.Item().PaddingTop(10).Height(3).Row(row =>
                    {
                        row.RelativeItem(3).Background("#004D36"); // Wigwe teal/dark green
                        row.RelativeItem(1).Background("#D4AF37"); // Gold
                    });
                    fcol.Item().PaddingTop(4).AlignCenter().Text(x =>
                    {
                        x.Span("OFFICIAL TRANSCRIPT COVER LETTER | CONFIDENTIAL | WIGWE UNIVERSITY").FontSize(7.5f).FontColor("#94A3B8");
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private void RenderDefaultLayout(ColumnDescriptor col, LetterTemplateResponse? template, Dictionary<string, string> placeholders)
    {
        // 1. Header with Logo & Dual line
        RenderHeader(col, template);

        // 2. Office subtitle
        var officeSubtitle = template?.HeaderSubtitle ?? "Office of The Acting Registrar";
        col.Item().PaddingBottom(18).Text(officeSubtitle).FontSize(14).Italic().Bold().FontColor("#0F172A");

        // 3. Date
        var dateVal = !string.IsNullOrWhiteSpace(template?.HeaderDate) ? template.HeaderDate : $"Date: {placeholders["{date}"]}";
        col.Item().PaddingBottom(18).Text(dateVal).FontSize(10.5f).FontColor("#0F172A");

        // 4. Recipient Block
        col.Item().PaddingBottom(18).Column(r =>
        {
            r.Item().Text("The Registrar").Bold().FontColor("#0F172A");
            r.Item().Text(placeholders["{institutionName}"] + ",").FontColor("#0F172A");
            r.Item().Text(placeholders["{institutionAddress}"] + ",").FontColor("#0F172A");
        });

        // 5. Salutation
        col.Item().PaddingBottom(16).Text("Dear Registrar,").FontColor("#0F172A");

        // 6. Subject
        var subjectText = $"FORWARDING OF ACADEMIC TRANSCRIPT OF: {placeholders["{studentName}"].ToUpper()}, ({placeholders["{matricNumber}"]})";
        col.Item().PaddingBottom(18).Text(subjectText).Bold().FontSize(11).FontColor("#0F172A");

        // 7. Paragraph 1
        var p1 = $"At the request of {placeholders["{studentName}"]}, with Matriculation Number {placeholders["{matricNumber}"]}, who was a student of Wigwe University from {placeholders["{entrySession}"]} to {placeholders["{exitSession}"]}, I write to forward herewith his/her official Academic Transcript.";
        col.Item().PaddingBottom(14).Text(p1).LineHeight(1.5f).FontColor("#1E293B");

        // 8. Paragraph 2
        var p2 = $"You will kindly have to ensure and certify yourself that {placeholders["{studentName}"]} is the same person as the one who has requested us to send his/her Transcript, and in respect of whom Transcript has been issued.";
        col.Item().PaddingBottom(14).Text(p2).LineHeight(1.5f).FontColor("#1E293B");

        // 9. Paragraph 3
        var p3 = "The attached information is supplied in strict confidence solely for the official use of the recipient institution, and should not on any condition be communicated to the candidate or be made known to unauthorized person(s).";
        col.Item().PaddingBottom(14).Text(p3).LineHeight(1.5f).FontColor("#1E293B");

        // 10. Closing assurance
        col.Item().PaddingBottom(18).Text("Please accept the assurances of my esteemed regards.").LineHeight(1.5f).FontColor("#1E293B");

        // 11. Valediction & Signature Block
        col.Item().PaddingBottom(8).Text("Yours sincerely,").FontColor("#0F172A");

        RenderSignature(col, template);
    }

    private void RenderSection(ColumnDescriptor col, LetterSectionDto section, LetterTemplateResponse? template, Dictionary<string, string> placeholders)
    {
        var rawContent = ReplacePlaceholders(section.Content ?? "", placeholders);

        switch (section.Type?.ToLowerInvariant())
        {
            case "header":
                RenderHeader(col, template);
                break;

            case "office_subtitle":
                var subtitle = !string.IsNullOrWhiteSpace(rawContent) ? rawContent : (template?.HeaderSubtitle ?? "Office of The Acting Registrar");
                col.Item().PaddingBottom(18).Text(subtitle).FontSize(14).Italic().Bold().FontColor("#0F172A");
                break;

            case "date":
                var displayDate = !string.IsNullOrWhiteSpace(rawContent) && rawContent != "{date}"
                    ? rawContent
                    : (!string.IsNullOrWhiteSpace(template?.HeaderDate) ? template.HeaderDate : $"Date: {placeholders["{date}"]}");
                col.Item().PaddingBottom(16).Text(displayDate).FontSize(10.5f).FontColor("#0F172A");
                break;

            case "recipient":
                col.Item().PaddingBottom(16).Column(r =>
                {
                    if (!string.IsNullOrWhiteSpace(rawContent))
                    {
                        RenderHtmlOrText(r, rawContent);
                    }
                    else
                    {
                        r.Item().Text("The Registrar").Bold().FontColor("#0F172A");
                        r.Item().Text(placeholders["{institutionName}"] + ",").FontColor("#0F172A");
                        r.Item().Text(placeholders["{institutionAddress}"] + ",").FontColor("#0F172A");
                    }
                });
                break;

            case "salutation":
                var salutation = !string.IsNullOrWhiteSpace(rawContent) ? rawContent : "Dear Registrar,";
                col.Item().PaddingBottom(16).Text(salutation).FontColor("#0F172A");
                break;

            case "subject":
                col.Item().PaddingBottom(18).Text(rawContent).Bold().FontSize(11).FontColor("#0F172A");
                break;

            case "text":
            default:
                col.Item().PaddingBottom(14).Column(tc =>
                {
                    RenderHtmlOrText(tc, rawContent);
                });
                break;

            case "valediction":
                var valediction = !string.IsNullOrWhiteSpace(rawContent) ? rawContent : "Yours sincerely,";
                col.Item().PaddingBottom(8).Text(valediction).FontColor("#0F172A");
                break;

            case "signature":
                RenderSignature(col, template);
                break;
        }
    }

    private void RenderHeader(ColumnDescriptor col, LetterTemplateResponse? template)
    {
        col.Item().PaddingBottom(10).Column(hCol =>
        {
            // Logo centered at top
            byte[]? logoBytes = null;
            if (template != null && !string.IsNullOrEmpty(template.LogoBase64))
            {
                try
                {
                    logoBytes = Convert.FromBase64String(template.LogoBase64.Contains(",") ? template.LogoBase64.Split(',')[1] : template.LogoBase64);
                }
                catch { }
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
                hCol.Item().AlignCenter().Height(55).Image(logoBytes);
            }
            else
            {
                var title = template?.HeaderTitle ?? "WIGWE UNIVERSITY";
                hCol.Item().AlignCenter().Text(title.ToUpper()).FontSize(20).Bold().FontColor("#004D36");
            }

            // Divider bar (Green and Gold accents)
            hCol.Item().PaddingTop(8).Height(3).Row(row =>
            {
                row.RelativeItem(3).Background("#004D36"); // Teal/Dark Green
                row.RelativeItem(1).Background("#D4AF37"); // Gold
            });
        });
    }

    private void RenderSignature(ColumnDescriptor col, LetterTemplateResponse? template)
    {
        col.Item().Column(s =>
        {
            if (template != null && !string.IsNullOrEmpty(template.SignatureBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(template.SignatureBase64.Contains(",") ? template.SignatureBase64.Split(',')[1] : template.SignatureBase64);
                    s.Item().PaddingVertical(4).Height(50).Image(bytes);
                }
                catch
                {
                    s.Item().Height(30);
                }
            }
            else
            {
                s.Item().Height(30);
            }

            var sigName = !string.IsNullOrWhiteSpace(template?.SignatoryName) ? template.SignatoryName : "Moses N. Itauma,";
            var sigPos = !string.IsNullOrWhiteSpace(template?.SignatoryPosition) ? template.SignatoryPosition : "Acting Registrar & Secretary to Council";

            s.Item().Text(sigName).Bold().FontColor("#0F172A");
            s.Item().Text(sigPos).Italic().FontColor("#0F172A");
        });
    }

    private void RenderHtmlOrText(ColumnDescriptor col, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        if (!content.Contains("<p>") && !content.Contains("<ul>") && !content.Contains("<li>"))
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                col.Item().PaddingBottom(4).Text(line).LineHeight(1.5f).FontColor("#1E293B");
            }
            return;
        }

        var blockMatches = Regex.Matches(content, @"(<p\b[^>]*>.*?</p>|<ul\b[^>]*>.*?</ul>)");
        if (blockMatches.Count == 0)
        {
            var cleanText = Regex.Replace(content, "<.*?>", string.Empty);
            col.Item().Text(cleanText).LineHeight(1.5f).FontColor("#1E293B");
            return;
        }

        foreach (Match match in blockMatches)
        {
            var block = match.Value;
            if (block.StartsWith("<p"))
            {
                var innerHtml = Regex.Replace(block, @"^<p\b[^>]*>", "").Replace("</p>", "");
                col.Item().PaddingBottom(4).Text(text =>
                {
                    RenderRichText(text, innerHtml);
                });
            }
            else if (block.StartsWith("<ul"))
            {
                var items = Regex.Matches(block, @"<li\b[^>]*>(.*?)</li>");
                foreach (Match item in items)
                {
                    var liHtml = item.Groups[1].Value;
                    col.Item().PaddingBottom(2).Row(row =>
                    {
                        row.AutoItem().PaddingRight(6).Text("•").FontSize(10.5f).FontColor("#1E293B");
                        row.RelativeItem().Text(text =>
                        {
                            RenderRichText(text, liHtml);
                        });
                    });
                }
            }
        }
    }

    private void RenderRichText(TextDescriptor text, string content)
    {
        var matches = Regex.Matches(content, @"(<strong\b[^>]*>.*?</strong>|<b\b[^>]*>.*?</b>|<em\b[^>]*>.*?</em>|<i\b[^>]*>.*?</i>|[^<]+)");
        foreach (Match match in matches)
        {
            var textSegment = match.Value;
            if (textSegment.StartsWith("<strong>") || textSegment.StartsWith("<b>"))
            {
                var innerText = textSegment.Replace("<strong>", "").Replace("</strong>", "").Replace("<b>", "").Replace("</b>", "");
                text.Span(innerText).Bold().FontSize(10.5f).FontColor("#0F172A").LineHeight(1.5f);
            }
            else if (textSegment.StartsWith("<em>") || textSegment.StartsWith("<i>"))
            {
                var innerText = textSegment.Replace("<em>", "").Replace("</em>", "").Replace("<i>", "").Replace("</i>", "");
                text.Span(innerText).Italic().FontSize(10.5f).FontColor("#1E293B").LineHeight(1.5f);
            }
            else
            {
                text.Span(textSegment).FontSize(10.5f).FontColor("#1E293B").LineHeight(1.5f);
            }
        }
    }

    private string ReplacePlaceholders(string text, Dictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var result = text;
        foreach (var kvp in placeholders)
        {
            result = result.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
