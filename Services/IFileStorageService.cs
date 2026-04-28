using Microsoft.AspNetCore.Http;

namespace LMS.Api.Services;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string containerName, string fileName);
    Task DeleteFileAsync(string fileUrl);
    
    // Legacy methods for admission documents
    Task<string> SaveFileAsync(string category, string referenceId, string fileName, Stream stream);
    Task<string?> GetPhysicalPathAsync(string fileUrl);
}
