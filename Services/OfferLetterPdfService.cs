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
            container.Page(page =>
            {
                 page.Size(PageSizes.A4);
                 page.Margin(0.85f, Unit.Inch);
                 page.PageColor(Colors.White);
                 page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                page.Content().Column(col =>
                {
                    // HEADER (Only on first page as it's at the top of Content)
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

                         row.AutoItem().AlignRight().Column(dateCol =>
                         {
                             dateCol.Item().Text("DATE").FontSize(8).Bold().FontColor("#94A3B8").LetterSpacing(0.1f);
                             var displayDate = !string.IsNullOrEmpty(template?.HeaderDate) 
                                 ? template.HeaderDate 
                                 : DateTime.UtcNow.ToString("MMMM dd, yyyy");
                            dateCol.Item().Text(displayDate).FontSize(11).Bold().FontColor("#1E293B");
                        });
                    });

                    // Recipient Section
                    col.Item().PaddingBottom(30).Column(c =>
                    {
                        c.Item().Text("ADMISSION OFFER TO:").FontSize(8).Bold().FontColor("#94A3B8").LetterSpacing(0.1f);
                        c.Item().Text((application.StudentName ?? "APPLICANT").ToUpper()).FontSize(14).Bold().FontColor("#1E293B");
                        
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

                                if (!string.IsNullOrEmpty(address))
                                    c.Item().PaddingTop(2).Text(address).FontSize(10).FontColor("#475569");
                                
                                var cityState = string.Join(", ", new[] { city, state, country }.Where(x => !string.IsNullOrEmpty(x)));
                                if (!string.IsNullOrEmpty(cityState))
                                    c.Item().Text(cityState).FontSize(10).FontColor("#475569");
                            }
                            catch { /* Skip address */ }
                        }

                        c.Item().PaddingTop(5).Text($"EMAIL: {application.StudentEmail ?? "N/A"}").FontSize(9).FontColor("#64748B");
                        c.Item().Text($"APPLICATION ID: {application.ApplicationNumber ?? "N/A"}").FontSize(9).FontColor("#64748B");
                    });

                    // Dynamic or Fallback Content
                    if (template != null && !string.IsNullOrEmpty(template.SectionsJson))
                    {
                        try
                        {
                            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var sections = System.Text.Json.JsonSerializer.Deserialize<List<LetterSectionDto>>(template.SectionsJson, options);
                            if (sections != null && sections.Any())
                            {
                                foreach (var section in sections.Where(s => s.IsVisible))
                                {
                                    RenderSection(col, section, application, template.SignatureBase64);
                                }
                            }
                            else
                            {
                                RenderFallbackContent(col, application);
                            }
                        }
                        catch (Exception ex)
                        {
                             col.Item().Text($"Content Rendering Error: {ex.Message}").FontColor(Colors.Red.Medium).FontSize(8);
                             RenderFallbackContent(col, application);
                        }
                    }
                    else
                    {
                        RenderFallbackContent(col, application);
                    }
                });

                page.Footer().Column(fcol => {
                    // Decorative Bottom Bar
                    fcol.Item().Height(8).Row(row => {
                        row.RelativeItem().Background("#10B981"); // Teal-ish (Wigwe Teal)
                        row.RelativeItem().Background("#059669"); // Green (Wigwe Green)
                        row.RelativeItem().Background("#0F172A"); // Dark Blue / Slate 900
                        row.RelativeItem().Background("#D4AF37"); // Gold
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
        col.Item().PaddingBottom(15).Text($"Dear {application.StudentName?.Split(' ')[0] ?? "Student"},").FontSize(11);
        col.Item().PaddingBottom(20).Text($"On behalf of the Admissions Committee, it is with great pleasure that I offer you admission to Wigwe University for the {application.AcademicProgram?.Name ?? "selected"} program, beginning in the Fall Semester of {application.CreatedAt.Year}.").LineHeight(1.5f).FontColor("#334155");
        col.Item().Text("Your application stood out among a highly competitive pool of candidates. We were particularly impressed by your academic record and your demonstrated passion for technological innovation.").LineHeight(1.5f).FontColor("#334155");
    }

    private void RenderSection(ColumnDescriptor col, LetterSectionDto section, AdmissionApplication app, string signatureBase64)
    {
        var rawContent = ReplacePlaceholders(section.Content ?? "", app);

        switch (section.Type)
        {
            case "subject":
                col.Item().PaddingBottom(25).Text(rawContent).FontSize(22).Bold().FontColor("#0F172A").LineHeight(1.1f);
                break;
            case "text":
                // Support whitespace-pre-line by treating each line separately if needed, 
                // or just using the LineHeight and relying on QuestPDF's text wrapping for the rest.
                col.Item().PaddingBottom(20).Text(rawContent).FontSize(11).LineHeight(1.6f).FontColor("#334155");
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
                    f.Item().PaddingTop(5).Text(rawContent).Italic().FontSize(11).LineHeight(1.5f).FontColor("#475569");
                });
                break;
            case "signature":
                col.Item().PaddingTop(20).Column(s => {
                    s.Item().Text("Sincerely,").FontSize(11).FontColor("#334155");
                    if (!string.IsNullOrEmpty(signatureBase64))
                    {
                         try 
                         { 
                            var bytes = Convert.FromBase64String(signatureBase64.Contains(",") ? signatureBase64.Split(',')[1] : signatureBase64);
                            s.Item().PaddingVertical(10).Height(60).Image(bytes); 
                         } catch { s.Item().Height(40); }
                    }
                    else
                    {
                        s.Item().Height(40);
                    }
                    s.Item().Text("THE REGISTRAR").FontSize(10).Bold().FontColor("#1E293B");
                    s.Item().Text("Wigwe University").FontSize(9).Bold().FontColor("#64748B");
                });
                break;
        }
    }

    private string ReplacePlaceholders(string text, AdmissionApplication app)
    {
        return text
            .Replace("{studentName}", app.StudentName)
            .Replace("{programName}", app.AcademicProgram?.Name ?? "Selected Program")
            .Replace("{year}", app.CreatedAt.Year.ToString())
            .Replace("{applicationNumber}", app.ApplicationNumber);
    }

    private record LetterSectionDto(string Id, string Type, string Title, string? Content, bool IsVisible);
}
