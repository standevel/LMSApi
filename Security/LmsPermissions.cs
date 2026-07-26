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
    public const string AdvisingManage = "advising.manage";
    public const string AdvisingAccess = "advising.access";
    public const string ProfileView = "profile.view";
    public const string UsersSwitch = "users.switch";
    public const string AdmissionsManage = "admissions.manage";
    public const string FeesManage = "fees.manage";
    public const string HostelsManage = "hostels.manage";
    public const string HostelsView = "hostels.view";
    public const string HostelsExeatManage = "hostels.exeat.manage";
    public const string TimetableManage = "timetable.manage";
    public const string IntegrationsManage = "integrations.manage";
    public const string QuizzesManage = "quizzes.manage";

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
        AdvisingManage,
        AdvisingAccess,
        ProfileView,
        UsersSwitch,
        AdmissionsManage,
        FeesManage,
        HostelsManage,
        HostelsView,
        HostelsExeatManage,
        TimetableManage,
        IntegrationsManage,
        QuizzesManage
    ];
}
