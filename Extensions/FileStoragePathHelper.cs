using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LMS.Api.Extensions;

internal static class FileStoragePathHelper
{
    public static string ResolveBasePath(string? storageBasePathConfig)
    {
        if (string.IsNullOrWhiteSpace(storageBasePathConfig))
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        }

        var normalizedPath = storageBasePathConfig.Trim()
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && Regex.IsMatch(normalizedPath, "^[A-Za-z]:[\\/].*"))
        {
            normalizedPath = normalizedPath.Substring(2).TrimStart(Path.DirectorySeparatorChar);
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalizedPath));
    }
}
