using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using System.Net;

namespace TeslaSolarCharger.Server.Services;

public class TokenHelper(ILogger<TokenHelper> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    ITscConfigurationService tscConfigurationService,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache memoryCache) : ITokenHelper
{
    public async Task<TokenState> GetFleetApiTokenState(bool useCache)
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenState));
        if (useCache && memoryCache.TryGetValue(constants.FleetApiTokenStateKey, out TokenState cachedFleetTokenState))
        {
            logger.LogTrace("Returning FleetApiTokenState from cache: {tokenState}", cachedFleetTokenState);
            return cachedFleetTokenState;
        }
        var state = await GetUncachedFleetApiTokenState().ConfigureAwait(false);
        memoryCache.Set(constants.FleetApiTokenStateKey, state.TokenState, GetCacheEntryOptions(state.ExpiresAtUtc));
        memoryCache.Set(constants.FleetApiTokenExpirationTimeKey, state, GetCacheEntryOptions(state.ExpiresAtUtc));
        return state.TokenState;
    }

    public async Task<DateTimeOffset?> GetFleetApiTokenExpirationDate(bool useCache)
    {
        logger.LogTrace("{method}()", nameof(GetFleetApiTokenExpirationDate));
        if (useCache && memoryCache.TryGetValue(constants.FleetApiTokenExpirationTimeKey, out DateTimeOffset? expirationTime))
        {
            logger.LogTrace("Returning FleetApiToken ExpirationTime from cache: {expirationTime}", expirationTime);
            return expirationTime;
        }
        var state = await GetUncachedFleetApiTokenState().ConfigureAwait(false);
        memoryCache.Set(constants.FleetApiTokenStateKey, state.TokenState, GetCacheEntryOptions(state.ExpiresAtUtc));
        memoryCache.Set(constants.FleetApiTokenExpirationTimeKey, state, GetCacheEntryOptions(state.ExpiresAtUtc));
        return state.ExpiresAtUtc;
    }

    public async Task<TokenState> GetBackendTokenState(bool useCache)
    {
        logger.LogTrace("{method}", nameof(GetBackendTokenState));
        if (useCache && memoryCache.TryGetValue(constants.BackendTokenStateKey, out TokenState cachedFleetTokenState))
        {
            logger.LogTrace("Returning BackendTokenState from cache: {tokenState}", cachedFleetTokenState);
            return cachedFleetTokenState;
        }
        var state = await GetUncachedBackendTokenState().ConfigureAwait(false);
        memoryCache.Set(constants.BackendTokenStateKey, state.TokenState, GetCacheEntryOptions(state.ExpiresAtUtc));
        return state.TokenState;
    }

    public async Task<DateTimeOffset?> GetBackendTokenExpirationDate()
    {
        logger.LogTrace("{method}()", nameof(GetBackendTokenExpirationDate));
        var expirationDate = await teslaSolarChargerContext.BackendTokens.Select(t => t.ExpiresAtUtc)
            .SingleOrDefaultAsync();
        return expirationDate;
    }

    private async Task<TokenStateIncludingExpirationTime> GetUncachedFleetApiTokenState()
    {
        var hasCurrentTokenMissingScopes = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.FleetApiTokenMissingScopes)
            .AnyAsync().ConfigureAwait(false);
        if (hasCurrentTokenMissingScopes)
        {
            return new()
            {
                TokenState = TokenState.MissingScopes,
            };
        }
        var isTokenUnauthorized = string.Equals(await tscConfigurationService.GetConfigurationValueByKey(constants.FleetApiTokenUnauthorizedKey), "true", StringComparison.InvariantCultureIgnoreCase);
        if (isTokenUnauthorized)
        {
            return new()
            {
                TokenState = TokenState.Unauthorized,
            };
        }
        var backendTokenState = await GetBackendTokenState(false);
        if (backendTokenState != TokenState.UpToDate)
        {
            return new()
            {
                TokenState = TokenState.MissingPrecondition,
            };
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "FleetApiRequests/FleetApiTokenExpiresInSeconds";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return new()
            {
                TokenState = TokenState.MissingPrecondition,
            };
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not check if token is valid. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            return new()
            {
                TokenState = TokenState.MissingPrecondition,
            };
        }
        var validFleetApiToken = JsonConvert.DeserializeObject<DtoValue<long?>>(responseString);
        if (validFleetApiToken == null)
        {
            logger.LogError("Could not check if fleet api token is available.");
            return new()
            {
                TokenState = TokenState.MissingPrecondition,
            };
        }
        if (validFleetApiToken.Value == null)
        {
            return new()
            {
                TokenState = TokenState.NotAvailable,
            };
        }
        if (validFleetApiToken.Value <= 0)
        {
            return new()
            {
                TokenState = TokenState.Expired,
                ExpiresAtUtc = dateTimeProvider.DateTimeOffSetUtcNow().AddSeconds(validFleetApiToken.Value.Value),
            };
        }
        return new()
        {
            TokenState = TokenState.UpToDate,
            ExpiresAtUtc = dateTimeProvider.DateTimeOffSetUtcNow().AddSeconds(validFleetApiToken.Value.Value),
        };
    }


    private async Task<TokenStateIncludingExpirationTime> GetUncachedBackendTokenState()
    {
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return new()
            {
                TokenState = TokenState.NotAvailable,
            };
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        if (token.ExpiresAtUtc < currentDate)
        {
            return new()
            {
                TokenState = TokenState.Expired,
            };
        }
        var isTokenValid = await HasValidBackendToken();
        if (isTokenValid)
        {
            return new()
            {
                TokenState = TokenState.UpToDate,
                ExpiresAtUtc = token.ExpiresAtUtc,
            };
        }

        return new()
        {
            TokenState = TokenState.Unauthorized,
        };
    }

    private async Task<bool> HasValidBackendToken()
    {
        logger.LogTrace("{method}", nameof(HasValidBackendToken));
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if (token == default)
        {
            return false;
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "Client/IsTokenValid";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }
        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Could not check if token is valid. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            throw new InvalidOperationException("Could not check if token is valid");
        }
        return true;
    }

    private MemoryCacheEntryOptions GetCacheEntryOptions(DateTimeOffset? validUntil)
    {
        var validFor = TimeSpan.FromMinutes(15);
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        if (validUntil != default && (validUntil < (currentDate + validFor)) && (validUntil > currentDate))
        {
            validFor = validUntil.Value - currentDate;
        }
        return new()
        {
            AbsoluteExpirationRelativeToNow = validFor,
        };
    }

    private class TokenStateIncludingExpirationTime
    {
        public TokenState TokenState { get; init; }
        public DateTimeOffset? ExpiresAtUtc { get; init; }
    }

}
