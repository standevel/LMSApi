using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICertificateService
{
    Task<ErrorOr<CertificateRequestDto>> CreateCertificateRequestAsync(Guid studentId, CreateCertificateRequestDto request, Guid requestedBy, CancellationToken ct = default);
    Task<ErrorOr<CertificateRequestDto>> ProcessCertificateRequestAsync(Guid requestId, Guid processedBy, bool bypassGraduationCheck = false, CancellationToken ct = default);
    Task<ErrorOr<CertificateRequestDto>> GetCertificateRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<ErrorOr<List<CertificateRequestDto>>> GetStudentCertificateRequestsAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<List<CertificateRequestDto>>> GetAllCertificateRequestsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ErrorOr<SystemCertificateConfigurationDto>> GetConfigurationAsync(CancellationToken ct = default);
    Task<ErrorOr<SystemCertificateConfigurationDto>> UpdateConfigurationAsync(UpdateSystemCertificateConfigurationRequest request, Guid userId, CancellationToken ct = default);
    Task<ErrorOr<CertificateVerificationDto>> VerifyCertificateAsync(string credentialId, CancellationToken ct = default);
    Task<ErrorOr<BatchProcessResultDto>> ProcessCertificateRequestsBatchAsync(List<Guid> requestIds, Guid processedBy, bool bypassGraduationCheck = false, CancellationToken ct = default);
}
