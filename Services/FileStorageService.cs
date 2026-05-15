using LMS.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LMS.Api.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _storageBasePath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IConfiguration configuration,
        ILogger<FileStorageService> logger)
    {
        _storageBasePath = FileStoragePathHelper.ResolveBasePath(configuration["FileStorage:BasePath"]);
        _logger = logger;
        
        // Ensure base directory exists
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
        }
    }

    public async Task<string> UploadFileAsync(IFormFile file, string containerName, string fileName)
    {
        try
        {
            var containerPath = Path.Combine(_storageBasePath, containerName);
            if (!Directory.Exists(containerPath))
            {
                Directory.CreateDirectory(containerPath);
            }

            var filePath = Path.Combine(containerPath, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Uploaded file {FileName} to container {ContainerName}", fileName, containerName);

            // Return relative URL path
            return $"/uploads/{containerName}/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} to container {ContainerName}", fileName, containerName);
            throw;
        }
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.TrimStart('/');
            var filePath = Path.Combine(_storageBasePath, relativePath);

            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("Deleted file from path {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from URL {FileUrl}", fileUrl);
            throw;
        }
    }

    // Legacy methods for admission documents
    public async Task<string> SaveFileAsync(string category, string referenceId, string fileName, Stream stream)
    {
        try
        {
            var containerPath = Path.Combine(_storageBasePath, category, referenceId);
            if (!Directory.Exists(containerPath))
            {
                Directory.CreateDirectory(containerPath);
            }

            var filePath = Path.Combine(containerPath, fileName);

            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Saved file {FileName} to category {Category} with reference {ReferenceId}", fileName, category, referenceId);

            // Return relative path
            return Path.Combine(category, referenceId, fileName).Replace("\\", "/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} to category {Category}", fileName, category);
            throw;
        }
    }

    public Task<string?> GetPhysicalPathAsync(string fileUrl)
    {
        try
        {
            // fileUrl is already a relative path like "category/referenceId/filename.pdf"
            var filePath = Path.Combine(_storageBasePath, fileUrl);

            if (File.Exists(filePath))
            {
                return Task.FromResult<string?>(filePath);
            }

            _logger.LogWarning("File not found at path {FilePath}", filePath);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get physical path for URL {FileUrl}", fileUrl);
            return Task.FromResult<string?>(null);
        }
    }
}
