using FastEndpoints;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Services;
using LMS.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace LMS.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string LocalJwtScheme = "LocalJwt";
    public const string CompositeJwtScheme = "CompositeJwt";
    public const string FrontendCorsPolicy = "FrontendCors";

    public static IServiceCollection AddApplicationCore(this IServiceCollection services)
    {
        services.AddFastEndpoints();
        services.AddOpenApi();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        return services;
    }

    public static IServiceCollection AddApplicationDatabase(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LmsDbContext>(options =>
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
        });

        return services;
    }

    public static IServiceCollection AddApplicationSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policyBuilder =>
            {
                if (allowedOrigins.Length == 0)
                {
                    policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                policyBuilder.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<ApiReplayPreventionOptions>(configuration.GetSection("ApiReplayPrevention"));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IAcademicProgramRepository, AcademicProgramRepository>();
        services.AddScoped<IAcademicSessionRepository, AcademicSessionRepository>();
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IFacultyRepository, FacultyRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAcademicProgramService, AcademicProgramService>();
        services.AddScoped<IAcademicSessionService, AcademicSessionService>();
        services.AddScoped<ICurriculumService, CurriculumService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IFacultyService, FacultyService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAdmissionService, AdmissionService>();
        services.AddHttpClient<IEmailService, BrevoEmailService>();
        services.AddScoped<IActiveDirectoryService, EntraIdService>();
        services.AddScoped<IPdfService, OfferLetterPdfService>();
        services.AddScoped<ILetterTemplateService, LetterTemplateService>();

        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<IAdminAuthzService, AdminAuthzService>();
        services.AddScoped<IDbInitializer, DbInitializer>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CompositeJwtScheme;
                options.DefaultChallengeScheme = CompositeJwtScheme;
            })
            .AddPolicyScheme(CompositeJwtScheme, "Composite JWT Scheme", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    var token = authorization["Bearer ".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    var handler = new JwtSecurityTokenHandler();
                    if (!handler.CanReadToken(token))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    var jwt = handler.ReadJwtToken(token);
                    return string.Equals(jwt.Issuer, jwtSettings.Issuer, StringComparison.OrdinalIgnoreCase)
                        ? LocalJwtScheme
                        : JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(options =>
            {
                var azureAd = configuration.GetSection("AzureAd");
                var clientId = azureAd["ClientId"] ?? "";
                var instance = azureAd["Instance"] ?? "https://login.microsoftonline.com/";
                var tenantId = azureAd["TenantId"] ?? "";
                var audience = azureAd["Audience"] ?? "";
                var normalizedInstance = instance.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? instance
                    : $"https://{instance}";

                options.Authority = $"{normalizedInstance.TrimEnd('/')}/{tenantId}/v2.0";
                options.Audience = audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = new[]
                    {
                        $"https://sts.windows.net/{tenantId}/",
                        $"{normalizedInstance.TrimEnd('/')}/{tenantId}/v2.0"
                    },
                    ValidateAudience = true,
                    ValidAudiences = new[]
                    {
                        audience,
                        clientId
                    },
                    NameClaimType = "name",
                    RoleClaimType = "roles"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"[Auth Failed] {context.Exception.Message}");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        Console.WriteLine("[Auth Success] Token validated successfully.");
                        foreach (var claim in context.Principal?.Claims ?? [])
                        {
                            Console.WriteLine($"  Claim: {claim.Type} = {claim.Value}");
                        }
                        return Task.CompletedTask;
                    }
                };
            })
            .AddJwtBearer(LocalJwtScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "roles"
                };
            });

        services.AddLmsAuthorization();
        return services;
    }
}
