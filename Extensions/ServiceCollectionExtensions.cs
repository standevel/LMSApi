using FastEndpoints;
using FastEndpoints.Swagger;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using LMS.Api.Data.Repositories;
using LMS.Api.Security;
using LMS.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Text;

namespace LMS.Api.Extensions; 
public static class ServiceCollectionExtensions
{
    public const string LocalJwtScheme = "LocalJwt";
    public const string CompositeJwtScheme = "CompositeJwt";
    public const string FrontendCorsPolicy = "FrontendCors";
    private static readonly Assembly apiAssembly = typeof(ServiceCollectionExtensions).Assembly;

    public static IServiceCollection AddApplicationCore(this IServiceCollection services, IWebHostEnvironment environment = null!)
    {
        services.AddFastEndpoints(options =>
        {
            options.Assemblies = [apiAssembly];
            options.DisableAutoDiscovery = false;
        });

        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddHttpContextAccessor();
        services.AddSignalR();
        services.AddControllers()
            .AddNewtonsoftJson(o => o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.MaxDepth = 256;
            });

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20 MB max for file uploads
        });

        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB max request body
        });

        // services.SwaggerDocument(opts =>
        // {
        //     opts.DocumentSettings = document =>
        //     {
        //         document.DocumentName = "v1";
        //         document.Title = "LMS API";
        //         document.Version = "v1";
        //     };
        // });
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        return services;
    } 
    public static IServiceCollection AddApplicationDatabase(this IServiceCollection services, string connectionString, bool ignorePendingModelChangesWarning = false)
    {
        services.AddDbContext<LmsDbContext>(options =>
        {
            if (ignorePendingModelChangesWarning)
            {
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            }

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

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;

            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (IPAddress.TryParse(proxy, out var proxyAddress))
                {
                    options.KnownProxies.Add(proxyAddress);
                }
            }

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
            {
                var parts = network.Split('/', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var prefix) &&
                    int.TryParse(parts[1], out var prefixLength))
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
                }
            }
        });

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

                policyBuilder.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
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
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAcademicProgramService, AcademicProgramService>();
        services.AddScoped<IAcademicSessionService, AcademicSessionService>();
        services.AddScoped<ICurriculumService, CurriculumService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IFacultyService, FacultyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IGradebookService, GradebookService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IAssignmentGroupService, AssignmentGroupService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IBulkOperationService, BulkOperationService>();
        services.AddScoped<IDiscussionService, DiscussionService>();
        services.AddScoped<IIntegrationService, IntegrationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IParentPortalService, ParentPortalService>();
        services.AddScoped<IGuardianProvisioningService, GuardianProvisioningService>();
        services.AddScoped<IPrerequisiteValidationService, PrerequisiteValidationService>();
        services.AddScoped<IProctoringService, ProctoringService>();
        services.AddScoped<IQuestionBankService, QuestionBankService>();
        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IAdmissionService, AdmissionService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IAdviserService, AdviserService>();
        services.AddScoped<IProgramSwitchService, ProgramSwitchService>();
        services.AddHttpClient<IEmailService, BrevoEmailService>();
        services.AddScoped<IActiveDirectoryService, EntraIdService>();
        services.AddScoped<IPdfService, OfferLetterPdfService>();
        services.AddScoped<ILetterTemplateService, LetterTemplateService>();

// Fee Management
         services.AddScoped<IFeeService, FeeService>();
         services.AddHttpClient<PaystackService>();
         services.AddHttpClient<HydrogenService>();

         // Timetable Management
        services.AddScoped<ITimetableService, TimetableService>();
        services.AddScoped<ILectureSessionService, LectureSessionService>();
        services.AddScoped<ISessionManagementService, SessionManagementService>();
        services.AddScoped<ITeamsMeetingService, TeamsMeetingService>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();

        // Admission enhancement services
        services.AddScoped<ICreditTransferService, CreditTransferService>();
        services.AddScoped<IGradeConversionService, GradeConversionService>();
        services.AddScoped<ICourseEquivalencyService, CourseEquivalencyService>();
        services.AddScoped<ICredentialEvaluationService, CredentialEvaluationService>();

        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ILocalAuthService, LocalAuthService>();
        services.AddScoped<IAdminAuthzService, AdminAuthzService>();
        services.AddScoped<IRateLimitingService, RateLimitingService>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IStudentBulkImportService, StudentBulkImportService>();
        services.AddScoped<IDbInitializer, DbInitializer>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

        // Reporting & Analytics (Phase 3)
        services.AddScoped<IGpaCalculationService, GpaCalculationService>();
        services.AddScoped<ITranscriptGenerationService, TranscriptGenerationService>();
        services.AddScoped<IDegreeAuditService, DegreeAuditService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IReportSchedulerService, ReportSchedulerService>();

        // Student Management
        services.AddScoped<IStudentService, StudentService>();

        // Background Services
        services.AddHostedService<NotificationBackgroundService>();

        // Course Catalog Import — must be Singleton so in-memory preview dictionary survives across requests
        services.AddSingleton<ICourseCatalogImportService, CourseCatalogImportService>();

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
                    if (string.IsNullOrEmpty(authorization))
                    {
                        // Return LocalJwtScheme; its handler will gracefully handle missing tokens.
                        return LocalJwtScheme;
                    }

                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return LocalJwtScheme;
                    }

                    var token = authorization["Bearer ".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return LocalJwtScheme;
                    }

                    var jwtHandler = new JwtSecurityTokenHandler();
                    if (!jwtHandler.CanReadToken(token))
                    {
                        return LocalJwtScheme;
                    }

                    var jwt = jwtHandler.ReadJwtToken(token);
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

public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
     {
         // Email and external payment configurations are already registered via
         // AddHttpClient in AddApplicationSecurity. This method is reserved for
         // additional external service configurations if needed.
         return services;
     }
}
