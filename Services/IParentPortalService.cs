using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using ErrorOr;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IParentPortalService
{
    Task<ErrorOr<List<ParentGuardianDto>>> GetLinkedStudentsAsync(Guid parentId, CancellationToken ct = default);
    Task<ErrorOr<StudentProgressDto>> GetStudentProgressAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<StudentGradesDto>> GetStudentGradesAsync(Guid studentId, CancellationToken ct = default);
    Task<ErrorOr<Deleted>> SendMessageToStudentAsync(Guid studentId, Guid parentId, string content, CancellationToken ct = default);
}

public record StudentProgressDto(Guid StudentId, string StudentName, decimal CumulativeGpa, int CreditsEarned, int TotalCreditsRequired, List<CourseProgressDto> CourseProgress);

public record CourseProgressDto(Guid CourseOfferingId, string CourseCode, string CourseTitle, int AttendancePercentage, string? CurrentGrade, bool IsCompleted);

public record StudentGradesDto(Guid StudentId, string StudentName, List<StudentGradeDto> Grades);

public record StudentGradeDto(Guid CourseOfferingId, string CourseCode, string CourseTitle, string Grade);