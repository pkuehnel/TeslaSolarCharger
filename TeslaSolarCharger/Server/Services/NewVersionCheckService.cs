using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class NewVersionCheckService : INewVersionCheckService
{
    private readonly ILogger<NewVersionCheckService> _logger;
    private readonly ICoreService _coreService;
    private readonly ISettings _settings;
    private readonly IBackendApiService _backendApiService;
    private readonly IErrorHandlingService _errorHandlingService;
    private readonly IIssueKeys _issueKeys;

    public NewVersionCheckService(ILogger<NewVersionCheckService> logger, ICoreService coreService, ISettings settings,
        IBackendApiService backendApiService, IErrorHandlingService errorHandlingService, IIssueKeys issueKeys)
    {
        _logger = logger;
        _coreService = coreService;
        _settings = settings;
        _backendApiService = backendApiService;
        _errorHandlingService = errorHandlingService;
        _issueKeys = issueKeys;
    }

    public async Task CheckForNewVersion()
    {
        _logger.LogTrace("{method}()", nameof(CheckForNewVersion));
        var currentVersion = await _coreService.GetCurrentVersion().ConfigureAwait(false);
        var versionRecommendation = await _backendApiService.PostInstallationInformation("CheckForNewVersion").ConfigureAwait(false);
        var couldParseLocalVersion = Version.TryParse(currentVersion, out var localVersion);
        if (!couldParseLocalVersion)
        {
            if (string.IsNullOrEmpty(currentVersion))
            {
                _logger.LogError("Could not get local version");
                return;
            }
            var splittedVersionString= currentVersion.Split("-")[0];
            couldParseLocalVersion = Version.TryParse(splittedVersionString, out localVersion);
            if(!couldParseLocalVersion || localVersion == default)
            {
                _logger.LogError("Could not parse local version {currentVersion}", currentVersion);
                return;
            }
            var buildToUse = localVersion.Build > 0 ? localVersion.Build - 1 : 0;
            localVersion = new(localVersion.Major, localVersion.Minor, buildToUse);
        }
        var minimumVersion = Version.Parse(versionRecommendation.MinimumVersion);
        if (localVersion < minimumVersion)
        {
            await _errorHandlingService.HandleError(nameof(NewVersionCheckService), nameof(CheckForNewVersion), "New version required",
                "You need to update to the latest version as TSC won't work anymore", _issueKeys.NewRequiredSoftwareAvailable, null, null).ConfigureAwait(false);
            await _errorHandlingService.HandleErrorResolved(_issueKeys.NewRecommendedSoftwareAvailable, null).ConfigureAwait(false);
            await _errorHandlingService.HandleErrorResolved(_issueKeys.NewSoftwareAvailable, null).ConfigureAwait(false);
            return;
        }
        else
        {
            await _errorHandlingService.HandleErrorResolved(_issueKeys.NewRequiredSoftwareAvailable, null).ConfigureAwait(false);
        }

        var recommendedVersion = Version.Parse(versionRecommendation.RecommendedVersion);
        if (localVersion < recommendedVersion)
        {
            var headLineText = versionRecommendation.RecommendedVersionRequiredInDays == default ? "New version recommended" : $"New Version required in {versionRecommendation.RecommendedVersionRequiredInDays} days";
            var messageText = versionRecommendation.RecommendedVersionRequiredInDays == default ? "It is recommended to update to the latest version" : $"After {versionRecommendation.RecommendedVersionRequiredInDays} days your current installed version won't work anymore. Please update as soon as possible.";
            await _errorHandlingService.HandleError(nameof(NewVersionCheckService), nameof(CheckForNewVersion), headLineText,
                messageText, _issueKeys.NewRecommendedSoftwareAvailable, null, null).ConfigureAwait(false);
            await _errorHandlingService.HandleErrorResolved(_issueKeys.NewSoftwareAvailable, null).ConfigureAwait(false);
            return;
        }
        else
        {
            await _errorHandlingService.HandleErrorResolved(_issueKeys.NewRecommendedSoftwareAvailable, null).ConfigureAwait(false);
        }
        var latestVersion = Version.Parse(versionRecommendation.LatestVersion);
        if (localVersion < latestVersion)
        {
            var headLineText = "New version available";
            var messageText = "Update to the latest version to get the latest new features";
            await _errorHandlingService.HandleError(nameof(NewVersionCheckService), nameof(CheckForNewVersion), headLineText,
                messageText, _issueKeys.NewSoftwareAvailable, null, null).ConfigureAwait(false);
            return;
        }
        await _errorHandlingService.HandleErrorResolved(_issueKeys.NewSoftwareAvailable, null).ConfigureAwait(false);
    }

    private async Task<string?> GetRedirectedUrlAsync(string uri)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(uri).ConfigureAwait(false);

        return response.RequestMessage?.RequestUri?.ToString();
    }
}
