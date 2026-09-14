namespace LMS.Api.Data.Entities;

/// <summary>
/// Information categories a parent/guardian may be granted access to.
/// Values are stable ints (persisted in JSON/config and consent rows).
/// </summary>
public enum ParentAccessCategory
{
    AcademicResults = 1,
    Attendance = 2,
    CourseRegistration = 3,
    Fees = 4,
    Hostel = 5,
    Cafeteria = 6,
    Wallet = 7,
    PrivateMessages = 8,
    LecturerConversations = 9
}
