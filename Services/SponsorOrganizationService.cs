using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Services;

public sealed class SponsorOrganizationService(LmsDbContext db) : ISponsorOrganizationService
{
    public async Task<SponsorOrganizationDto> CreateSponsorAsync(CreateSponsorOrganizationRequest req)
    {
        var sponsor = new SponsorOrganization
        {
            Name = req.Name,
            Code = req.Code,
            Email = req.Email,
            Phone = req.Phone,
            IsActive = req.IsActive
        };

        db.SponsorOrganizations.Add(sponsor);
        await db.SaveChangesAsync();

        return MapToDto(sponsor);
    }

    public async Task<SponsorOrganizationDto?> UpdateSponsorAsync(Guid id, UpdateSponsorOrganizationRequest req)
    {
        var sponsor = await db.SponsorOrganizations.FindAsync(id);
        if (sponsor == null) return null;

        sponsor.Name = req.Name;
        sponsor.Code = req.Code;
        sponsor.Email = req.Email;
        sponsor.Phone = req.Phone;
        sponsor.IsActive = req.IsActive;

        await db.SaveChangesAsync();

        return MapToDto(sponsor);
    }

    public async Task<IEnumerable<SponsorOrganizationDto>> GetSponsorsAsync(bool? activeOnly = null)
    {
        var query = db.SponsorOrganizations.AsQueryable();

        if (activeOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == activeOnly.Value);
        }

        var sponsors = await query.OrderBy(s => s.Name).ToListAsync();
        return sponsors.Select(MapToDto);
    }

    public async Task<SponsorOrganizationDto?> GetSponsorByIdAsync(Guid id)
    {
        var sponsor = await db.SponsorOrganizations.FindAsync(id);
        return sponsor == null ? null : MapToDto(sponsor);
    }

    private static SponsorOrganizationDto MapToDto(SponsorOrganization s) => new(
        s.Id, s.Name, s.Code, s.Email, s.Phone, s.IsActive);
}
