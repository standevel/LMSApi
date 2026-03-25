using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LMS.Api.Data.Entities;

namespace LMS.Api.Services;

public sealed class EntraIdService : IActiveDirectoryService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<EntraIdService> _logger;
    private readonly string _domain;

    public EntraIdService(IConfiguration configuration, ILogger<EntraIdService> logger)
    {
        _logger = logger;
        _domain = configuration["AzureAd:Domain"] ?? "wigweuniversity.edu.ng";
        
        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
        _graphClient = new GraphServiceClient(clientSecretCredential);
    }

    public async Task<(string OfficialEmail, string TemporaryPassword)> CreateStudentAccountAsync(AdmissionApplication application)
    {
        var (firstName, lastName) = ParseName(application.StudentName);
        var yearSuffix = "25"; // Hardcoded for 2025/2026 session as per request
        var officialEmail = $"{firstName.ToLower()}.{lastName.ToLower()}{yearSuffix}@{_domain}";
        var tempPassword = GenerateTemporaryPassword();

        var user = new User
        {
            DisplayName = application.StudentName,
            GivenName = firstName,
            Surname = lastName,
            UserPrincipalName = officialEmail,
            MailNickname = $"{firstName.ToLower()}.{lastName.ToLower()}{yearSuffix}",
            AccountEnabled = true,
            PasswordProfile = new PasswordProfile
            {
                ForceChangePasswordNextSignIn = true,
                Password = tempPassword
            }
        };

        try
        {
            _logger.LogInformation("Creating Entra ID user: {UPN}", officialEmail);
            var createdUser = await _graphClient.Users.PostAsync(user);
            _logger.LogInformation("Successfully created Entra ID user: {Id}", createdUser?.Id);
            
            return (officialEmail, tempPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Entra ID user for {Email}", officialEmail);
            throw;
        }
    }

    private (string First, string Last) ParseName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return ("student", "wigwe");
        
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (parts[0], "Student");
        
        return (parts[0], parts[^1]); // First and last
    }

    private string GenerateTemporaryPassword()
    {
        // Simple temporary password generation
        return "Wigwe!" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
