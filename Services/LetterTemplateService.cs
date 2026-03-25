using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class LetterTemplateService(LmsDbContext dbContext) : ILetterTemplateService
{
    public async Task<LetterTemplateResponse> SaveTemplateAsync(SaveLetterTemplateRequest request)
    {
        var existing = await dbContext.LetterTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == request.TemplateType);

        if (existing == null)
        {
            existing = new LetterTemplate
            {
                TemplateType = request.TemplateType
            };
            dbContext.LetterTemplates.Add(existing);
        }

        existing.Name = request.Name;
        existing.HeaderTitle = request.HeaderTitle;
        existing.HeaderSubtitle = request.HeaderSubtitle;
        existing.HeaderContact = request.HeaderContact;
        existing.HeaderDate = request.HeaderDate;
        existing.LogoBase64 = request.LogoBase64;
        existing.SignatureBase64 = request.SignatureBase64;
        existing.SectionsJson = request.SectionsJson;
        existing.IsDefault = request.IsDefault;
        existing.UpdatedAt = DateTime.UtcNow;

        if (existing.IsDefault)
        {
            // Unset other defaults for same type or globally? Let's say per type for now.
            var others = await dbContext.LetterTemplates
                .Where(t => t.Id != existing.Id && t.TemplateType == existing.TemplateType)
                .ToListAsync();
            foreach (var other in others) other.IsDefault = false;
        }

        await dbContext.SaveChangesAsync();

        return MapToResponse(existing);
    }

    public async Task<LetterTemplateResponse?> GetTemplateByTypeAsync(string templateType)
    {
        var template = await dbContext.LetterTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == templateType && t.IsDefault)
            ?? await dbContext.LetterTemplates
            .FirstOrDefaultAsync(t => t.TemplateType == templateType);

        return template != null ? MapToResponse(template) : null;
    }

    public async Task<IEnumerable<LetterTemplateResponse>> GetAllTemplatesAsync()
    {
        var templates = await dbContext.LetterTemplates
            .OrderBy(t => t.Name)
            .ToListAsync();

        return templates.Select(MapToResponse);
    }

    private static LetterTemplateResponse MapToResponse(LetterTemplate t)
    {
        return new LetterTemplateResponse(
            t.Id,
            t.Name,
            t.TemplateType,
            t.HeaderTitle,
            t.HeaderSubtitle,
            t.HeaderContact,
            t.HeaderDate,
            t.LogoBase64,
            t.SignatureBase64,
            t.SectionsJson,
            t.IsDefault
        );
    }
}
