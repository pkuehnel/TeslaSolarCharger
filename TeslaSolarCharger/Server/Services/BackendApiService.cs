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
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BackendApiService(
    ILogger<BackendApiService> logger,
    ITscConfigurationService tscConfigurationService,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    ISettings settings)
    : IBackendApiService
{
    public async Task<DtoValue<string>> StartTeslaOAuth(string locale, string baseUrl)
    {
        logger.LogTrace("{method}()", nameof(StartTeslaOAuth));
        var currentTokens = await teslaSolarChargerContext.TeslaTokens.ToListAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TeslaTokens.RemoveRange(currentTokens);
        var cconfigEntriesToRemove = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .ToListAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TscConfigurations.RemoveRange(cconfigEntriesToRemove);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var backendApiBaseUrl = configurationWrapper.BackendApiBaseUrl();
        using var httpClient = new HttpClient();
        var requestUri = $"{backendApiBaseUrl}Tsc/StartTeslaOAuth?installationId={Uri.EscapeDataString(installationId.ToString())}&baseUrl={Uri.EscapeDataString(baseUrl)}";
        var responseString = await httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
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
        return new DtoValue<string>(requestUrl);
    }

    internal string GenerateAuthUrl(DtoTeslaOAuthRequestInformation oAuthInformation, string locale)
    {
        logger.LogTrace("{method}({@oAuthInformation})", nameof(GenerateAuthUrl), oAuthInformation);
        var url =
            $"https://auth.tesla.com/oauth2/v3/authorize?&client_id={Uri.EscapeDataString(oAuthInformation.ClientId)}&locale={Uri.EscapeDataString(locale)}&prompt={Uri.EscapeDataString(oAuthInformation.Prompt)}&redirect_uri={Uri.EscapeDataString(oAuthInformation.RedirectUri)}&response_type={Uri.EscapeDataString(oAuthInformation.ResponseType)}&scope={Uri.EscapeDataString(oAuthInformation.Scope)}&state={Uri.EscapeDataString(oAuthInformation.State)}";
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

    public async Task PostErrorInformation(string source, string methodName, string message, string? stackTrace = null)
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

    public async Task PostTeslaApiCallStatistics()
    {
        logger.LogTrace("{method}()", nameof(PostTeslaApiCallStatistics));
        var shouldTransferDate = configurationWrapper.SendTeslaApiStatsToBackend();
        var currentDate = dateTimeProvider.UtcNow().Date;
        if (!shouldTransferDate)
        {
            logger.LogWarning("You manually disabled tesla API stats transfer to the backend. This means your usage won't be considered in future optimizations.");
            return;
        }

        Func<DateTime, bool> predicate = d => d > (currentDate.AddDays(-1)) && (d < currentDate);
        var cars = settings.Cars.Where(c => c.WakeUpCalls.Count(predicate) > 0
                                             || c.VehicleDataCalls.Count(predicate) > 0
                                             || c.VehicleCalls.Count(predicate) > 0
                                             || c.ChargeStartCalls.Count(predicate) > 0
                                             || c.ChargeStopCalls.Count(predicate) > 0
                                             || c.SetChargingAmpsCall.Count(predicate) > 0
                                             || c.OtherCommandCalls.Count(predicate) > 0).ToList();

        var getVehicleDataFromTesla = configurationWrapper.GetVehicleDataFromTesla();
        foreach (var car in cars)
        {
            var statistics = new DtoTeslaApiCallStatistic
            {
                Date = DateOnly.FromDateTime(currentDate.AddDays(-1)),
                InstallationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false),
                StartupTime = settings.StartupTime,
                GetDataFromTesla = getVehicleDataFromTesla,
                ApiRefreshInterval = car.ApiRefreshIntervalSeconds,
                UseBle = car.UseBle,
                Vin = car.Vin,
                WakeUpCalls = car.WakeUpCalls.Where(predicate).ToList(),
                VehicleDataCalls = car.VehicleDataCalls.Where(predicate).ToList(),
                VehicleCalls = car.VehicleCalls.Where(predicate).ToList(),
                ChargeStartCalls = car.ChargeStartCalls.Where(predicate).ToList(),
                ChargeStopCalls = car.ChargeStopCalls.Where(predicate).ToList(),
                SetChargingAmpsCall = car.SetChargingAmpsCall.Where(predicate).ToList(),
                OtherCommandCalls = car.OtherCommandCalls.Where(predicate).ToList(),
            };
            var url = configurationWrapper.BackendApiBaseUrl() + "Tsc/NotifyTeslaApiCallStatistics";
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var response = await httpClient.PostAsJsonAsync(url, statistics).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not post tesla api call statistics");
            }
            
        }

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
