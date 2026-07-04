using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ISponsorOrganizationService
{
    Task<SponsorOrganizationDto> CreateSponsorAsync(CreateSponsorOrganizationRequest req);
    Task<SponsorOrganizationDto?> UpdateSponsorAsync(Guid id, UpdateSponsorOrganizationRequest req);
    Task<IEnumerable<SponsorOrganizationDto>> GetSponsorsAsync(bool? activeOnly = null);
    Task<SponsorOrganizationDto?> GetSponsorByIdAsync(Guid id);
}
