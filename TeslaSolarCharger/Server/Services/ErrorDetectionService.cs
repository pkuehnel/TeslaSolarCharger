using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ErrorDetectionService(ILogger<ErrorDetectionService> logger,
    IErrorHandlingService errorHandlingService,
    IDateTimeProvider dateTimeProvider,
    ISettings settings,
    ITeslaSolarChargerContext context,
    IConfigurationWrapper configurationWrapper,
    IIssueKeys issueKeys,
    ITokenHelper tokenHelper,
    IConstants constants,
    IFleetTelemetryWebSocketService fleetTelemetryWebSocketService,
    IBackendApiService backendApiService) : IErrorDetectionService
{
    public async Task DetectErrors()
    {
        var activeErrors = await context.LoggedErrors
            .Where(e => e.EndTimeStamp == default)
            .ToListAsync().ConfigureAwait(false);
        foreach (var error in activeErrors)
        {
            if (error.Vin == null || settings.CarsToManage.Any(c => c.Vin == error.Vin))
            {
                continue;
            }
            logger.LogDebug("Remove error with ID {id} as it belongs to a car that should not be managed.", error.Id);
            error.EndTimeStamp = dateTimeProvider.UtcNow();
        }
        await context.SaveChangesAsync().ConfigureAwait(false);

        await AddOrRemoveErrors(activeErrors, issueKeys.RestartNeeded, "TSC restart needed",
        "Due to configuration changes a restart of TSC is needed.", settings.RestartNeeded).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.CrashedOnStartup, "TSC crashed on startup",
            $"Exeption Message: <code>{settings.StartupCrashMessage}</code>", settings.CrashedOnStartup).ConfigureAwait(false);


        var pvValueUpdateAge = dateTimeProvider.DateTimeOffSetUtcNow() - settings.LastPvValueUpdate;
        var solarValuesTooOld = (pvValueUpdateAge > (configurationWrapper.PvValueJobUpdateIntervall() * 3)) && (
                                await context.ModbusResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false)
                              || await context.RestValueResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false)
                              || await context.MqttResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false));
        await AddOrRemoveErrors(activeErrors, issueKeys.SolarValuesNotAvailable, "Solar values are not available",
            $"Solar values are {pvValueUpdateAge} old. It looks like there is something wrong when trying to get the solar values.", solarValuesTooOld).ConfigureAwait(false);

        await AddOrRemoveErrors(activeErrors, issueKeys.BaseAppNotLicensed, "Base App not licensed",
            "Can not send commands to car as app is not licensed", !await backendApiService.IsBaseAppLicensed(true));

        //ToDo: if last check there was no token related issue, only detect token related issues every x minutes as creates high load in backend
        await DetectTokenStateIssues(activeErrors);
        foreach (var car in settings.CarsToManage)
        {
            if ((car.LastNonSuccessBleCall != default)
                && (car.LastNonSuccessBleCall.Value > (dateTimeProvider.UtcNow() - configurationWrapper.BleUsageStopAfterError())))
            {
                //Issue should already be active as is set on TeslaFleetApiService.
                //Note: The same logic for the if is used in TeslaFleetApiService.SendCommandToTeslaApi<T> if ble is enabled.
                //So: let it be like that even though the if part is empty.
            }
            else
            {
                //ToDo: In a future release this should only be done if no fleet api request was sent the last x minutes (BleUsageStopAfterError)
                await errorHandlingService.HandleErrorResolved(issueKeys.UsingFleetApiAsBleFallback, car.Vin);
            }
            var fleetTelemetryEnabled = await context.Cars
                .Where(c => c.Vin == car.Vin)
                .Select(c => c.UseFleetTelemetry)
                .FirstOrDefaultAsync();

            if (fleetTelemetryEnabled && (!fleetTelemetryWebSocketService.IsClientConnected(car.Vin)))
            {
                await errorHandlingService.HandleError(nameof(ErrorHandlingService), nameof(DetectErrors), $"Fleet Telemetry not connected for car {car.Vin}",
                    "Fleet telemetry is not connected. Please check the connection.", issueKeys.FleetTelemetryNotConnected, car.Vin, null);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetTelemetryNotConnected, car.Vin);
            }

            if (car.State is CarStateEnum.Asleep or CarStateEnum.Offline)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.GetVehicleData, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.VehicleDataRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.VehicleDataRequestUrl, car.Vin);
            }

            if (car.State != CarStateEnum.Asleep && car.State != CarStateEnum.Offline && car.State != CarStateEnum.Unknown)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.WakeUpRequestUrl, car.Vin);
            }
            if (car.State is CarStateEnum.Charging)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleCommandNoSuccess + constants.ChargeStartRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.ChargeStartRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.ChargeStartRequestUrl, car.Vin);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleCommandNoSuccess + constants.ChargeStopRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.ChargeStopRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.ChargeStopRequestUrl, car.Vin);
            }
        }
    }

    private async Task DetectTokenStateIssues(List<LoggedError> activeErrors)
    {
        logger.LogTrace("{method}()", nameof(DetectTokenStateIssues));
        var backendTokenState = await tokenHelper.GetBackendTokenState(true);
        var fleetApiTokenState = await tokenHelper.GetFleetApiTokenState(true);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoBackendApiToken, "Backend API Token not up to date",
            "You are currently not connected to the backend. Open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token.",
            backendTokenState != TokenState.UpToDate).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenUnauthorized, "Fleet API token is unauthorized",
            "You recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Unauthorized).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoFleetApiToken, "No Fleet API Token available.",
            "Open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token.",
            fleetApiTokenState == TokenState.NotAvailable).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenExpired, "Fleet API token is expired",
            "Either you recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Expired).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenMissingScopes, "Your Tesla token has missing scopes.",
            "Open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Note: You need to allow all selectable scopes as otherwise TSC won't work properly.",
            fleetApiTokenState == TokenState.MissingScopes).ConfigureAwait(false);

        //Remove all fleet api related issue keys on token error because very likely it is because of the underlaying token issue.
        if (fleetApiTokenState != TokenState.UpToDate)
        {
            foreach (var activeError in activeErrors.Where(activeError => activeError.IssueKey.StartsWith(issueKeys.GetVehicleData)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.CarStateUnknown)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.FleetApiNonSuccessStatusCode)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.FleetApiNonSuccessResult)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.UnsignedCommand)))
            {
                activeError.EndTimeStamp = dateTimeProvider.UtcNow();
            }

            await context.SaveChangesAsync();
        }
    }

    private async Task AddOrRemoveErrors(List<LoggedError> activeErrors, string issueKey, string headline, string message, bool shouldBeActive)
    {
        var filteredErrors = activeErrors.Where(e => e.IssueKey == issueKey).ToList();
        if (shouldBeActive && filteredErrors.Count < 1)
        {
            var loggedError = new LoggedError()
            {
                StartTimeStamp = dateTimeProvider.UtcNow(),
                IssueKey = issueKey,
                Source = nameof(ErrorHandlingService),
                MethodName = nameof(AddOrRemoveErrors),
                Headline = headline,
                Message = message,
            };
            context.LoggedErrors.Add(loggedError);
        }
        else if (shouldBeActive)
        {
            for (var i = 0; i < filteredErrors.Count; i++)
            {
                if (i == 0)
                {
                    filteredErrors[i].FurtherOccurrences.Add(dateTimeProvider.UtcNow());
                }
                else
                {
                    logger.LogWarning("More than one error with issue key {issueKey} was active", issueKey);
                    filteredErrors[i].EndTimeStamp = dateTimeProvider.UtcNow();
                }
            }
        }
        else if (!shouldBeActive && filteredErrors.Count > 0)
        {
            foreach (var filteredError in filteredErrors)
            {
                filteredError.EndTimeStamp = dateTimeProvider.UtcNow();
            }
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

}
