using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class NewVersionCheckService : INewVersionCheckService
{
    private readonly ILogger<NewVersionCheckService> _logger;
    private readonly ICoreService _coreService;
    private readonly ISettings _settings;

    public NewVersionCheckService(ILogger<NewVersionCheckService> logger, ICoreService coreService, ISettings settings)
    {
        _logger = logger;
        _coreService = coreService;
        _settings = settings;
    }

    public async Task CheckForNewVersion()
    {
        _logger.LogTrace("{method}()", nameof(CheckForNewVersion));
        var currentVersion = await _coreService.GetCurrentVersion().ConfigureAwait(false);
        if (string.IsNullOrEmpty(currentVersion))
        {
            _settings.IsNewVersionAvailable = false;
            return;
        }

        try
        {
            if (currentVersion.Contains("-"))
            {
                currentVersion = currentVersion.Split("-").First();
            }
            var localVersion = Version.Parse(currentVersion);
            _logger.LogDebug("Local version is {localVersion}", localVersion);
            var finalUrl = await GetRedirectedUrlAsync("https://github.com/pkuehnel/TeslaSolarCharger/releases/latest").ConfigureAwait(false);
            if (string.IsNullOrEmpty(finalUrl))
            {
                _settings.IsNewVersionAvailable = false;
                return;
            }
            var tag = finalUrl.Split("/").Last();
            var githubVersionString = tag.Substring(1);
            if (string.IsNullOrEmpty(githubVersionString))
            {
                _settings.IsNewVersionAvailable = false;
                return;
            }
            var githubVersion = Version.Parse(githubVersionString);
            _logger.LogDebug("Local version is {githubVersion}", githubVersionString);
            if (githubVersion > localVersion)
            {
                _settings.IsNewVersionAvailable = true;
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't check for new version");
            _settings.IsNewVersionAvailable = false;
            return;
        }
        _settings.IsNewVersionAvailable = false;
    }

    private async Task<string?> GetRedirectedUrlAsync(string uri)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(uri).ConfigureAwait(false);

        return response.RequestMessage?.RequestUri?.ToString();
    }
}
