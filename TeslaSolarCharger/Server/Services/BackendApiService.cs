using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend.User;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
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
    IPasswordGenerationService passwordGenerationService)
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
        var backendApiBaseUrl = configurationWrapper.BackendApiBaseUrl();
        var encryptionKey = passwordGenerationService.GeneratePassword(32);
        var state = Guid.NewGuid();
        var requestUri = $"{backendApiBaseUrl}Client/AddAuthenticationStartInformation?redirectUri={Uri.EscapeDataString(baseUrl)}&encryptionKey={Uri.EscapeDataString(encryptionKey)}&state={Uri.EscapeDataString(state.ToString())}";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Can not start Tesla O Auth without backend token");
        }
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var oAuthRequestInformation = JsonConvert.DeserializeObject<DtoTeslaOAuthRequestInformation>(responseString) ?? throw new InvalidDataException("Could not get oAuth data");
        var requestUrl = GenerateAuthUrl(oAuthRequestInformation, locale);
        var tokenRequested = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.FleetApiTokenRequested)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequested == null)
        {
            var config = new TscConfiguration
            {
                Key = constants.FleetApiTokenRequested,
                Value = dateTimeProvider.UtcNow().ToString("O"),
            };
            teslaSolarChargerContext.TscConfigurations.Add(config);
        }
        else
        {
            tokenRequested.Value = dateTimeProvider.UtcNow().ToString("O");
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await errorHandlingService.HandleError(nameof(BackendApiService), nameof(StartTeslaOAuth), "Waiting for Tesla token",
            "Waiting for the Tesla Token from the TSC backend. This might take up to five minutes. If after five minutes this error is still displayed, open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            issueKeys.FleetApiTokenNotReceived, null, null);
        //Do not set FleetApiTokenNotReceived to resolved here, as the token might still be in transit
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenNotRequested, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenUnauthorized, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenMissingScopes, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenRequestExpired, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenExpired, null);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenRefreshNonSuccessStatusCode, null);
        return new DtoValue<string>(requestUrl);
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
        var url = configurationWrapper.BackendApiBaseUrl() + "User/Login";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var response = await httpClient.PostAsJsonAsync(url, dtoLogin).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Could not login to backend. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            throw new InvalidOperationException("Could not login to backend");
        }
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var newToken = JsonConvert.DeserializeObject<DtoAccessToken>(responseContent) ?? throw new InvalidDataException("Could not parse token");
        token = new(newToken.AccessToken, newToken.RefreshToken)
        {
            ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(newToken.ExpiresAt),
        };
        teslaSolarChargerContext.BackendTokens.Add(token);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<DtoValue<bool>> HasValidBackendToken()
    {
        logger.LogTrace("{method}", nameof(HasValidBackendToken));
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if (token == default)
        {
            return new(false);
        }
        var url = configurationWrapper.BackendApiBaseUrl() + "Client/IsTokenValid";
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token.AccessToken);
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(false);
        }
        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Could not check if token is valid. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            throw new InvalidOperationException("Could not check if token is valid");
        }
        return new(true);
    }

    public async Task RefreshBackendToken()
    {
        logger.LogTrace("{method}(token)", nameof(RefreshBackendToken));
        var url = configurationWrapper.BackendApiBaseUrl() + "User/RefreshToken";
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if(token == default)
        {
            logger.LogError("Could not refresh backend token. No token found");
            return;
        }
        var dtoRefreshToken = new DtoTokenRefreshModel(token.AccessToken, token.RefreshToken);
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        var response = await httpClient.PostAsJsonAsync(url, dtoRefreshToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.LogError("Could not refresh backend token. StatusCode: {statusCode}, resultBody: {resultBody}", response.StatusCode, responseString);
            throw new InvalidOperationException("Could not refresh backend token");
        }
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var newToken = JsonConvert.DeserializeObject<DtoAccessToken>(responseContent) ?? throw new InvalidDataException("Could not parse token");
        token.AccessToken = newToken.AccessToken;
        token.RefreshToken = newToken.RefreshToken;
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
            var url = configurationWrapper.BackendApiBaseUrl() + "Tsc/NotifyInstallation";
            var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
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
}
