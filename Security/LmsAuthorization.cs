namespace LMS.Api.Security;

public static class LmsRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string ViceChancellor = "VC";
    public const string Dean = "Dean";
    public const string Lecturer = "Lecturer";
    public const string Adviser = "Adviser";
    public const string Student = "Student";
    public const string Registrar = "Registrar";
    public const string Finance = "Finance";
    public const string HOD = "HOD";
    public const string Guest = "Guest";
    public const string Alumni = "Alumni";
    public const string Parent = "Parent";
    public const string AdmissionOfficer = "AdmissionOfficer";
    public const string AcademicAdmin = "AcademicAdmin";

}

public static class LmsPolicies
{
    public const string Management = "Management";
    public const string AcademicStaff = "AcademicStaff";
    public const string TeachingStaff = "TeachingStaff";
    public const string StudentOnly = "StudentOnly";
    public const string StaffOnly = "StaffOnly";
    public const string CourseManagement = "CourseManagement";
    public const string AcademicManagement = "AcademicManagement";
    public const string AdmissionsManagement = "AdmissionsManagement";
}

public static class LmsAuthorizationExtensions
{
    public static IServiceCollection AddLmsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Do not set FallbackPolicy to allow anonymous for endpoints without explicit auth requirements
            // Endpoints should opt-in to authentication via [AllowAnonymous(false)] or policies

            options.AddPolicy(
                LmsPolicies.Management,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Registrar));

            options.AddPolicy(
                LmsPolicies.AcademicStaff,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer, LmsRoles.Adviser, LmsRoles.HOD));

            options.AddPolicy(
                LmsPolicies.TeachingStaff,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.Lecturer));

            options.AddPolicy(
                LmsPolicies.StudentOnly,
                policy => policy.RequireRole(LmsRoles.Student));

            options.AddPolicy(
                LmsPolicies.StaffOnly,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer, LmsRoles.Adviser, LmsRoles.Registrar, LmsRoles.AcademicAdmin, LmsRoles.HOD));

            options.AddPolicy(
                LmsPolicies.CourseManagement,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer, LmsRoles.AcademicAdmin));

            options.AddPolicy(
                LmsPolicies.AcademicManagement,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.AcademicAdmin));

            options.AddPolicy(
                LmsPolicies.AdmissionsManagement,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.Registrar, LmsRoles.AdmissionOfficer));
        });

        return services;
    }
}
