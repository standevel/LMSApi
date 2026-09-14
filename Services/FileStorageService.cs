using System.Text;
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
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            var safeContainer = SanitizeIdentifier(containerName, "uploads");
            var safeFileName = SanitizeFileName(fileName);

            var containerPath = Path.Combine(_storageBasePath, safeContainer);
            var filePath = Path.Combine(containerPath, safeFileName);
            var fullPath = EnsureWithinStorageBase(filePath);

            var directoryPath = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Uploaded file {FileName} to container {ContainerName}", safeFileName, safeContainer);

            // Return relative URL path
            return $"/uploads/{safeContainer}/{safeFileName}";
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
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return;
            }

            if (fileUrl.Contains(".."))
            {
                throw new UnauthorizedAccessException("Path traversal attempt detected in DeleteFileAsync.");
            }

            var relativePath = fileUrl.TrimStart('/', '\\');
            var filePath = Path.Combine(_storageBasePath, relativePath);
            var fullPath = EnsureWithinStorageBase(filePath);

            if (File.Exists(fullPath))
            {
                await Task.Run(() => File.Delete(fullPath));
                _logger.LogInformation("Deleted file from path {FilePath}", fullPath);
                return;
            }

            if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = relativePath.Substring(8);
                var altPath = Path.Combine(_storageBasePath, stripped);
                var altFullPath = EnsureWithinStorageBase(altPath);
                if (File.Exists(altFullPath))
                {
                    await Task.Run(() => File.Delete(altFullPath));
                    _logger.LogInformation("Deleted file from path {FilePath}", altFullPath);
                }
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
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var safeCategory = SanitizeIdentifier(category, "general");
            var safeRefId = SanitizeIdentifier(referenceId, "anonymous");
            var safeFileName = SanitizeFileName(fileName);

            var containerPath = Path.Combine(_storageBasePath, safeCategory, safeRefId);
            var filePath = Path.Combine(containerPath, safeFileName);
            var fullPath = EnsureWithinStorageBase(filePath);

            var directoryPath = Path.GetDirectoryName(fullPath)!;
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using var fileStream = new FileStream(fullPath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation("Saved file {FileName} to category {Category} with reference {ReferenceId}", safeFileName, safeCategory, safeRefId);

            // Return relative path
            return Path.Combine(safeCategory, safeRefId, safeFileName).Replace("\\", "/");
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
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return Task.FromResult<string?>(null);
            }

            if (fileUrl.Contains(".."))
            {
                _logger.LogWarning("Potential path traversal attempt in fileUrl: {FileUrl}", fileUrl);
                return Task.FromResult<string?>(null);
            }

            var relativePath = fileUrl.TrimStart('/', '\\');
            var filePath = Path.Combine(_storageBasePath, relativePath);
            var fullPath = Path.GetFullPath(filePath);

            var normalizedBase = Path.GetFullPath(_storageBasePath);
            if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                normalizedBase += Path.DirectorySeparatorChar;
            }

            if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, Path.GetFullPath(_storageBasePath), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Path traversal attempt detected in GetPhysicalPathAsync for URL {FileUrl}", fileUrl);
                return Task.FromResult<string?>(null);
            }

            if (File.Exists(fullPath))
            {
                return Task.FromResult<string?>(fullPath);
            }

            if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("uploads\\", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = relativePath.Substring(8);
                var altPath = Path.GetFullPath(Path.Combine(_storageBasePath, stripped));
                if (altPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase) && File.Exists(altPath))
                {
                    return Task.FromResult<string?>(altPath);
                }
            }

            _logger.LogWarning("File not found at path {FilePath}", fullPath);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get physical path for URL {FileUrl}", fileUrl);
            return Task.FromResult<string?>(null);
        }
    }

    private string EnsureWithinStorageBase(string targetPath)
    {
        var fullPath = Path.GetFullPath(targetPath);
        var normalizedBase = Path.GetFullPath(_storageBasePath);
        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            normalizedBase += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, Path.GetFullPath(_storageBasePath), StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Path traversal attempt detected: target path is outside the storage directory.");
        }

        return fullPath;
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        }

        var sanitized = Path.GetFileName(fileName.Trim().Replace('\\', '/'));
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = sanitized.Where(c => !invalidChars.Contains(c) && c != ':').ToArray();
        sanitized = new string(cleanChars).Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized) || sanitized == "." || sanitized == "..")
        {
            throw new ArgumentException("Invalid file name provided.", nameof(fileName));
        }

        return sanitized;
    }

    private static string SanitizeIdentifier(string? identifier, string fallback = "default")
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return fallback;
        }

        var clean = Path.GetFileName(identifier.Trim().Replace('\\', '/').TrimEnd('/'));
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = clean.Where(c => !invalidChars.Contains(c) && c != ':').ToArray();
        var result = new string(cleanChars).Trim('.', ' ');

        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }
}
