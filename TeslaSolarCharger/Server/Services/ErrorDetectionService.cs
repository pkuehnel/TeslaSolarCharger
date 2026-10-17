using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

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
    IBackendApiService backendApiService,
    IFleetApiRateLimitService fleetApiRateLimitService,
    IGenericValueService genericValueService) : IErrorDetectionService
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

        await AddOrRemoveErrors(activeErrors, issueKeys.RestartNeeded, "Solar4Car restart needed",
        "Due to configuration changes a restart of Solar4Car is needed.", settings.RestartNeeded).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.CrashedOnStartup, "Solar4Car crashed on startup",
            $"Exception Message: <code>{settings.StartupCrashMessage}</code>", settings.CrashedOnStartup).ConfigureAwait(false);


        var pvValueUpdateAge = dateTimeProvider.DateTimeOffSetUtcNow() - settings.LastPvValueUpdate;
        await AddOrRemoveErrors(activeErrors, issueKeys.SolarValuesNotAvailable, "Solar values are not available",
            $"Solar values are {pvValueUpdateAge} old. It looks like there is something wrong when trying to get the solar values.", AreSolarValuesTooOld(pvValueUpdateAge)).ConfigureAwait(false);

        var backendTokenState = await tokenHelper.GetBackendTokenState(true);
        var backendTokenUpToDate = backendTokenState == TokenState.UpToDate;
        var fleetApiTokenState = await tokenHelper.GetFleetApiTokenState(true);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoBackendApiToken, "Backend API Token not up to date",
            "You are currently not connected to the backend. Open the <a href=\"/cloudconnection\">Cloud Connection</a> page and log in with your <a href=\"https://solar4car.com/\">solar4car.com</a> account.",
                !backendTokenUpToDate).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenUnauthorized, "Tesla cloud token is unauthorized",
            "You recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Unauthorized).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoFleetApiToken, "No Tesla cloud token available.",
            "Open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token.",
            fleetApiTokenState == TokenState.NotAvailable).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenExpired, "Tesla cloud token is expired",
            "Either you recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Expired).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenMissingScopes, "Your Tesla token has missing scopes.",
            "Open the <a href=\"/cloudconnection\">Cloud Connection</a> and request a new token. Note: You need to allow all selectable scopes as otherwise Solar4Car won't work properly.",
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

        await DetectBaseAppLicenseIssue(activeErrors, backendTokenUpToDate).ConfigureAwait(false);

        foreach (var car in settings.CarsToManage)
        {
            var carSettings = await context.Cars
                .Where(c => c.Vin == car.Vin)
                .Select(c => new
                {
                    c.UseFleetTelemetry,
                    c.IncludeTrackingRelevantFields,
                    c.UseBle,
                    c.CarType,
                })
                .FirstOrDefaultAsync();

            if ((carSettings?.UseFleetTelemetry == true) && (!fleetTelemetryWebSocketService.IsClientConnected(car.Vin)))
            {
                await errorHandlingService.HandleError(nameof(ErrorHandlingService), nameof(DetectErrors), $"Fleet Telemetry not connected for car {car.Vin}",
                    "Fleet telemetry is not connected. Please check your internet connection and Token state in Cloud Connection. If both have no issues, it is likely a server side issue and will be resolved as soon as possible.", issueKeys.FleetTelemetryNotConnected, car.Vin, null);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetTelemetryNotConnected, car.Vin);
            }

            var isFleetApiLicensed = await backendApiService.IsFleetApiLicensed(car.Vin, true).ConfigureAwait(false);
            if (((carSettings?.IncludeTrackingRelevantFields == true) || (carSettings?.UseBle != true))
                && (!isFleetApiLicensed)
                && carSettings?.CarType == CarType.Tesla)
            {
                await errorHandlingService.HandleError(nameof(ErrorHandlingService), nameof(DetectErrors), $"No Car License for car {car.Vin}",
                    "The car has no Car License. Enable BLE for the car and disable include tracking relevant fields, or buy a Car License for that car. Note: After buying a Car License you need to restart Solar4Car as otherwise it takes up to six hours until Solar4Car detects the change.", issueKeys.FleetApiNotLicensed, car.Vin, null);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNotLicensed, car.Vin);
            }

            //Resolved here and not only after a successful Fleet API command, as the rate limit is also irrelevant again when BLE
            //works again, the car needs no commands at all or a Fleet API license was bought. In all those cases no Fleet API
            //command is sent that could resolve the issue.
            if (!fleetApiRateLimitService.IsKnownToBeBlocked(car, FleetApiBudgetKind.Commands))
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiCommandRateLimited, car.Vin);
            }

            if (car.IsOnline.Value == false)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.GetVehicleData, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.VehicleDataRequestUrl, car.Vin);
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.VehicleDataRequestUrl, car.Vin);
            }

            if (car.IsOnline.Value == true)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.WakeUpRequestUrl, car.Vin);
            }
            if (car.IsCharging.Value == true)
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

    /// <summary>
    /// The solar values are too old when they were not updated for three runs while at least one device is configured.
    /// Every configured device (REST, Modbus, MQTT and templates) is in the value registry, the fake device is not configured.
    /// </summary>
    internal bool AreSolarValuesTooOld(TimeSpan pvValueUpdateAge)
    {
        return pvValueUpdateAge > configurationWrapper.PvValueJobUpdateIntervall() * 3
               && genericValueService
                   .GetAllByPredicate(v => v.SourceValueKey.ConfigurationType != ConfigurationType.FakeSolarValue)
                   .Count > 0;
    }

    private async Task DetectTokenStateIssues(List<LoggedError> activeErrors)
    {
        logger.LogTrace("{method}()", nameof(DetectTokenStateIssues));
    }

    internal async Task DetectBaseAppLicenseIssue(List<LoggedError> activeErrors, bool backendTokenUpToDate)
    {
        var isBaseAppLicensed = await backendApiService.IsBaseAppLicensed(true).ConfigureAwait(false);
        string headline;
        string message;
        if (isBaseAppLicensed.Data == false)
        {
            headline = "No Solar4Car base license";
            message = "Your Solar4Car account has no active base license, so Solar4Car does not control any charging: "
                      + "no commands are sent to your cars or chargers, including cars connected via BLE. "
                      + "The base license is required for every installation, a Car License is only needed in addition for using the Tesla cloud. "
                      + $"Get it at <a href=\"{constants.SubscriptionsUrl}\" target=\"_blank\">solar4car.com/subscriptions</a>. "
                      + "After buying, restart Solar4Car as otherwise it takes up to six hours until the license is detected.";
        }
        else
        {
            headline = "Could not check if Solar4Car is licensed";
            message = isBaseAppLicensed.ErrorMessage ?? string.Empty;
        }
        //Only show error message if backend token is up to date.
        var shouldBeActive = backendTokenUpToDate && (isBaseAppLicensed.Data != true);
        await AddOrRemoveErrors(activeErrors, issueKeys.BaseAppNotLicensed, headline, message, shouldBeActive).ConfigureAwait(false);
    }

    /// <summary>
    /// Headline and message of an error that is still active are updated, so it shows the current state and texts
    /// changed by an update reach errors that were opened by an older version.
    /// </summary>
    internal async Task AddOrRemoveErrors(List<LoggedError> activeErrors, string issueKey, string headline, string message, bool shouldBeActive)
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
                    filteredErrors[i].Headline = headline;
                    filteredErrors[i].Message = message;
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
