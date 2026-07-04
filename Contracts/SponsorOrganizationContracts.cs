using System;
using System.ComponentModel.DataAnnotations;

namespace LMS.Api.Contracts;

public record SponsorOrganizationDto(
    Guid Id,
    string Name,
    string Code,
    string? Email,
    string? Phone,
    bool IsActive);

public record CreateSponsorOrganizationRequest(
    [Required] string Name,
    [Required] string Code,
    string? Email,
    string? Phone,
    bool IsActive = true);

public record UpdateSponsorOrganizationRequest(
    [Required] string Name,
    [Required] string Code,
    string? Email,
    string? Phone,
    bool IsActive);

public record GetSponsorsRequest(
    bool? ActiveOnly);
