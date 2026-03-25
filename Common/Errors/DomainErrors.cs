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
}
