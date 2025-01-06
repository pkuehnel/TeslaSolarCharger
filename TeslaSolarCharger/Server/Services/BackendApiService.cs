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
        var configEntriesToRemove = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .ToListAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TscConfigurations.RemoveRange(configEntriesToRemove);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
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
        var url = configurationWrapper.BackendApiBaseUrl() + "User/RefreshToken";
        //As expiration date is not null a token must exist.
        var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
        var dtoRefreshToken = new DtoTokenRefreshModel(token.AccessToken, token.RefreshToken);
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var response = await httpClient.PostAsJsonAsync(url, dtoRefreshToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Could not refresh backend token. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            memoryCache.Remove(constants.BackendTokenStateKey);
            throw new InvalidOperationException("Could not refresh backend token");
        }
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var newToken = JsonConvert.DeserializeObject<DtoAccessToken>(responseContent) ?? throw new InvalidDataException("Could not parse token");
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
                var url = configurationWrapper.BackendApiBaseUrl() + $"Client/NotifyInstallation?version={Uri.EscapeDataString(currentVersion ?? string.Empty)}&infoReason{Uri.EscapeDataString(reason)}";
                var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new("Bearer", token.AccessToken);
                var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Sent installation information to Backend");
                    return;
                }

                logger.LogWarning("Error while sending installation information to backend. StatusCode: {statusCode}. Trying again without token", response.StatusCode);
            }
            var noTokenUrl = configurationWrapper.BackendApiBaseUrl() + $"Client/NotifyInstallationAnonymous?version={Uri.EscapeDataString(currentVersion ?? string.Empty)}&infoReason{Uri.EscapeDataString(reason)}&installationId={Uri.EscapeDataString(installationId.ToString())}";
            var nonTokenRequest = new HttpRequestMessage(HttpMethod.Post, noTokenUrl);
            var nonTokenResponse = await httpClient.SendAsync(nonTokenRequest).ConfigureAwait(false);
            if (nonTokenResponse.IsSuccessStatusCode)
            {
                logger.LogInformation("Sent installation information to Backend");
                return;
            }

            logger.LogWarning("Error while sending installation information to backend. StatusCode: {statusCode}.", nonTokenResponse.StatusCode);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not post installation information");
        }

    }

    public async Task PostErrorInformation(string source, string methodName, string message, string issueKey, string? vin,
        string? stackTrace)
    {
        try
        {
            var url = configurationWrapper.BackendApiBaseUrl() + "Tsc/NotifyError";
            var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
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
            logger.LogError(e, "Could not post error information");
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
        var url = configurationWrapper.BackendApiBaseUrl() + $"Tsc/GetBackendNotifications?installationId={installationId}&lastKnownNotificationId={lastKnownNotificationId}";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var response = await httpClient.GetStringAsync(url).ConfigureAwait(false);
        var notifications = JsonConvert.DeserializeObject<List<DtoBackendNotification>>(response) ?? throw new InvalidDataException("Could not parse notifications");

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

    private async Task<Result<T>> SendRequestToBackend<T>(HttpMethod httpMethod, string? accessToken, string requestUrlPart, object? content)
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
                return new Result<T>(
                    default,
                    $"Unsupported HTTP method: {httpMethod}"
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
                        return new Result<T>(
                            default,
                            $"{finalUrl}: Could not deserialize response to {typeof(T).Name}."
                        );
                    }

                    return new Result<T>(deserializedObject, null);
                }
                else
                {
                    // If T=object, we don't do any deserialization
                    return new Result<T>(
                        default,
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

                return new Result<T>(default, message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while sending request to backend");
            return new Result<T>(
                default,
                $"{finalUrl}: Unexpected error: {ex.Message}"
            );
        }

    }
}
