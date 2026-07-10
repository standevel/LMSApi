using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Data.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace LMS.Api.Services;

public sealed class OfferLetterPdfService(ILetterTemplateService templateService) : IPdfService
{
    public async Task<byte[]> GenerateOfferLetterAsync(AdmissionApplication application, string? templateType = "Undergraduate")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var template = await templateService.GetTemplateByTypeAsync(templateType ?? "Undergraduate");

        var document = Document.Create(container =>
        {
            // ── Page 1: Admission Offer Letter ────────────────────────────────
            container.Page(page =>
            {
                 page.Size(PageSizes.A4);
                 page.Margin(0.6f, Unit.Inch);
                 page.PageColor(Colors.White);
                 page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                 page.Content().Column(col =>
                 {
                     if (template != null && !string.IsNullOrEmpty(template.SectionsJson))
                     {
                         try
                         {
                             var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                             var sections = System.Text.Json.JsonSerializer.Deserialize<List<LetterSectionDto>>(template.SectionsJson, options);
                             if (sections != null && sections.Any())
                             {
                                 var hasHeader = sections.Any(s => s.Type == "header");
                                 var hasRecipient = sections.Any(s => s.Type == "recipient");
                                 if (!hasHeader) RenderStaticHeader(col, template);
                                 if (!hasRecipient) RenderStaticRecipient(col, application);
                                 foreach (var section in sections.Where(s => s.IsVisible))
                                     RenderSection(col, section, application, template);
                             }
                             else
                             {
                                 RenderStaticHeader(col, template);
                                 RenderStaticRecipient(col, application);
                                 RenderFallbackContent(col, application);
                             }
                         }
                         catch (Exception ex)
                         {
                             col.Item().Text($"Content Rendering Error: {ex.Message}").FontColor(Colors.Red.Medium).FontSize(8);
                             RenderStaticHeader(col, template);
                             RenderStaticRecipient(col, application);
                             RenderFallbackContent(col, application);
                         }
                     }
                     else
                     {
                         RenderStaticHeader(col, template);
                         RenderStaticRecipient(col, application);
                         RenderFallbackContent(col, application);
                     }
                 });

                page.Footer().Column(fcol => {
                    fcol.Item().Height(5).Row(row => {
                        row.RelativeItem().Background("#10B981");
                        row.RelativeItem().Background("#059669");
                        row.RelativeItem().Background("#0F172A");
                        row.RelativeItem().Background("#D4AF37");
                    });
                    fcol.Item().PaddingVertical(10).AlignCenter().Text(x =>
                    {
                        x.Span("CONFIDENTIAL ADMISSION DOCUMENT | PAGE ").FontSize(8).FontColor("#94A3B8");
                        x.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    });
                });
            });

            // ── Page 2: Advance Payment Memorandum ────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.7f, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                page.Content().Column(col =>
                {
                    // Logo / University Name header
                    col.Item().PaddingBottom(30).Row(headerRow =>
                    {
                        headerRow.RelativeItem().Column(c =>
                        {
                            if (template != null && !string.IsNullOrEmpty(template.LogoBase64))
                            {
                                try
                                {
                                    var bytes = Convert.FromBase64String(template.LogoBase64.Contains(",") ? template.LogoBase64.Split(',')[1] : template.LogoBase64);
                                    c.Item().Height(60).Image(bytes);
                                }
                                catch
                                {
                                    c.Item().Text("WIGWE UNIVERSITY").FontSize(22).Bold().FontColor("#0F172A");
                                }
                            }
                            else
                            {
                                c.Item().Text("WIGWE UNIVERSITY").FontSize(22).Bold().FontColor("#0F172A");
                            }
                        });
                    });

                    // Divider
                    col.Item().PaddingBottom(20).BorderBottom(1.5f).BorderColor("#D4AF37");

                    // Memo Title Block
                    col.Item().PaddingBottom(6).AlignCenter()
                        .Text("WIGWE UNIVERSITY MEMORANDUM").FontSize(13).Bold().FontColor("#0F172A").LetterSpacing(0.05f);

                    col.Item().PaddingBottom(4).AlignCenter()
                        .Text($"DATE: {DateTime.UtcNow:dd MMMM, yyyy}".ToUpper()).FontSize(11).Bold().FontColor("#1E293B");

                    col.Item().PaddingBottom(4).AlignCenter()
                        .Text("ATTENTION:").FontSize(11).Bold().FontColor("#334155");

                    col.Item().PaddingBottom(20).AlignCenter()
                        .Text("ALL WIGWE UNIVERSITY PARENTS AND INTENDING PARENTS")
                        .FontSize(12).Bold().FontColor("#0F172A");

                    col.Item().PaddingBottom(24).AlignCenter()
                        .Text("RE: ADVANCE PAYMENT OF TUITION FEES")
                        .FontSize(12).Bold().Underline().FontColor("#0F172A");

                    // Body Text
                    col.Item().PaddingBottom(18).Text(
                        "This directive is to all parents who desire to make advance payments of the tuition fees for their Children / Wards.")
                        .FontSize(11).FontColor("#334155").LineHeight(1.6f);

                    col.Item().PaddingBottom(8).Text("Please note as follows:").FontSize(11).Bold().FontColor("#1E293B");

                    // Numbered list
                    var memoPoints = new[]
                    {
                        "That the University account details are available on the WU website.",
                        "The bursary unit must be notified whenever such payments are made.",
                        "All such payments must be clearly given the narrative \"advance payment\".",
                        "Refund of such \"advance payments\" is done with 50% of the sum retained as administrative fees."
                    };

                    for (int i = 0; i < memoPoints.Length; i++)
                    {
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            row.AutoItem().PaddingRight(10).Text($"{i + 1}.").FontSize(11).Bold().FontColor("#334155");
                            row.RelativeItem().Text(memoPoints[i]).FontSize(11).FontColor("#334155").LineHeight(1.6f);
                        });
                    }

                    col.Item().PaddingTop(20).PaddingBottom(16).Text(
                        "For further information, please contact the Dean, Student Affairs, Students Welfare Officer.")
                        .FontSize(11).FontColor("#334155").LineHeight(1.6f);

                    col.Item().PaddingTop(8).Text("Bursary Unit").FontSize(11).Bold().FontColor("#1E293B");

                    // Contact footer block
                    col.Item().PaddingTop(40).BorderTop(1).BorderColor("#E2E8F0").PaddingTop(14).Row(footRow =>
                    {
                        footRow.RelativeItem().Column(fc =>
                        {
                            fc.Item().Text("Isiokpo, Rivers State").FontSize(9).FontColor("#64748B");
                            fc.Item().Text("T: +2348032006346").FontSize(9).FontColor("#64748B");
                            fc.Item().Text("E: contact@wigweuniversity.edu.ng").FontSize(9).FontColor("#64748B");
                            fc.Item().Text("E: bursary@wigweuniversity.edu.ng").FontSize(9).FontColor("#64748B");
                        });
                        footRow.RelativeItem().AlignCenter().Text("www.wigweuniversity.edu.ng").FontSize(9).FontColor("#64748B");
                        footRow.RelativeItem().AlignRight().Text("RC-7258338").FontSize(9).FontColor("#64748B");
                    });
                });

                page.Footer().Column(fcol =>
                {
                    fcol.Item().Height(5).Row(row =>
                    {
                        row.RelativeItem().Background("#10B981");
                        row.RelativeItem().Background("#059669");
                        row.RelativeItem().Background("#0F172A");
                        row.RelativeItem().Background("#D4AF37");
                    });
                    fcol.Item().PaddingVertical(10).AlignCenter().Text(x =>
                    {
                        x.Span("CONFIDENTIAL ADMISSION DOCUMENT | PAGE ").FontSize(8).FontColor("#94A3B8");
                        x.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
    private void RenderFallbackContent(ColumnDescriptor col, AdmissionApplication application)
    {
        col.Item().PaddingBottom(20).Text($"Subject: Official Offer of Admission - Fall {application.CreatedAt.Year}").FontSize(18).Bold().FontColor("#0F172A");
        col.Item().PaddingBottom(15).Text($"Dear {application.FirstName ?? "Student"},").FontSize(11);
        col.Item().PaddingBottom(20).Text($"On behalf of the Admissions Committee, it is with great pleasure that I offer you admission to Wigwe University for the {application.AcademicProgram?.Name ?? "selected"} program, beginning in the Fall Semester of {application.CreatedAt.Year}.").LineHeight(1.5f).FontColor("#334155");
        col.Item().Text("Your application stood out among a highly competitive pool of candidates. We were particularly impressed by your academic record and your demonstrated passion for technological innovation.").LineHeight(1.5f).FontColor("#334155");
    }

    private void RenderSection(ColumnDescriptor col, LetterSectionDto section, AdmissionApplication app, LetterTemplateResponse? template)
    {
        var rawContent = ReplacePlaceholders(section.Content ?? "", app);

        switch (section.Type)
        {
            case "header":
                RenderStaticHeader(col, template);
                break;
            case "recipient":
                RenderStaticRecipient(col, app);
                break;
            case "date":
                var dateStr = string.IsNullOrEmpty(rawContent) || rawContent == "{date}"
                    ? (!string.IsNullOrEmpty(template?.HeaderDate) ? template.HeaderDate : app.CreatedAt.ToString("MMMM dd, yyyy"))
                    : rawContent;
                col.Item().PaddingBottom(15).Text(dateStr).FontSize(11).Bold().FontColor("#1E293B");
                break;
            case "subject":
                col.Item().PaddingBottom(25).Text(rawContent).FontSize(20).Bold().FontColor("#0F172A").LineHeight(1.1f);
                break;
            case "text":
                RenderHtmlContent(col, rawContent);
                break;
            case "program_details":
                col.Item().PaddingBottom(30).Border(1).BorderColor("#F1F5F9").Background("#F8FAFC").Padding(20).Column(details =>
                {
                    details.Item().PaddingBottom(15).Text("PROGRAM OF STUDY").FontSize(9).Bold().FontColor("#D4AF37").LetterSpacing(0.1f);
                    
                    details.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Column(c => {
                            c.Item().Text("FACULTY / COLLEGE").FontSize(8).Bold().FontColor("#94A3B8");
                            c.Item().Text(app.Faculty?.Name ?? "N/A").FontSize(11).Bold().FontColor("#1E293B");
                        });
                        table.Cell().Column(c => {
                            c.Item().Text("ACADEMIC PROGRAM").FontSize(8).Bold().FontColor("#94A3B8");
                            c.Item().Text(app.AcademicProgram?.Name ?? "N/A").FontSize(11).Bold().FontColor("#1E293B");
                        });
                        table.Cell().PaddingTop(15).Column(c => {
                            c.Item().Text("RESUMPTION DATE").FontSize(8).Bold().FontColor("#94A3B8");
                            var start = app.AcademicSession?.StartDate.ToString("MMMM dd, yyyy") ?? "September 2026";
                            c.Item().Text(start).FontSize(11).Bold().FontColor("#1E293B");
                        });
                        table.Cell().PaddingTop(15).Column(c => {
                            c.Item().Text("ADMISSION CATEGORY").FontSize(8).Bold().FontColor("#94A3B8");
                            c.Item().Text(app.Persona ?? "Full-Time Undergraduate").FontSize(11).Bold().FontColor("#1E293B");
                        });
                    });
                });
                break;
            case "financial_aid":
                col.Item().PaddingBottom(25).BorderLeft(3).BorderColor("#D4AF37").PaddingLeft(15).Column(f => {
                    f.Item().Text("FINANCIAL SUPPORT & SCHOLARSHIP").FontSize(9).Bold().FontColor("#64748B").LetterSpacing(0.05f);
                    f.Item().PaddingTop(5).Column(fc => {
                        RenderHtmlContent(fc, rawContent);
                    });
                });
                break;
            case "signature":
                col.Item().PaddingTop(20).Column(s => {
                    s.Item().Text("Sincerely,").FontSize(11).FontColor("#334155");
                    if (template != null && !string.IsNullOrEmpty(template.SignatureBase64))
                    {
                         try 
                         { 
                            var bytes = Convert.FromBase64String(template.SignatureBase64.Contains(",") ? template.SignatureBase64.Split(',')[1] : template.SignatureBase64);
                            s.Item().PaddingVertical(10).Height(60).Image(bytes); 
                         } catch { s.Item().Height(40); }
                    }
                    else
                    {
                        s.Item().Height(40);
                    }
                    
                    if (template != null && !string.IsNullOrEmpty(template.SignatoryName))
                    {
                        s.Item().Text(template.SignatoryName).FontSize(10).Bold().FontColor("#1E293B");
                    }
                    
                    var signatoryPosition = template != null && !string.IsNullOrEmpty(template.SignatoryPosition)
                        ? template.SignatoryPosition
                        : "Registrar";
                    s.Item().Text(signatoryPosition.ToUpper()).FontSize(9).Bold().FontColor("#64748B");
                });
                break;
        }
    }

    private void RenderStaticHeader(ColumnDescriptor col, LetterTemplateResponse? template)
    {
        col.Item().PaddingBottom(30).Row(row =>
        {
            row.RelativeItem().Row(innerRow =>
            {
                if (template != null && !string.IsNullOrEmpty(template.LogoBase64))
                {
                    try 
                    { 
                        var bytes = Convert.FromBase64String(template.LogoBase64.Contains(",") ? template.LogoBase64.Split(',')[1] : template.LogoBase64);
                        innerRow.AutoItem().Height(70).Image(bytes); 
                    } catch { /* Fallback */ }
                }
                
                var headerTitle = template?.HeaderTitle ?? "WIGWE UNIVERSITY";
                var headerSubtitle = template?.HeaderSubtitle ?? "OFFICE OF ACADEMIC ADMISSIONS";
                var headerContact = template?.HeaderContact ?? "Rivers State, Nigeria • www.wigweuniversity.edu.ng";

                 innerRow.RelativeItem().PaddingLeft(10).Column(innerCol =>
                 {
                     innerCol.Item().PaddingTop(5).Text(headerTitle.ToUpper()).FontSize(20).Bold().FontColor("#0F172A");
                     innerCol.Item().Text(headerSubtitle.ToUpper()).FontSize(9).Bold().FontColor("#D4AF37").LetterSpacing(0.2f);
                     innerCol.Item().Text(headerContact).FontSize(8).FontColor("#64748B");
                 });
             });

             var hasDateSection = false;
             if (template != null && !string.IsNullOrEmpty(template.SectionsJson))
             {
                 try
                 {
                     var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                     var sections = System.Text.Json.JsonSerializer.Deserialize<List<LetterSectionDto>>(template.SectionsJson, options);
                     hasDateSection = sections?.Any(s => s.Type == "date" && s.IsVisible) ?? false;
                 }
                 catch {}
             }

             if (!hasDateSection)
             {
                 row.AutoItem().AlignRight().Column(dateCol =>
                 {
                     dateCol.Item().Text("DATE").FontSize(8).Bold().FontColor("#94A3B8").LetterSpacing(0.1f);
                     var displayDate = !string.IsNullOrEmpty(template?.HeaderDate) 
                         ? template.HeaderDate 
                         : DateTime.UtcNow.ToString("MMMM dd, yyyy");
                     dateCol.Item().Text(displayDate).FontSize(11).Bold().FontColor("#1E293B");
                 });
             }
        });
    }

    private void RenderStaticRecipient(ColumnDescriptor col, AdmissionApplication application)
    {
        col.Item().PaddingBottom(10).Column(c =>
        {
            c.Item().Text("ADMISSION OFFER TO:").FontSize(8).Bold().FontColor("#94A3B8").LetterSpacing(0.1f);
            var fullName = $"{application.FirstName} {application.MiddleName} {application.LastName}".Trim();
            c.Item().Text((fullName ?? "APPLICANT").ToUpper()).FontSize(14).Bold().FontColor("#1E293B");
            
            if (!string.IsNullOrEmpty(application.EmergencyContactJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(application.EmergencyContactJson);
                    var root = doc.RootElement;
                    var address = root.TryGetProperty("address", out var a) ? a.GetString() : null;
                    var city = root.TryGetProperty("city", out var ci) ? ci.GetString() : null;
                    var state = root.TryGetProperty("state", out var s) ? s.GetString() : null;
                    var country = root.TryGetProperty("country", out var co) ? co.GetString() : "Nigeria";

                   }
                catch { /* Skip address */ }
            }
        });
    }

    private void RenderHtmlContent(ColumnDescriptor col, string html)
    {
        // If it doesn't look like HTML (e.g. no <p> or <ul>), treat it as standard multiline text
        if (!html.Contains("<p>") && !html.Contains("<ul>") && !html.Contains("<li>") && !html.Contains("<ol>"))
        {
            var lines = html.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                col.Item().PaddingBottom(15).Text(text => {
                    text.Span(line).FontSize(11).FontColor("#334155").LineHeight(1.6f);
                });
            }
            return;
        }

        // Parse HTML block by block
        var blockMatches = System.Text.RegularExpressions.Regex.Matches(html, @"(<p\b[^>]*>.*?</p>|<ul\b[^>]*>.*?</ul>|<ol\b[^>]*>.*?</ol>)");
        
        foreach (System.Text.RegularExpressions.Match match in blockMatches)
        {
            var block = match.Value;
            if (block.StartsWith("<p"))
            {
                var innerHtml = System.Text.RegularExpressions.Regex.Replace(block, @"^<p\b[^>]*>", "").Replace("</p>", "");
                col.Item().PaddingBottom(15).Text(text => {
                    RenderRichText(text, innerHtml);
                });
            }
            else if (block.StartsWith("<ul"))
            {
                var items = System.Text.RegularExpressions.Regex.Matches(block, @"<li\b[^>]*>(.*?)</li>");
                foreach (System.Text.RegularExpressions.Match item in items)
                {
                    var liHtml = item.Groups[1].Value;
                    col.Item().PaddingBottom(5).Row(row => {
                        row.AutoItem().PaddingRight(8).Text("•").FontSize(11).FontColor("#334155").LineHeight(1.6f);
                        row.RelativeItem().Text(text => {
                            RenderRichText(text, liHtml);
                        });
                    });
                }
                col.Item().PaddingBottom(10);
            }
            else if (block.StartsWith("<ol"))
            {
                var items = System.Text.RegularExpressions.Regex.Matches(block, @"<li\b[^>]*>(.*?)</li>");
                int index = 1;
                foreach (System.Text.RegularExpressions.Match item in items)
                {
                    var liHtml = item.Groups[1].Value;
                    col.Item().PaddingBottom(5).Row(row => {
                        row.AutoItem().PaddingRight(8).Text($"{index}.").FontSize(11).FontColor("#334155").LineHeight(1.6f);
                        row.RelativeItem().Text(text => {
                            RenderRichText(text, liHtml);
                        });
                    });
                    index++;
                }
                col.Item().PaddingBottom(10);
            }
        }
    }

    private void RenderRichText(TextDescriptor text, string content)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"(<strong\b[^>]*>.*?</strong>|<b\b[^>]*>.*?</b>|<em\b[^>]*>.*?</em>|<i\b[^>]*>.*?</i>|[^<]+)");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var textSegment = match.Value;
            if (textSegment.StartsWith("<strong>") || textSegment.StartsWith("<b>"))
            {
                var innerText = textSegment.Replace("<strong>", "").Replace("</strong>", "").Replace("<b>", "").Replace("</b>", "");
                text.Span(innerText).Bold().FontSize(11).FontColor("#334155").LineHeight(1.6f);
            }
            else if (textSegment.StartsWith("<em>") || textSegment.StartsWith("<i>"))
            {
                var innerText = textSegment.Replace("<em>", "").Replace("</em>", "").Replace("i", "").Replace("</i>", "").Replace("<", "").Replace(">", "");
                text.Span(innerText).Italic().FontSize(11).FontColor("#334155").LineHeight(1.6f);
            }
            else
            {
                var cleanText = System.Text.RegularExpressions.Regex.Replace(textSegment, "<[^>]*>", "");
                if (!string.IsNullOrEmpty(cleanText))
                {
                    text.Span(cleanText).FontSize(11).FontColor("#334155").LineHeight(1.6f);
                }
            }
        }
    }

    private string ReplacePlaceholders(string text, AdmissionApplication app)
    {
        var fullName = $"{app.FirstName} {app.MiddleName} {app.LastName}".Trim();
        return text
            .Replace("{studentName}", fullName)
            .Replace("{programName}", app.AcademicProgram?.Name ?? "Selected Program")
            .Replace("{collegeName}", app.Faculty?.Name ?? "Selected College")
            .Replace("{session}", app.AcademicSession?.Name ?? "Selected Session")
            .Replace("{year}", app.CreatedAt.Year.ToString())
            .Replace("{date}", app.CreatedAt.ToString("MMMM dd, yyyy"))
            .Replace("{applicationNumber}", app.ApplicationNumber);
    }

    private record LetterSectionDto(string Id, string Type, string Title, string? Content, bool IsVisible);
}
