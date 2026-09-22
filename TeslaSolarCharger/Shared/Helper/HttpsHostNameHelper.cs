using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace TeslaSolarCharger.Shared.Helper;

/// <summary>
/// Host names and IP addresses TSC's HTTPS certificate is issued for, as the user configures them and as browsers
/// request them.
/// </summary>
public static partial class HttpsHostNameHelper
{
    public const int MaxConfiguredHostNames = 20;
    private const int MaxHostNameLength = 253;
    private const int MaxLabelLength = 63;
    private static readonly char[] Separators = ['\r', '\n', ',', ';', ' ', '\t',];
    private static readonly IdnMapping IdnMapping = new();

    /// <summary>
    /// Splits configured host names (one per line; commas, semicolons and spaces work, too) into distinct, normalized
    /// entries. Invalid entries are kept, so a validator can report them.
    /// </summary>
    public static List<string> SplitHostNames(string? hostNames)
    {
        if (string.IsNullOrWhiteSpace(hostNames))
        {
            return [];
        }
        return hostNames.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Brings a host name into the form browsers send it in: lower case, without a trailing dot, internationalized
    /// names as punycode. IP addresses get their canonical form, IPv6 without brackets.
    /// </summary>
    public static string Normalize(string hostName)
    {
        var trimmed = hostName.Trim().TrimEnd('.');
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1];
        }
        if (TryParseIpAddress(trimmed, out var ipAddress))
        {
            return ipAddress.ToString();
        }
        try
        {
            return IdnMapping.GetAscii(trimmed).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return trimmed.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Parses only complete IP addresses, unlike <see cref="IPAddress.TryParse(string, out IPAddress)"/>, which also
    /// reads a host name like "123" as 0.0.0.123. IPv4 addresses mapped to IPv6 come back as IPv4 and IPv6 addresses
    /// without scope, as a certificate cannot contain either.
    /// </summary>
    public static bool TryParseIpAddress(string value, out IPAddress ipAddress)
    {
        ipAddress = IPAddress.None;
        var looksLikeIpAddress = value.Contains(':') || FullIpv4AddressRegex().IsMatch(value);
        if (!looksLikeIpAddress || !IPAddress.TryParse(value, out var parsed))
        {
            return false;
        }
        ipAddress = NormalizeIpAddress(parsed);
        return true;
    }

    public static IPAddress NormalizeIpAddress(IPAddress ipAddress)
    {
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            return ipAddress.MapToIPv4();
        }
        return ipAddress.AddressFamily == AddressFamily.InterNetworkV6 && ipAddress.ScopeId != 0
            ? new IPAddress(ipAddress.GetAddressBytes())
            : ipAddress;
    }

    /// <summary>
    /// Whether an already normalized entry is an IP address or a DNS name a certificate can be issued for.
    /// </summary>
    public static bool IsValidHostName(string hostName)
    {
        if (TryParseIpAddress(hostName, out _))
        {
            return true;
        }
        if (hostName.Length is 0 or > MaxHostNameLength)
        {
            return false;
        }
        var labels = hostName.Split('.');
        //A last label of digits only is an incomplete IP address, e.g. 192.168.1, not a name
        return labels.All(IsValidLabel) && !labels[^1].All(char.IsAsciiDigit);
    }

    private static bool IsValidLabel(string label) =>
        label.Length is > 0 and <= MaxLabelLength
        && label.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
        && label[0] != '-'
        && label[^1] != '-';

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    private static partial Regex FullIpv4AddressRegex();
}
