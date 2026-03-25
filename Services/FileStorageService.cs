using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LMS.Api.Services;

public sealed class FileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public FileStorageService(IConfiguration configuration)
    {
        // Default to C:\LMS_Storage if not configured
        _rootPath = configuration["FileStorage:RootPath"] ?? @"C:\LMS_Storage\Uploads";
    }

    public async Task<string> SaveFileAsync(string category, string referenceId, string fileName, Stream content)
    {
        // Sanitize inputs to prevent path traversal
        var sanitizedCategory = Sanitize(category);
        var sanitizedRefId = Sanitize(referenceId);
        var sanitizedFileName = Path.GetFileName(fileName); // Prevents subdirectories in filename

        var folderPath = Path.Combine(_rootPath, sanitizedCategory, sanitizedRefId);

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Add a timestamp to prevent collisions
        var uniqueFileName = $"{Guid.NewGuid()}_{sanitizedFileName}";
        var physicalPath = Path.Combine(folderPath, uniqueFileName);

        using (var fileStream = new FileStream(physicalPath, FileMode.Create))
        {
            await content.CopyToAsync(fileStream);
        }

        // Return a relative path that we can use for virtual path mapping or direct access
        // Example: /Admission/app-id/uuid_name.pdf
        return $"/{sanitizedCategory}/{sanitizedRefId}/{uniqueFileName}";
    }

    public Task<string> GetPhysicalPathAsync(string fileUrl)
    {
        // Logic to map the relative URL back to a physical path for downloads
        var relativePath = fileUrl.TrimStart('/');
        return Task.FromResult(Path.Combine(_rootPath, relativePath));
    }

    private static string Sanitize(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }
        return input.Replace(" ", "_");
    }
}
