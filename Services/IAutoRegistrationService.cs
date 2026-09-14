using System;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface IAutoRegistrationService
{
    /// <summary>
    /// Evaluates and registers eligible curriculum and carryover courses for a specific student in an academic session.
    /// </summary>
    Task<StudentAutoRegistrationDetailDto> AutoRegisterStudentAsync(
        Guid studentId,
        Guid academicSessionId,
        bool isDryRun = false,
        LMS.Api.Data.Enums.Semester? targetSemester = null,
        CancellationToken ct = default);

    /// <summary>
    /// Runs batch automatic course registration across all matching students for an academic session.
    /// </summary>
    Task<BatchAutoRegistrationResultDto> RunBatchAutoRegistrationAsync(
        BatchAutoRegistrationRequest request,
        CancellationToken ct = default);
}
