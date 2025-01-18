namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public class DtoVersionRecommendation(string latestVersion, string recommendedVersion, string minimumVersion)
{
    public string LatestVersion { get; set; } = latestVersion;
    public string RecommendedVersion { get; set; } = recommendedVersion;
    public int? RecommendedVersionRequiredInDays { get; set; }
    public string MinimumVersion { get; set; } = minimumVersion;
}
