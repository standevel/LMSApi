using ErrorOr;

namespace LMS.Api.Common.Errors;

public static class DomainErrors
{
    public static class AcademicProgram
    {
        public static Error NotFound => Error.NotFound(
            "Program.NotFound",
            "Academic program not found");

        public static Error DuplicateCode => Error.Conflict(
            "Program.DuplicateCode",
            "An academic program with this code already exists");
    }

    public static class AcademicSession
    {
        public static Error NotFound => Error.NotFound(
            "Session.NotFound",
            "Academic session not found");

        public static Error ActiveSessionExists => Error.Conflict(
            "Session.ActiveExists",
            "Only one academic session can be active at a time");
    }

    public static class Enrollment
    {
        public static Error Duplicate => Error.Conflict(
            "Enrollment.Duplicate",
            "Student is already enrolled in a program for this academic session");

        public static Error StudentNotFound => Error.NotFound(
            "Enrollment.StudentNotFound",
            "Student record not found");
    }

    public static class Curriculum
    {
        public static Error NotFound => Error.NotFound(
            "Curriculum.NotFound",
            "Curriculum version not found");

        public static Error DuplicateCourse => Error.Conflict(
            "Curriculum.DuplicateCourse",
            "This course already exists in the curriculum for the selected level and semester");
    }

    public static class Course
    {
        public static Error NotFound => Error.NotFound(
            "Course.NotFound",
            "Course not found");

        public static Error DuplicateCode => Error.Conflict(
            "Course.DuplicateCode",
            "A course with this code already exists");
    }

    public static class Faculty
    {
        public static Error NotFound => Error.NotFound(
            "Faculty.NotFound",
            "Faculty not found");
    }

    public static class Department
    {
        public static Error NotFound => Error.NotFound(
            "Department.NotFound",
            "Department not found");

        public static Error DuplicateCode => Error.Conflict(
            "Department.DuplicateCode",
            "A department with this code already exists");
    }

    public static class DiscussionThread
    {
        public static Error NotFound => Error.NotFound(
            "DiscussionThread.NotFound",
            "Discussion thread not found");
    }

    public static class DiscussionPost
    {
        public static Error NotFound => Error.NotFound(
            "DiscussionPost.NotFound",
            "Discussion post not found");
    }

    public static class Notification
    {
        public static Error NotFound => Error.NotFound(
            "Notification.NotFound",
            "Notification not found");
    }

    public static class Message
    {
        public static Error NotFound => Error.NotFound(
            "Message.NotFound",
            "Message not found");
    }

    public static class Reporting
    {
        public static Error StudentNotFound => Error.NotFound(
            "Reporting.StudentNotFound",
            "Student record not found");

        public static Error GpaNotAvailable => Error.NotFound(
            "Reporting.GpaNotAvailable",
            "GPA data is not available for the specified student or session");

        public static Error TranscriptNotFound => Error.NotFound(
            "Reporting.TranscriptNotFound",
            "Transcript request not found");

        public static Error DegreeAuditNotFound => Error.NotFound(
            "Reporting.DegreeAuditNotFound",
            "Degree audit not found");

        public static Error DegreeRequirementNotFound => Error.NotFound(
            "Reporting.DegreeRequirementNotFound",
            "Degree requirement not found");

        public static Error ReportCacheExpired => Error.NotFound(
            "Reporting.ReportCacheExpired",
            "Report cache has expired");

        public static Error CertificateNotFound => Error.NotFound(
            "Reporting.CertificateNotFound",
            "Certificate request not found");

        public static Error GraduationCheckFailed => Error.Validation(
            "Reporting.GraduationCheckFailed",
            "Student has not completed all degree requirements for graduation");
    }
}
