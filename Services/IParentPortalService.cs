using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IParentPortalService
{
    Task<ErrorOr<List<ParentStudentLinkDto>>> GetLinkedStudentsAsync(Guid parentId, CancellationToken ct = default);
    Task<ErrorOr<StudentProgressDto>> GetStudentProgressAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<StudentGradesDto>> GetStudentGradesAsync(Guid studentId, Guid? academicSessionId = null, CancellationToken ct = default);
    Task<ErrorOr<bool>> SendMessageToStudentAsync(Guid studentId, Guid parentUserId, string content, CancellationToken ct = default);
}

public record StudentProgressDto(Guid StudentId, string StudentName, string StudentNumber, decimal CumulativeGpa, int CreditsEarned, int TotalCreditsRequired, List<CourseProgressDto> CourseProgress);

public record CourseProgressDto(Guid CourseOfferingId, string CourseCode, string CourseTitle, int AttendancePercentage, string? CurrentGrade, bool IsCompleted, string? SessionName = null, Guid? AcademicSessionId = null);

public record StudentGradesDto(Guid StudentId, string StudentName, string StudentNumber, List<StudentGradeDto> Grades);

public record StudentGradeDto(Guid CourseOfferingId, string CourseCode, string CourseTitle, string Grade, string? SessionName = null, Guid? AcademicSessionId = null);
