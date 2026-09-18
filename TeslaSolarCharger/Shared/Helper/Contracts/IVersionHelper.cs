namespace TeslaSolarCharger.Shared.Helper.Contracts;

public interface IVersionHelper
{
    /// <summary>
    /// Converts a version string (including SemVer pre release and build metadata suffixes) to a <see cref="Version"/> that can be compared with other versions.
    /// </summary>
    /// <param name="versionString">Version string, e.g. <c>2.48.1</c>, <c>2.48.1-alpha.3</c> or <c>2.48.1+abc123</c>.</param>
    /// <returns><c>null</c> if the version could not be parsed.</returns>
    Version? ParseComparableVersion(string? versionString);

    /// <summary>
    /// Checks if <paramref name="currentVersionString"/> is within the (inclusive) range of <paramref name="validFromVersion"/> and <paramref name="validToVersion"/>.
    /// Bounds that are <c>null</c>, empty or unparsable are treated as "no bound".
    /// </summary>
    /// <returns><c>true</c> if the version is in range or if the current version is unknown.</returns>
    bool IsVersionInRange(string? currentVersionString, string? validFromVersion, string? validToVersion);
}
