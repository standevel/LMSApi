namespace LMS.Api.Security;

public static class LmsRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string ViceChancellor = "VC";
    public const string Dean = "Dean";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";
    public const string Registrar = "Registrar";
    public const string Finance = "Finance";
    public const string HOD = "HOD";
    public const string Guest = "Guest";
    public const string Alumni = "Alumni";

}

public static class LmsPolicies
{
    public const string Management = "Management";
    public const string AcademicStaff = "AcademicStaff";
    public const string TeachingStaff = "TeachingStaff";
    public const string StudentOnly = "StudentOnly";
    public const string StaffOnly = "StaffOnly";
    public const string CourseManagement = "CourseManagement";
}

public static class LmsAuthorizationExtensions
{
    public static IServiceCollection AddLmsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = options.DefaultPolicy;

            options.AddPolicy(
                LmsPolicies.Management,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Registrar));

            options.AddPolicy(
                LmsPolicies.AcademicStaff,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer));

            options.AddPolicy(
                LmsPolicies.TeachingStaff,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.Lecturer));

            options.AddPolicy(
                LmsPolicies.StudentOnly,
                policy => policy.RequireRole(LmsRoles.Student));

            options.AddPolicy(
                LmsPolicies.StaffOnly,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer, LmsRoles.Registrar));

            options.AddPolicy(
                LmsPolicies.CourseManagement,
                policy => policy.RequireRole(LmsRoles.SuperAdmin, LmsRoles.Admin, LmsRoles.ViceChancellor, LmsRoles.Dean, LmsRoles.Lecturer));
        });

        return services;
    }
}
