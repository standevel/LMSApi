using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ITranscriptMigrationService
{
    Task<PreviewTranscriptMigrationResponse> PreviewMigrationAsync(PreviewTranscriptMigrationRequest request, CancellationToken ct);
    Task<CommitTranscriptMigrationResponse> CommitMigrationAsync(CommitTranscriptMigrationRequest request, Guid currentUserId, CancellationToken ct);
    byte[] GenerateTranscriptTemplate();
    List<TranscriptCourseSourceItem> ParseTranscriptFile(Stream fileStream, string fileName);
    Task<IEnumerable<MigratedCourseResultDto>> GetStudentMigratedCoursesAsync(Guid studentId, CancellationToken ct);
    Task<IEnumerable<TranscriptTargetCourseDto>> GetAvailableTargetCoursesAsync(CancellationToken ct);
}
