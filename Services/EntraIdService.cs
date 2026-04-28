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
        _domain = "wigweuniversity.edu.ng";

        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        _logger.LogInformation("Initializing Entra ID Service with TenantId: {TenantId}, ClientId: {ClientId}, Domain: {Domain}",
            tenantId, clientId, _domain);

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException("AzureAd TenantId, ClientId, or ClientSecret is not configured");
        }

        try
        {
            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret, options);
            _graphClient = new GraphServiceClient(clientSecretCredential);
            _logger.LogInformation("Entra ID Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Entra ID Service");
            throw;
        }
    }

    public async Task<(string EntraObjectId, string OfficialEmail, string? TemporaryPassword, bool IsExisting)> CreateStudentAccountAsync(AdmissionApplication application)
    {
        _logger.LogInformation("[ENTRA-CREATE-START] Starting Entra ID account creation for application {ApplicationId}", application.Id);
        
        var firstName = application.FirstName ?? "student";
        var lastName = application.LastName ?? "wigwe";
        var yearSuffix = "25"; // Hardcoded for 2025/2026 session as per request
        var officialEmail = $"{firstName.ToLower()}.{lastName.ToLower()}{yearSuffix}.test@{_domain}";

        _logger.LogInformation("[ENTRA-CREATE-PREP] Prepared user details: Email={Email}, FirstName={FirstName}, LastName={LastName}, Domain={Domain}",
            officialEmail, firstName, lastName, _domain);
     
    
        // Check if user already exists in Entra ID (Idempotency check)
        _logger.LogInformation("[ENTRA-IDEMPOTENCY] Checking if user already exists: {Email}", officialEmail);
        try
        {
             var existingUsers = await _graphClient.Users
                .GetAsync(requestConfiguration => 
                {
                    requestConfiguration.QueryParameters.Filter = $"userPrincipalName eq '{officialEmail}'";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "userPrincipalName" };
                });
            
            if (existingUsers?.Value?.Count > 0)
            {
                var existingUser = existingUsers.Value[0];
                _logger.LogInformation("[ENTRA-IDEMPOTENCY] User already exists in Entra ID: Id={UserId}, Email={Email}. Returning existing account.",
                    existingUser.Id, officialEmail);
                return (EntraObjectId: existingUser.Id ?? string.Empty, OfficialEmail: officialEmail, TemporaryPassword: (string?)null, IsExisting: true);
            }
            
            _logger.LogInformation("[ENTRA-IDEMPOTENCY] User does not exist. Proceeding with new account creation.");
        }
        catch (Exception checkEx)
        {
            _logger.LogWarning(checkEx, "[ENTRA-IDEMPOTENCY] Error checking for existing user, proceeding with creation attempt: {Email}", officialEmail);
        }
        
        var tempPassword = GenerateTemporaryPassword();
        _logger.LogInformation("[ENTRA-CREATE-PREP] Generated temporary password with length: {PasswordLength}", tempPassword?.Length ?? 0);

        var user = new User
        {
            DisplayName = $"{firstName} {application.MiddleName} {lastName}".Trim(),
            GivenName = firstName,
            Surname = lastName,
            UserPrincipalName = officialEmail,
            MailNickname = $"{firstName.ToLower()}.{lastName.ToLower()}{yearSuffix}.test",
            AccountEnabled = true,
            PasswordProfile = new PasswordProfile
            {
                ForceChangePasswordNextSignIn = true,
                Password = tempPassword
            }
        };

        try
        {
            _logger.LogInformation("[ENTRA-GRAPH-CALL] Calling Microsoft Graph API to create user with UPN: {UPN}", officialEmail);
            _logger.LogInformation("[ENTRA-GRAPH-CALL] Graph client initialized: {GraphClientInitialized}", _graphClient != null);
            
            var createdUser = await _graphClient.Users.PostAsync(user);
            if (createdUser == null)
            {
                _logger.LogError("[ENTRA-GRAPH-ERROR] Entra ID user creation returned null for {Email}", officialEmail);
                throw new InvalidOperationException($"Failed to create Entra ID user: null response from Graph API for {officialEmail}");
            }
            var userId = createdUser.Id ?? string.Empty;

            _logger.LogInformation("[ENTRA-GRAPH-RESULT] Graph API response received. UserId={UserId}, HasId={HasId}",
                userId, !string.IsNullOrEmpty(userId));

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("[ENTRA-GRAPH-ERROR] Entra ID user creation returned null or empty ID for {Email}", officialEmail);
                throw new InvalidOperationException($"Failed to create Entra ID user: no ID returned from Graph API");
            }

            _logger.LogInformation("[ENTRA-CREATE-SUCCESS] Successfully created Entra ID user: Id={UserId}, Email={Email}", userId, officialEmail);

            return (EntraObjectId: userId, OfficialEmail: officialEmail, TemporaryPassword: tempPassword, IsExisting: false);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError odataEx)
        {
            _logger.LogError(odataEx, "[ENTRA-GRAPH-ODATA-ERROR] OData error creating Entra ID user for {Email}. Code: {ErrorCode}, Message: {ErrorMessage}",
                officialEmail, odataEx.Error?.Code, odataEx.Error?.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENTRA-GRAPH-ERROR] Error creating Entra ID user for {Email}. Exception type: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                officialEmail, ex.GetType().Name, ex.Message, ex.StackTrace);
            throw;
        }
        finally
        {
            _logger.LogInformation("[ENTRA-CREATE-END] Finished Entra ID account creation attempt for {Email}", officialEmail);
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
