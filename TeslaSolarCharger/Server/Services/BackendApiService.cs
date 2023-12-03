using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BackendApiService : IBackendApiService
{
    private readonly ILogger<BackendApiService> _logger;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IConstants _constants;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BackendApiService(ILogger<BackendApiService> logger, ITscConfigurationService tscConfigurationService,
        IConfigurationWrapper configurationWrapper, ITeslaSolarChargerContext teslaSolarChargerContext, IConstants constants,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _tscConfigurationService = tscConfigurationService;
        _configurationWrapper = configurationWrapper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _constants = constants;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<DtoValue<string>> StartTeslaOAuth(string locale)
    {
        _logger.LogTrace("{method}()", nameof(StartTeslaOAuth));
        var currentTokens = await _teslaSolarChargerContext.TeslaTokens.ToListAsync().ConfigureAwait(false);
        _teslaSolarChargerContext.TeslaTokens.RemoveRange(currentTokens);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var installationId = await _tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiBaseUrl = _configurationWrapper.BackendApiBaseUrl();
        using var httpClient = new HttpClient();
        var requestUri = $"{backendApiBaseUrl}Tsc/StartTeslaOAuth?installationId={installationId}";
        var responseString = await httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
        var oAuthRequestInformation = JsonConvert.DeserializeObject<DtoTeslaOAuthRequestInformation>(responseString) ?? throw new InvalidDataException("Could not get oAuth data");
        var requestUrl = GenerateAuthUrl(oAuthRequestInformation, locale);
        var tokenRequested = await _teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == _constants.FleetApiTokenRequested)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequested == null)
        {
            var config = new TscConfiguration
            {
                Key = _constants.FleetApiTokenRequested,
                Value = _dateTimeProvider.UtcNow().ToString("O"),
            };
            _teslaSolarChargerContext.TscConfigurations.Add(config);
        }
        else
        {
            tokenRequested.Value = _dateTimeProvider.UtcNow().ToString("O");
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        return new DtoValue<string>(requestUrl);
    }

    internal string GenerateAuthUrl(DtoTeslaOAuthRequestInformation oAuthInformation, string locale)
    {
        _logger.LogTrace("{method}({@oAuthInformation})", nameof(GenerateAuthUrl), oAuthInformation);
        var url =
            $"https://auth.tesla.com/oauth2/v3/authorize?&client_id={Uri.EscapeDataString(oAuthInformation.ClientId)}&locale={Uri.EscapeDataString(locale)}&prompt={Uri.EscapeDataString(oAuthInformation.Prompt)}&redirect_uri={Uri.EscapeDataString(oAuthInformation.RedirectUri)}&response_type={Uri.EscapeDataString(oAuthInformation.ResponseType)}&scope={Uri.EscapeDataString(oAuthInformation.Scope)}&state={Uri.EscapeDataString(oAuthInformation.State)}";
        return url;
    }

    public async Task PostInstallationInformation(string reason)
    {
        try
        {
            var url = _configurationWrapper.BackendApiBaseUrl() + "Tsc/NotifyInstallation";
            var installationId = await _tscConfigurationService.GetInstallationId().ConfigureAwait(false);
            var currentVersion = await GetCurrentVersion().ConfigureAwait(false);
            var installationInformation = new DtoInstallationInformation
            {
                InstallationId = installationId.ToString(),
                Version = currentVersion ?? "unknown",
                InfoReason = reason,
            };
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = await httpClient.PostAsJsonAsync(url, installationInformation).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not post installation information");
        }

    }

    public async Task PostErrorInformation(string source, string methodName, string message, string? stackTrace = null)
    {
        try
        {
            var url = _configurationWrapper.BackendApiBaseUrl() + "Tsc/NotifyError";
            var installationId = await _tscConfigurationService.GetInstallationId().ConfigureAwait(false);
            var currentVersion = await GetCurrentVersion().ConfigureAwait(false);
            var errorInformation = new DtoErrorInformation()
            {
                InstallationId = installationId.ToString(),
                Source = source,
                MethodName = methodName,
                Message = message,
                Version = currentVersion ?? "unknown",
                StackTrace = stackTrace,
            };
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = await httpClient.PostAsJsonAsync(url, errorInformation).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not post error information");
        }

    }

    public Task<string?> GetCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return Task.FromResult(fileVersionInfo.ProductVersion);
    }
}
