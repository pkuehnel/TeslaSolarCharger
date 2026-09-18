using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Shared.Helper.Contracts;

namespace TeslaSolarCharger.Shared.Helper;

public class VersionHelper(ILogger<VersionHelper> logger) : IVersionHelper
{
    public Version? ParseComparableVersion(string? versionString)
    {
        logger.LogTrace("{method}({versionString})", nameof(ParseComparableVersion), versionString);
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return null;
        }

        //Build metadata (+abc123) never influences precedence, so it can be removed before parsing
        var withoutBuildMetadata = versionString.Split('+')[0];
        var preReleaseSeparatorIndex = withoutBuildMetadata.IndexOf('-');
        var isPreRelease = preReleaseSeparatorIndex >= 0;
        var coreVersionString = isPreRelease ? withoutBuildMetadata[..preReleaseSeparatorIndex] : withoutBuildMetadata;
        if (!Version.TryParse(coreVersionString, out var parsedVersion))
        {
            logger.LogWarning("Could not parse version {versionString}", versionString);
            return null;
        }

        if (!isPreRelease)
        {
            return parsedVersion;
        }

        //A pre release of x.y.z is older than x.y.z, so it is compared as the previous patch version
        var buildToUse = parsedVersion.Build > 0 ? parsedVersion.Build - 1 : 0;
        return new(parsedVersion.Major, parsedVersion.Minor, buildToUse);
    }

    public bool IsVersionInRange(string? currentVersionString, string? validFromVersion, string? validToVersion)
    {
        logger.LogTrace("{method}({currentVersionString}, {validFromVersion}, {validToVersion})", nameof(IsVersionInRange),
            currentVersionString, validFromVersion, validToVersion);
        var currentVersion = ParseComparableVersion(currentVersionString);
        if (currentVersion == default)
        {
            //Without a known current version the range can not be evaluated, so do not filter anything out
            return true;
        }

        var fromVersion = ParseComparableVersion(validFromVersion);
        if ((fromVersion != default) && (currentVersion < fromVersion))
        {
            return false;
        }

        var toVersion = ParseComparableVersion(validToVersion);
        if ((toVersion != default) && (currentVersion > toVersion))
        {
            return false;
        }

        return true;
    }
}
