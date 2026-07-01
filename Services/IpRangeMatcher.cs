using System.Net;
using System.Net.Sockets;

namespace LMS.Api.Services;

public static class IpRangeMatcher
{
    public static List<string> NormalizeRanges(IEnumerable<string>? ranges) =>
        (ranges ?? Enumerable.Empty<string>())
            .Select(range => range.Trim())
            .Where(range => !string.IsNullOrWhiteSpace(range))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static List<string> ValidateRanges(IEnumerable<string>? ranges)
    {
        var errors = new List<string>();
        foreach (var range in NormalizeRanges(ranges))
        {
            if (!TryParseRange(range, out _))
            {
                errors.Add($"Invalid IP/range: \"{range}\". Use localhost, a single IP, CIDR range such as 192.168.10.0/24, or start-end range such as 192.168.10.1-192.168.10.50.");
            }
        }

        return errors;
    }

    public static bool MatchesAny(IPAddress clientIp, IEnumerable<string>? ranges)
    {
        var normalizedClientIp = NormalizeIp(clientIp);
        var isLoopback = IPAddress.IsLoopback(normalizedClientIp);
        return NormalizeRanges(ranges).Any(range =>
            TryParseRange(range, out var parsed) &&
            (!isLoopback || IsExplicitLoopbackAllowance(parsed)) &&
            Matches(normalizedClientIp, parsed));
    }

    private static bool TryParseRange(string value, out ParsedIpRange parsed)
    {
        parsed = default;
        if (string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            parsed = new ParsedIpRange(IPAddress.Loopback, IPAddress.IPv6Loopback, 0, IpRangeKind.Localhost);
            return true;
        }

        var rangeParts = value.Split('-', 2, StringSplitOptions.TrimEntries);
        if (rangeParts.Length == 2)
        {
            if (!IPAddress.TryParse(rangeParts[0], out var startAddress) ||
                !IPAddress.TryParse(rangeParts[1], out var endAddress))
            {
                return false;
            }

            startAddress = NormalizeIp(startAddress);
            endAddress = NormalizeIp(endAddress);
            if (startAddress.AddressFamily != endAddress.AddressFamily ||
                CompareBytes(startAddress.GetAddressBytes(), endAddress.GetAddressBytes()) > 0)
            {
                return false;
            }

            parsed = new ParsedIpRange(startAddress, endAddress, 0, IpRangeKind.StartEnd);
            return true;
        }

        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (!IPAddress.TryParse(parts[0], out var address))
        {
            return false;
        }

        address = NormalizeIp(address);
        if (parts.Length == 1)
        {
            parsed = new ParsedIpRange(address, null, address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128, IpRangeKind.Single);
            return true;
        }

        var maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > maxPrefix)
        {
            return false;
        }

        parsed = new ParsedIpRange(address, null, prefixLength, IpRangeKind.Cidr);
        return true;
    }

    private static bool Matches(IPAddress clientIp, ParsedIpRange range)
    {
        if (range.Kind == IpRangeKind.Localhost)
        {
            return IPAddress.IsLoopback(clientIp);
        }

        var rangeAddress = NormalizeIp(range.Address);
        if (clientIp.AddressFamily != rangeAddress.AddressFamily)
        {
            return false;
        }

        if (range.Kind == IpRangeKind.Single)
        {
            return clientIp.Equals(rangeAddress);
        }

        if (range.Kind == IpRangeKind.StartEnd)
        {
            var endAddress = range.EndAddress is null ? rangeAddress : NormalizeIp(range.EndAddress);
            return CompareBytes(clientIp.GetAddressBytes(), rangeAddress.GetAddressBytes()) >= 0 &&
                CompareBytes(clientIp.GetAddressBytes(), endAddress.GetAddressBytes()) <= 0;
        }

        var clientBytes = clientIp.GetAddressBytes();
        var rangeBytes = rangeAddress.GetAddressBytes();
        var fullBytes = range.PrefixLength / 8;
        var remainingBits = range.PrefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (clientBytes[i] != rangeBytes[i])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (clientBytes[fullBytes] & mask) == (rangeBytes[fullBytes] & mask);
    }

    private static IPAddress NormalizeIp(IPAddress ip) =>
        ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static bool IsExplicitLoopbackAllowance(ParsedIpRange range) =>
        range.Kind == IpRangeKind.Localhost ||
        (range.Kind == IpRangeKind.Single && IPAddress.IsLoopback(NormalizeIp(range.Address)));

    private static int CompareBytes(byte[] left, byte[] right)
    {
        for (var i = 0; i < left.Length; i++)
        {
            var comparison = left[i].CompareTo(right[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private enum IpRangeKind
    {
        Single,
        Cidr,
        StartEnd,
        Localhost
    }

    private readonly record struct ParsedIpRange(IPAddress Address, IPAddress? EndAddress, int PrefixLength, IpRangeKind Kind);
}
