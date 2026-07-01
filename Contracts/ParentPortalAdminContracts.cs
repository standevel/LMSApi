using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

public record SystemParentPortalConfigurationDto(
    bool AutoCreateGuardianAccountsOnStudentCreation,
    bool SendGuardianInvitationEmail,
    string DefaultRelationship);

public record UpdateSystemParentPortalConfigurationRequest(
    bool AutoCreateGuardianAccountsOnStudentCreation,
    bool SendGuardianInvitationEmail,
    string DefaultRelationship);

public record ProvisionGuardianRequest(
    bool? SendInvitationEmail = null);

public record ProvisionGuardianBatchRequest(
    IReadOnlyList<Guid>? StudentIds,
    bool AllEligible,
    Guid? SessionId,
    Guid? ProgramId,
    Guid? LevelId,
    string? Status,
    bool? SendInvitationEmail = null);

public record ProvisionGuardianResultDto(
    Guid StudentId,
    string StudentName,
    string? StudentNumber,
    string? GuardianEmail,
    string Status,
    string Message,
    Guid? ParentGuardianId,
    Guid? ParentStudentLinkId,
    bool CreatedUser,
    bool CreatedGuardian,
    bool CreatedLink);

public record ProvisionGuardianBatchResponse(
    int Total,
    int CreatedAccounts,
    int LinkedExistingAccounts,
    int AlreadyLinked,
    int Skipped,
    int Failed,
    IReadOnlyList<ProvisionGuardianResultDto> Results);
