using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using LMS.Api.Data;
using LMS.Api.Services;
using Microsoft.Extensions.Configuration;

namespace LMS.Api.Services;

public class IntegrationService : BaseService, IIntegrationService
{
    private readonly LmsDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public IntegrationService(
        LmsDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IAuditService auditService) : base(auditService)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ErrorOr<Deleted>> TestExternalSystemConnectionAsync(string systemType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(systemType))
        {
            return Error.Validation("InvalidInput", "System type is required.");
        }

        var section = _configuration.GetSection($"Integrations:{systemType.ToUpperInvariant()}");
        if (!section.Exists())
        {
            return Error.NotFound("SystemType.NotConfigured", $"System type '{systemType}' is not configured.");
        }

        var baseUrl = section["BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                using var response = await client.GetAsync(baseUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return Error.Failure("Integration.ConnectionFailed", $"Integration returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                return Error.Failure("Integration.ConnectionFailed", ex.Message);
            }
        }
        else if (string.IsNullOrWhiteSpace(section["ConnectionString"]))
        {
            return Error.Validation("Integration.MissingConnectionDetails", $"Integration '{systemType}' requires BaseUrl or ConnectionString.");
        }

        return Result.Deleted;
    }

    public Task<ErrorOr<List<ExternalSystemInfoDto>>> GetAvailableIntegrationsAsync(CancellationToken ct = default)
    {
        var knownSystems = new Dictionary<string, string>
        {
            ["SIS"] = "Student Information System",
            ["LMS"] = "Learning Management System",
            ["HR"] = "Human Resources System",
            ["FINANCE"] = "Financial Management System",
            ["LIBRARY"] = "Library Management System"
        };

        var integrations = knownSystems
            .Select(system =>
            {
                var section = _configuration.GetSection($"Integrations:{system.Key}");
                var isConfigured = section.Exists() &&
                    (!string.IsNullOrWhiteSpace(section["BaseUrl"]) ||
                     !string.IsNullOrWhiteSpace(section["ConnectionString"]));

                DateTime? lastSync = DateTime.TryParse(section["LastSyncUtc"], out var parsed)
                    ? parsed
                    : null;

                return new ExternalSystemInfoDto(system.Key, system.Value, isConfigured, lastSync);
            })
            .ToList();

        return Task.FromResult<ErrorOr<List<ExternalSystemInfoDto>>>(integrations);
    }
}
