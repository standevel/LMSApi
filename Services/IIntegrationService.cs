using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IIntegrationService
{
    // Placeholder for integration functionality
    // In a real implementation, this would have methods for integrating with external systems
    Task<ErrorOr<Deleted>> TestExternalSystemConnectionAsync(string systemType, CancellationToken ct = default);
    Task<ErrorOr<List<ExternalSystemInfoDto>>> GetAvailableIntegrationsAsync(CancellationToken ct = default);
}

public record ExternalSystemInfoDto(
    string SystemType,
    string SystemName,
    bool IsConfigured,
    DateTime? LastSyncTime);

public record TestExternalSystemConnectionRequest(
    string SystemType,
    string ConnectionString);