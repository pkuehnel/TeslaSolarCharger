using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend.User;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BackendApiService(
    ILogger<BackendApiService> logger,
    ITscConfigurationService tscConfigurationService,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys,
    IPasswordGenerationService passwordGenerationService,
    ITokenHelper tokenHelper,
    IMemoryCache memoryCache)
    : IBackendApiService
{
    public async Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl)
    {
        logger.LogTrace("{method}()", nameof(StartTeslaOAuth));
        var encryptionKey = passwordGenerationService.GeneratePassword(32);
        await tscConfigurationService.SetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey, encryptionKey).ConfigureAwait(false);
        var state = Guid.NewGuid();
        var requestUri = $"Client/AddAuthenticationStartInformation?redirectUri={Uri.EscapeDataString(baseUrl)}&encryptionKey={Uri.EscapeDataString(encryptionKey)}&state={Uri.EscapeDataString(state.ToString())}";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Can not start Tesla O Auth without backend token");
        }
        var result = await SendRequestToBackend<DtoTeslaOAuthRequestInformation>(HttpMethod.Post, token.AccessToken, requestUri, null).ConfigureAwait(false);
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var oAuthRequestInformation = result.Data;
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }

        if (oAuthRequestInformation == default)
        {
            throw new InvalidOperationException("oAuth Information is null");
        }
        var requestUrl = GenerateAuthUrl(oAuthRequestInformation, locale);
        await tscConfigurationService.SetConfigurationValueByKey(constants.FleetApiTokenMissingScopes, "false");
        await tscConfigurationService.SetConfigurationValueByKey(constants.FleetApiTokenUnauthorizedKey, "false");
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenUnauthorized, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenMissingScopes, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenRequestExpired, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenRefreshNonSuccessStatusCode, null);
        return new(requestUrl);
    }

    public async Task GetToken(DtoBackendLogin login)
    {
        logger.LogTrace("{method}()", nameof(GetToken));

        if (string.IsNullOrEmpty(login.UserName))
        {
            throw new InvalidOperationException("Username is empty");
        }
        if (string.IsNullOrEmpty(login.Password))
        {
            throw new InvalidOperationException("Password is empty");
        }
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if (token != default)
        {
            teslaSolarChargerContext.BackendTokens.Remove(token);
        }
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var dtoLogin = new DtoLogin(login.UserName, login.Password, installationId.ToString());
        var result = await SendRequestToBackend<DtoAccessToken>(HttpMethod.Post, null, "User/Login", dtoLogin);
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
        var newToken = result.Data;
        if(newToken == default)
        {
            throw new InvalidOperationException("Could not parse token");
        }
        token = new(newToken.AccessToken, newToken.RefreshToken)
        {
            ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(newToken.ExpiresAt),
        };
        teslaSolarChargerContext.BackendTokens.Add(token);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RefreshBackendTokenIfNeeded()
    {
        logger.LogTrace("{method}(token)", nameof(RefreshBackendTokenIfNeeded));
        var tokenExpriationDate = await tokenHelper.GetBackendTokenExpirationDate();
        if(tokenExpriationDate == default)
        {
            logger.LogError("Could not refresh backend token. No token found.");
            return;
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        if(tokenExpriationDate > currentDate.AddMinutes(1))
        {
            logger.LogTrace("Token is still valid");
            return;
        }
        //As expiration date is not null a token must exist.
        var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
        var dtoRefreshToken = new DtoTokenRefreshModel(token.AccessToken, token.RefreshToken);
        var result = await SendRequestToBackend<DtoAccessToken>(HttpMethod.Post, null, "User/RefreshToken", dtoRefreshToken);
        if (result.HasError)
        {
            await errorHandlingService.HandleError(nameof(BackendApiService), nameof(RefreshBackendTokenIfNeeded),
                "Could not refresh backend token", result.ErrorMessage ?? string.Empty, issueKeys.BackendTokenNotRefreshable, null, null);
            logger.LogError("Could not refresh backend token. {errorMessage}", result.ErrorMessage);
            memoryCache.Remove(constants.BackendTokenStateKey);
            throw new InvalidOperationException($"Could not refresh backend token {result.ErrorMessage}");
        }
        await errorHandlingService.HandleErrorResolved(issueKeys.BackendTokenUnauthorized, null);
        var newToken = result.Data ?? throw new InvalidDataException("Could not parse new token");
        token.AccessToken = newToken.AccessToken;
        token.RefreshToken = newToken.RefreshToken;
        token.ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(newToken.ExpiresAt);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        memoryCache.Remove(constants.BackendTokenStateKey);
    }

    internal string GenerateAuthUrl(DtoTeslaOAuthRequestInformation oAuthInformation, string locale)
    {
        logger.LogTrace("{method}({@oAuthInformation})", nameof(GenerateAuthUrl), oAuthInformation);
        var url =
            $"https://auth.tesla.com/oauth2/v3/authorize?&client_id={Uri.EscapeDataString(oAuthInformation.ClientId)}&locale={Uri.EscapeDataString(locale)}&prompt={Uri.EscapeDataString(oAuthInformation.Prompt)}&redirect_uri={Uri.EscapeDataString(oAuthInformation.RedirectUri)}&response_type={Uri.EscapeDataString(oAuthInformation.ResponseType)}&scope={Uri.EscapeDataString(oAuthInformation.Scope)}&state={Uri.EscapeDataString(oAuthInformation.State)}&prompt_missing_scopes=true";
        return url;
    }

    public async Task PostInstallationInformation(string reason)

    {
        try
        {
            var tokenState = await tokenHelper.GetBackendTokenState(true);
            var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
            var currentVersion = await GetCurrentVersion().ConfigureAwait(false);
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            if (tokenState == TokenState.UpToDate)
            {
                var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
                var result = await SendRequestToBackend<object>(HttpMethod.Post, token.AccessToken,
                    $"Client/NotifyInstallation?version={Uri.EscapeDataString(currentVersion ?? string.Empty)}&infoReason{Uri.EscapeDataString(reason)}",
                    null);
                if (!result.HasError)
                {
                    logger.LogInformation("Sent installation information to Backend");
                    return;
                }

                logger.LogWarning("Error while sending installation information to backend. {errorMessage}", result.ErrorMessage);
            }
            var noTokenResult = await SendRequestToBackend<object>(HttpMethod.Post, null,
                $"Client/NotifyInstallationAnonymous?version={Uri.EscapeDataString(currentVersion ?? string.Empty)}&infoReason{Uri.EscapeDataString(reason)}&installationId={Uri.EscapeDataString(installationId.ToString())}",
                null);
            if (!noTokenResult.HasError)
            {
                logger.LogInformation("Sent installation information to Backend");
                return;
            }

            logger.LogWarning("Error while sending installation information to backend. {errorMessage}", noTokenResult.ErrorMessage);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not post installation information");
        }

    }

    public Task<string?> GetCurrentVersion()
    {
        logger.LogTrace("{method}()", nameof(GetCurrentVersion));
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return Task.FromResult(fileVersionInfo.ProductVersion);
    }

    public async Task GetNewBackendNotifications()
    {
        logger.LogTrace("{method}()", nameof(GetNewBackendNotifications));
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var lastKnownNotificationId = await teslaSolarChargerContext.BackendNotifications
            .OrderByDescending(n => n.BackendIssueId)
            .Select(n => n.BackendIssueId)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        var result = await SendRequestToBackend<List<DtoBackendNotification>>(HttpMethod.Get, null,
            $"Tsc/GetBackendNotifications?installationId={installationId}&lastKnownNotificationId={lastKnownNotificationId}", null);
        if (result.HasError)
        {
            logger.LogError("Could not load new Backend Information. {errorMessage}", result.ErrorMessage);
        }
        var notifications = result.Data ?? throw new InvalidDataException("Could not parse notifications");
        foreach (var dtoBackendNotification in notifications)
        {
            teslaSolarChargerContext.BackendNotifications.Add(new BackendNotification
            {
                BackendIssueId = dtoBackendNotification.Id,
                Type = dtoBackendNotification.Type,
                Headline = dtoBackendNotification.Headline,
                DetailText = dtoBackendNotification.DetailText,
                ValidFromDate = dtoBackendNotification.ValidFromDate,
                ValidToDate = dtoBackendNotification.ValidToDate,
                ValidFromVersion = dtoBackendNotification.ValidFromVersion,
                ValidToVersion = dtoBackendNotification.ValidToVersion,
            });
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<Dtos.Result<T>> SendRequestToBackend<T>(HttpMethod httpMethod, string? accessToken, string requestUrlPart, object? content)
    {
        var request = new HttpRequestMessage();
        var finalUrl = configurationWrapper.BackendApiBaseUrl() + requestUrlPart;
        request.RequestUri = new Uri(finalUrl);
        if (accessToken != default)
        {
            request.Headers.Authorization = new("Bearer", accessToken);
        }
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            if (httpMethod == HttpMethod.Get)
            {
                request.Method = HttpMethod.Get;
            }
            else if (httpMethod == HttpMethod.Post)
            {
                request.Method = HttpMethod.Post;
                if (content != default)
                {
                    var jsonContent = new StringContent(
                        JsonConvert.SerializeObject(content),
                        System.Text.Encoding.UTF8,
                        "application/json");
                    request.Content = jsonContent;
                }
            }
            else
            {
                return new Dtos.Result<T>(
                    default,
                    $"Unsupported HTTP method: {httpMethod}",
                    null
                );
            }
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (typeof(T) != typeof(object))
                {
                    var deserializedObject = JsonConvert.DeserializeObject<T>(responseContent);

                    if (deserializedObject == null)
                    {
                        return new Dtos.Result<T>(
                            default,
                            $"{finalUrl}: Could not deserialize response to {typeof(T).Name}.",
                            null
                        );
                    }

                    return new Dtos.Result<T>(deserializedObject, null, null);
                }
                else
                {
                    // If T=object, we don't do any deserialization
                    return new Dtos.Result<T>(
                        default,
                        null,
                        null
                    );
                }
            }
            else
            {
                var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                var message = problemDetails != null
                    ? $"Backend Error: {problemDetails.Detail}"
                    : "An error occurred while retrieving data from the backend server.";

                return new Dtos.Result<T>(default, message, problemDetails);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while sending request to backend");
            return new Dtos.Result<T>(
                default,
                $"{finalUrl}: Unexpected error: {ex.Message}", null
            );
        }

    }
}
