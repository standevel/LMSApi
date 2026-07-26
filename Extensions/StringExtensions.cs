using System.Globalization;

namespace LMS.Api.Extensions;

public static class StringExtensions
{
    private static readonly TextInfo TextInfo = new CultureInfo("en-US", false).TextInfo;

    /// <summary>
    /// Converts a string to title case (e.g., "JOHN DOE" becomes "John Doe").
    /// </summary>
    public static string ToTitleCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        return TextInfo.ToTitleCase(input.ToLowerInvariant().Trim());
    }
}
