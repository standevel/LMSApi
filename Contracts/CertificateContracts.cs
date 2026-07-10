using System;
using LMS.Api.Data.Enums;

namespace LMS.Api.Contracts;

public sealed record CertificateRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    CertificateType CertificateType,
    CertificateStatus Status,
    string? DeliveryMethod,
    string? DeliveryEmail,
    decimal? FeeAmount,
    bool FeePaid,
    string? DocumentUrl,
    string CredentialId,
    string? ProcessorName,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public sealed record CreateCertificateRequestDto(
    Guid? StudentId,
    CertificateType CertificateType,
    string? DeliveryMethod = "Email",
    string? DeliveryEmail = null,
    string? Remarks = null
);

public sealed record ProcessCertificateRequestRequest(
    bool BypassGraduationCheck = false,
    string? ProcessorRemarks = null
);

public sealed record SystemCertificateConfigurationDto(
    Guid Id,
    bool ChargeForCertificates,
    decimal OfficialCertificateFee,
    string SignatoryName,
    string SignatoryPosition,
    string? SignatorySignatureBase64,
    string RegistrarName,
    string RegistrarPosition,
    string? RegistrarSignatureBase64,
    DateTime UpdatedAt
);

public sealed record UpdateSystemCertificateConfigurationRequest(
    bool? ChargeForCertificates = null,
    decimal? OfficialCertificateFee = null,
    string? SignatoryName = null,
    string? SignatoryPosition = null,
    string? SignatorySignatureBase64 = null,
    string? RegistrarName = null,
    string? RegistrarPosition = null,
    string? RegistrarSignatureBase64 = null
);

public sealed record CertificateVerificationDto(
    string CredentialId,
    string StudentName,
    string ProgramName,
    string DegreeClassification,
    DateTime IssueDate,
    bool IsVerified,
    string Status
);

public sealed record BatchProcessCertificateRequestsRequest(
    System.Collections.Generic.List<Guid> RequestIds,
    bool BypassGraduationCheck = false
);

public sealed record BatchProcessResultDto(
    int TotalProcessed,
    int SuccessCount,
    int FailureCount,
    System.Collections.Generic.List<BatchProcessErrorDto> Errors
);

public sealed record BatchProcessErrorDto(
    Guid RequestId,
    string ErrorMessage
);
