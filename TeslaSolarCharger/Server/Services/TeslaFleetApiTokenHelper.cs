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
    IConfigurationWrapper configurationWrapper,
    IBackendApiService backendApiService,
    IDateTimeProvider dateTimeProvider) : ITeslaFleetApiTokenHelper
{
    public async Task<TokenState> GetFleetApiTokenState()
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenState));
        var hasCurrentTokenMissingScopes = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .AnyAsync().ConfigureAwait(false);
        if (hasCurrentTokenMissingScopes)
        {
            return TokenState.MissingScopes;
        }
        var isTokenUnauthorized = string.Equals(await tscConfigurationService.GetConfigurationValueByKey(constants.FleetApiTokenUnauthorizedKey), "true", StringComparison.InvariantCultureIgnoreCase);
        if (isTokenUnauthorized)
        {
            return TokenState.Unauthorized;
        }
        var backendTokenState = await GetBackendTokenState();
        if (backendTokenState != TokenState.UpToDate)
        {
            return TokenState.MissingPrecondition;
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "FleetApiRequests/FleetApiTokenExpiresInSeconds";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return TokenState.MissingPrecondition;
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not check if token is valid. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            return TokenState.MissingPrecondition;
        }
        var validFleetApiToken = JsonConvert.DeserializeObject<DtoValue<long?>>(responseString);
        if (validFleetApiToken == null)
        {
            logger.LogError("Could not check if fleet api token is available.");
            return TokenState.MissingPrecondition;
        }
        if (validFleetApiToken.Value == null)
        {
            return TokenState.NotAvailable;
        }
        if (validFleetApiToken.Value <= 0)
        {
            return TokenState.Expired;
        }
        return TokenState.UpToDate;
    }

    public async Task<TokenState> GetBackendTokenState()
    {
        logger.LogTrace("{method}", nameof(GetBackendTokenState));
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return TokenState.NotAvailable;
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        if (token.ExpiresAtUtc < currentDate)
        {
            return TokenState.Expired;
        }
        var isTokenValid = await backendApiService.HasValidBackendToken();
        return isTokenValid ? TokenState.UpToDate : TokenState.Unauthorized;
    }
}
