using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiTokenHelper(ILogger<TeslaFleetApiTokenHelper> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    ITscConfigurationService tscConfigurationService,
    IConfigurationWrapper configurationWrapper) : ITeslaFleetApiTokenHelper
{
    public async Task<FleetApiTokenState> GetFleetApiTokenState()
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenState));
        var hasCurrentTokenMissingScopes = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .AnyAsync().ConfigureAwait(false);
        if (hasCurrentTokenMissingScopes)
        {
            return FleetApiTokenState.FleetApiTokenMissingScopes;
        }
        var isTokenUnauthorized = string.Equals(await tscConfigurationService.GetConfigurationValueByKey(constants.FleetApiTokenUnauthorizedKey), "true", StringComparison.InvariantCultureIgnoreCase);
        if (isTokenUnauthorized)
        {
            return FleetApiTokenState.FleetApiTokenUnauthorized;
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "FleetApiRequests/AnyFleetApiTokenWithExpiryInFuture";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return FleetApiTokenState.NoBackendApiToken;
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not check if token is valid. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            return FleetApiTokenState.BackendTokenUnauthorized;
        }
        var validFleetApiToken = JsonConvert.DeserializeObject<DtoValue<bool>>(responseString);
        if (validFleetApiToken?.Value != true)
        {
            return FleetApiTokenState.FleetApiTokenExpired;
        }
        return FleetApiTokenState.UpToDate;
    }
}
