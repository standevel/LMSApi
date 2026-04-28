using Microsoft.AspNetCore.Http;

namespace LMS.Api.Contracts;

public record LecturerCourseOfferingDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    string CourseTitle,
    int CreditUnits,
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    int EnrolledStudentCount,
    int UpcomingSessionsCount);

public record LecturerCoursesResponse(
    List<LecturerCourseOfferingDto> Courses,
    int TotalCourses,
    int TotalStudents);

// Course Detail Contracts
public record CourseMaterialDto(
    Guid Id,
    string Title,
    string? Description,
    string FileUrl,
    string? FileType,
    long? FileSize,
    DateTime UploadedAt,
    string UploadedByName);

public record CourseStudentDto(
    Guid Id,
    string StudentNumber,
    string FullName,
    string Email,
    DateTime EnrolledAt,
    string? Grade);

public record CourseDetailResponse(
    Guid Id,
    string CourseCode,
    string CourseTitle,
    string? Description,
    int CreditUnits,
    Guid ProgramId,
    string ProgramName,
    Guid LevelId,
    string LevelName,
    Guid AcademicSessionId,
    string AcademicSessionName,
    int Semester,
    List<CourseMaterialDto> Materials,
    List<CourseStudentDto> Students,
    int MaterialsCount,
    int StudentsCount);

public record AddCourseMaterialRequest(
    string Title,
    string? Description,
    IFormFile File);

public record AddCourseMaterialResponse(
    Guid Id,
    string Title,
    string FileUrl,
    DateTime UploadedAt);
