using LMS.Api.Contracts;

namespace LMS.Api.Services;

public interface ICourseCatalogImportService
{
    Task<CourseCatalogImportPreview> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        Guid? programId,
        IEnumerable<Guid> programIds,
        Guid? academicSessionId,
        CancellationToken ct = default);

    CourseCatalogImportPreview GetPreview(Guid uploadId);

    Task<CourseCatalogImportResult> ApplyImportAsync(
        Guid uploadId,
        Guid? programId,
        IEnumerable<Guid> programIds,
        Guid? curriculumId,
        string? curriculumName,
        Guid? academicSessionId,
        CancellationToken ct = default);

    void DeletePreview(Guid uploadId);
}
