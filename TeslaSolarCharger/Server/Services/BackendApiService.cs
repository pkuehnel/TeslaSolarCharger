using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
}
