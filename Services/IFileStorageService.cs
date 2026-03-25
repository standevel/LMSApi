using System;
using System.IO;
using System.Threading.Tasks;

namespace LMS.Api.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a file to the physical storage.
    /// </summary>
    /// <param name="category">The category of the file (e.g., Admission, Academic).</param>
    /// <param name="referenceId">The ID of the owner or application.</param>
    /// <param name="fileName">Original filename.</param>
    /// <param name="content">File stream content.</param>
    /// <returns>The relative path or URL where the file is stored.</returns>
    Task<string> SaveFileAsync(string category, string referenceId, string fileName, Stream content);

    /// <summary>
    /// Gets the absolute path for a stored file URL.
    /// </summary>
    Task<string> GetPhysicalPathAsync(string fileUrl);
}
