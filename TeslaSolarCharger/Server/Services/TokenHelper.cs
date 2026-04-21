using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using System.Net;
using System.Security.Claims;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Shared.Resources;

namespace TeslaSolarCharger.Server.Services;

public class TokenHelper(ILogger<TokenHelper> logger,
    IConstants constants,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache memoryCache,
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory serviceScopeFactory) : ITokenHelper
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
        memoryCache.Set(constants.FleetApiTokenExpirationTimeKey, state.ExpiresAtUtc, GetCacheEntryOptions(state.ExpiresAtUtc));
        return state.TokenState;
    }

    public async Task<List<DtoSmartCarTokenState>> GetSmartCarTokenStates(bool useCache)
    {
        logger.LogTrace("{method}()", nameof(GetSmartCarTokenStates));
        if (useCache && memoryCache.TryGetValue(constants.SmartCarTokenStatesKey, out List<DtoSmartCarTokenState>? cachedSmartCarTokenStates))
        {
            logger.LogTrace("Returning SmartCarTokenStates from cache: {tokenStates}", cachedSmartCarTokenStates);
            if (cachedSmartCarTokenStates != default)
            {
                return cachedSmartCarTokenStates;
            }
        }
        var state = await GetUncachedSmartCarTokenStates().ConfigureAwait(false);
        memoryCache.Set(constants.SmartCarTokenStatesKey, state, GetCacheEntryOptions(state.Any() ? state.Min(s => s.ExpiresAt) : null));
        return state;
    }

    private async Task<List<DtoSmartCarTokenState>> GetUncachedSmartCarTokenStates()
    {
        logger.LogTrace("{method}()", nameof(GetUncachedSmartCarTokenStates));
        var backendTokenState = await GetBackendTokenState(true);
        if (backendTokenState != TokenState.UpToDate)
        {
            throw new InvalidOperationException($"Backend token state is {backendTokenState} and not up to date.");
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "SmartCarRequests/GetSmartCarTokenStates";
        var httpClient = httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var token = await context.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Backend token not found.");
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Could not fetch SmartCar token states. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            throw new InvalidOperationException("Request resulted in non success status code.");
        }
        var smartCarTokenStates = JsonConvert.DeserializeObject<List<DtoSmartCarTokenState>>(responseString);
        if (smartCarTokenStates == null)
        {
            logger.LogError("Could not deserialize result.");
            throw new InvalidOperationException("Could not deserialize result.");
        }
        return smartCarTokenStates;
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
        memoryCache.Set(constants.FleetApiTokenExpirationTimeKey, state.ExpiresAtUtc, GetCacheEntryOptions(state.ExpiresAtUtc));
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

    public async Task<string?> GetTokenUserName()
    {
        logger.LogTrace("{method}()", nameof(GetTokenUserName));
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var token = await context.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return null;
        }
        var tokenHandler = new JwtSecurityTokenHandler();
        if (tokenHandler.ReadToken(token.AccessToken) is not JwtSecurityToken jwtToken)
        {
            return null;
        }

        // Extract claims
        var claims = jwtToken.Claims;

        foreach (var claim in claims)
        {
            if (claim.Type == ClaimTypes.Name)
            {
                return claim.Value;
            }
        }
        return null;
    }

    public async Task<DateTimeOffset?> GetBackendTokenExpirationDate()
    {
        logger.LogTrace("{method}()", nameof(GetBackendTokenExpirationDate));
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var expirationDate = await context.BackendTokens
            .Select(t => new
            {
                t.ExpiresAtUtc,
            })
            .SingleOrDefaultAsync();
        return expirationDate?.ExpiresAtUtc;
    }

    private async Task<TokenStateIncludingExpirationTime> GetUncachedFleetApiTokenState()
    {
        logger.LogTrace("{method}()", nameof(GetUncachedFleetApiTokenState));
        using var scope = serviceScopeFactory.CreateScope();
        var tscConfigurationService = scope.ServiceProvider.GetRequiredService<ITscConfigurationService>();
        var hasCurrentTokenMissingScopes = string.Equals(await tscConfigurationService.GetConfigurationValueByKey(constants.FleetApiTokenMissingScopes), "true", StringComparison.InvariantCultureIgnoreCase);
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
        var httpClient = httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var token = await context.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            return new()
            {
                TokenState = TokenState.MissingPrecondition,
            };
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
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
        logger.LogTrace("{method}()", nameof(GetUncachedBackendTokenState));
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var token = await context.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
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
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var token = await context.BackendTokens.SingleOrDefaultAsync();
        if (token == default)
        {
            return false;
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "Client/IsTokenValid";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var httpClient = httpClientFactory.CreateClient(StaticConstants.HttpClientNameShortTimeout);
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

    /// <summary>
    /// Generates memory cache entry options with a default absolute expiration of 15 minutes, 
    /// which can be reduced if a shorter, future expiration date is provided.
    /// </summary>
    /// <param name="validUntil">An optional future date and time. If provided and it occurs sooner than the 15-minute default, the cache expiration is shortened to match this exact date.</param>
    /// <returns>A configured <see cref="MemoryCacheEntryOptions"/> instance with the calculated absolute expiration relative to now.</returns>
    private MemoryCacheEntryOptions GetCacheEntryOptions(DateTimeOffset? validUntil)
    {
        logger.LogTrace("{method}({validUntil})", nameof(GetCacheEntryOptions), validUntil);
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
