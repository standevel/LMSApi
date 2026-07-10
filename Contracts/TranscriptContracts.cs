using System;

namespace LMS.Api.Contracts;

public record SystemTranscriptConfigurationDto(
    Guid Id,
    bool ChargeForTranscripts,
    decimal OfficialTranscriptFee,
    DateTime UpdatedAt);

public record UpdateSystemTranscriptConfigurationRequest(
    bool? ChargeForTranscripts,
    decimal? OfficialTranscriptFee);
