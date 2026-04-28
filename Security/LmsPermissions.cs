namespace LMS.Api.Security;

public static class LmsPermissions
{
    public const string AccessManage = "access.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string PermissionsManage = "permissions.manage";
    public const string CoursesManage = "courses.manage";
    public const string CoursesTeach = "courses.teach";
    public const string GradesSubmit = "grades.submit";
    public const string RecordsManage = "records.manage";
    public const string ReportsView = "reports.view";
    public const string EnrollmentsManage = "enrollments.manage";
    public const string ProfileView = "profile.view";

    public static readonly IReadOnlyList<string> All =
    [
        AccessManage,
        UsersManage,
        RolesManage,
        PermissionsManage,
        CoursesManage,
        CoursesTeach,
        GradesSubmit,
        RecordsManage,
        ReportsView,
        EnrollmentsManage,
        ProfileView
    ];
}
