using LanguageExt;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.LoggedError;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;
using Error = LanguageExt.Common.Error;

namespace TeslaSolarCharger.Server.Services;

public class ErrorHandlingService(ILogger<ErrorHandlingService> logger,
    IIssueKeys issueKeys,
    ITelegramService telegramService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    ISettings settings,
    ITokenHelper tokenHelper,
    IPossibleIssues possibleIssues,
    IConstants constants,
    IFleetTelemetryWebSocketService fleetTelemetryWebSocketService) : IErrorHandlingService
{
    public async Task<Fin<List<DtoLoggedError>>> GetActiveLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        var unfilterdErrorsFin = await GetUnfilterdLoggedErrors();
        return unfilterdErrorsFin.Match(
            Succ: unfilteredErrors =>
            {
                var errors = unfilteredErrors
                    .Where(e => e.DismissedAt == default || (e.FurtherOccurrences.Any() && (e.DismissedAt < e.FurtherOccurrences.Max())))
                    .Select(e => new DtoLoggedError()
                    {
                        Id = e.Id,
                        Severity = possibleIssues.GetIssueByKey(e.IssueKey).IssueSeverity,
                        Headline = e.Headline,
                        IssueKey = e.IssueKey,
                        OccurrenceCount = e.FurtherOccurrences.Count() + 1,
                        Vin = e.Vin,
                        Message = e.Message,
                        HideOccurrenceCount = possibleIssues.GetIssueByKey(e.IssueKey).HideOccurrenceCount
                    })
                    .ToList();

                var removedErrorCount = errors.RemoveAll(e => e.OccurrenceCount < possibleIssues.GetIssueByKey(e.IssueKey).ShowErrorAfterOccurrences);
                logger.LogDebug("{removedErrorsCount} errors removed as did not reach minimum error count", removedErrorCount);
                return Fin<System.Collections.Generic.List<DtoLoggedError>>.Succ(errors);
            },
            Fail: error =>
            {
                return Fin<System.Collections.Generic.List<DtoLoggedError>>.Fail(error);
            });

    }
    
    public async Task<Fin<List<DtoHiddenError>>> GetHiddenErrors()
    {
        logger.LogTrace("{method}()", nameof(GetHiddenErrors));
        var unfilterdErrorsFin = await GetUnfilterdLoggedErrors();
        return unfilterdErrorsFin.Match(
            Succ: unfilteredErrors =>
            {
                var hiddenErrors = new List<DtoHiddenError>();
                foreach (var loggedError in unfilteredErrors)
                {
                    var occurrences = new List<DateTime>() { loggedError.StartTimeStamp }.Concat(loggedError.FurtherOccurrences).ToList();
                    var hiddenError = new DtoHiddenError()
                    {
                        Id = loggedError.Id,
                        Severity = possibleIssues.GetIssueByKey(loggedError.IssueKey).IssueSeverity,
                        Headline = loggedError.Headline,
                        IssueKey = loggedError.IssueKey,
                        OccurrenceCount = occurrences.Count,
                        Vin = loggedError.Vin,
                        Message = loggedError.Message,
                        HideOccurrenceCount = possibleIssues.GetIssueByKey(loggedError.IssueKey).HideOccurrenceCount,
                    };
                    if (occurrences.Count
                        < possibleIssues.GetIssueByKey(loggedError.IssueKey).ShowErrorAfterOccurrences)
                    {
                        hiddenError.HideReason = LoggedErrorHideReason.NotEnoughOccurrences;
                        hiddenErrors.Add(hiddenError);
                    }
                    else if(loggedError.DismissedAt > occurrences.Max())
                    {
                        hiddenError.HideReason = LoggedErrorHideReason.Dismissed;
                        hiddenErrors.Add(hiddenError);
                    }
                }
                return Fin<List<DtoHiddenError>>.Succ(hiddenErrors);
            },
            Fail: error =>
            {
                return Fin<List<DtoHiddenError>>.Fail(error);
            });
    }

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
        var solarValuesTooOld = (pvValueUpdateAge > (configurationWrapper.PvValueJobUpdateIntervall() * 3)) &&(
                                await context.ModbusResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false)
                              || await context.RestValueResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false)
                              || await context.MqttResultConfigurations.Where(r => r.UsedFor <= ValueUsage.HomeBatterySoc).AnyAsync().ConfigureAwait(false));
        await AddOrRemoveErrors(activeErrors, issueKeys.SolarValuesNotAvailable, "Solar values are not available",
            $"Solar values are {pvValueUpdateAge} old. It looks like there is something wrong when trying to get the solar values.", solarValuesTooOld).ConfigureAwait(false);

        await AddOrRemoveErrors(activeErrors, issueKeys.VersionNotUpToDate, "New software version available",
            "Update TSC to the latest version.", settings.IsNewVersionAvailable).ConfigureAwait(false);

        //ToDO: fix next line, currently not working due to cyclic reference
        //await AddOrRemoveErrors(activeErrors, issueKeys.BaseAppNotLicensed, "Base App not licensed",
        //    "Can not send commands to car as app is not licensed", !await backendApiService.IsBaseAppLicensed(true));

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
                await HandleErrorResolved(issueKeys.UsingFleetApiAsBleFallback, car.Vin);
            }
            var fleetTelemetryEnabled = await context.Cars
                .Where(c => c.Vin == car.Vin)
                .Select(c => c.UseFleetTelemetry)
                .FirstOrDefaultAsync();
                
            if (fleetTelemetryEnabled && (!fleetTelemetryWebSocketService.IsClientConnected(car.Vin)))
            {
                await HandleError(nameof(ErrorHandlingService), nameof(DetectErrors), $"Fleet Telemetry not connected for car {car.Vin}",
                    "Fleet telemetry is not connected. Please check the connection.", issueKeys.FleetTelemetryNotConnected, car.Vin, null);
            }
            else
            {
                await HandleErrorResolved(issueKeys.FleetTelemetryNotConnected, car.Vin);
            }

            if (car.State is CarStateEnum.Asleep or CarStateEnum.Offline)
            {
                await HandleErrorResolved(issueKeys.GetVehicleData, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.VehicleDataRequestUrl, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.VehicleDataRequestUrl, car.Vin);
            }

            if (car.State != CarStateEnum.Asleep && car.State != CarStateEnum.Offline && car.State != CarStateEnum.Unknown)
            {
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.WakeUpRequestUrl, car.Vin);
            }
            if (car.State is CarStateEnum.Charging)
            {
                await HandleErrorResolved(issueKeys.BleCommandNoSuccess + constants.ChargeStartRequestUrl, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.ChargeStartRequestUrl, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.ChargeStartRequestUrl, car.Vin);
            }
            else
            {
                await HandleErrorResolved(issueKeys.BleCommandNoSuccess + constants.ChargeStopRequestUrl, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + constants.ChargeStopRequestUrl, car.Vin);
                await HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + constants.ChargeStopRequestUrl, car.Vin);
            }
        }
    }

    public async Task<DtoValue<int>> ErrorCount()
    {
        var count = await GetActiveIssueCountBySeverity(IssueSeverity.Error).ConfigureAwait(false);
        return new(count);
    }

    public async Task<DtoValue<int>> WarningCount()
    {
        var count = await GetActiveIssueCountBySeverity(IssueSeverity.Warning).ConfigureAwait(false);
        return new(count);
    }
    
    public async Task<Fin<int>> DismissError(int errorIdValue)
    {
        logger.LogTrace("{method}({errorId})", nameof(DismissError), errorIdValue);
        var error = await context.LoggedErrors.FindAsync(errorIdValue);
        if (error == default)
        {
            return Fin<int>.Fail(Error.New(new KeyNotFoundException("Could not find error with specified ID")));
        }

        error.DismissedAt = dateTimeProvider.UtcNow();
        try
        {
            await context.SaveChangesAsync();
            return Fin<int>.Succ(errorIdValue);
        }
        catch (Exception ex)
        {
            return Fin<int>.Fail(Error.New(ex));
        }
    }

    public async Task HandleError(string source, string methodName, string headline, string message, string issueKey, string? vin,
        string? stackTrace)
    {
        logger.LogTrace("{method}({source}, {methodName}, {message}, {issueKey}, {vin}, {stackTrace})",
            nameof(HandleError), source, methodName, message, issueKey, vin, stackTrace);
        //ToDo: maybe send error information again in the future. This currently leads to a circular dependency
        //await backendApiService.PostErrorInformation(source, methodName, message, issueKey, vin, stackTrace);
        var existingError = await context.LoggedErrors
            .Where(e => e.IssueKey == issueKey
                        && e.Vin == vin
                        && e.EndTimeStamp == null)
            .FirstOrDefaultAsync();
        if (existingError == default)
        {
            var error = new LoggedError()
            {
                StartTimeStamp = dateTimeProvider.UtcNow(),
                IssueKey = issueKey,
                Vin = vin,
                Source = source,
                MethodName = methodName,
                Headline = headline,
                Message = message,
                StackTrace = stackTrace,
            };
            context.LoggedErrors.Add(error);
        }
        else
        {
            existingError.FurtherOccurrences.Add(dateTimeProvider.UtcNow());
            var isChanged = false;
            if (existingError.Source != source)
            {
                existingError.Source = source;
                isChanged = true;
            }

            if (existingError.MethodName != methodName)
            {
                existingError.MethodName = methodName;
                isChanged = true;
            }

            if (existingError.Headline != headline)
            {
                existingError.Headline = headline;
                isChanged = true;
            }

            if (existingError.Message != message)
            {
                existingError.Message = message;
                isChanged = true;
            }

            if (existingError.StackTrace != stackTrace)
            {
                existingError.StackTrace = stackTrace;
                isChanged = true;
            }

            if (isChanged)
            {
                logger.LogInformation("Due to changes on the existing error the error {headline} for car {vin} is auto undismissed.", headline, vin);
                existingError.DismissedAt = default;
            }

        }


        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task HandleErrorResolved(string issueKey, string? vin)
    {
        logger.LogTrace("{method}({issueKey}, {vin})", nameof(HandleErrorResolved), issueKey, vin);
        var openError = context.LoggedErrors
            .FirstOrDefault(e => e.IssueKey == issueKey
                                 && e.Vin == vin
                                 && e.EndTimeStamp == null);
        if (openError == null)
        {
            return;
        }
        openError.EndTimeStamp = dateTimeProvider.UtcNow();
        await context.SaveChangesAsync();
    }

    public async Task SendTelegramMessages()
    {
        var openErrors = await context.LoggedErrors
            .Where(e => e.EndTimeStamp == null && e.TelegramNotificationSent == false)
            .ToListAsync();
        foreach (var error in openErrors)
        {
            if ((!possibleIssues.GetIssueByKey(error.IssueKey).IsTelegramEnabled) || ((error.FurtherOccurrences.Count + 1) < possibleIssues.GetIssueByKey(error.IssueKey).ShowErrorAfterOccurrences))
            {
                continue;
            }

            var errorText = $"[{error.StartTimeStamp.ToLocalTime()}] Error with key {error.IssueKey} ";
            if (!string.IsNullOrEmpty(error.Vin))
            {
                errorText += $"for car {error.Vin} ";
            }
            errorText += $"in {error.Source}.{error.MethodName}: {error.Message}";
            if (configurationWrapper.SendStackTraceToTelegram() && !string.IsNullOrEmpty(error.StackTrace))
            {
                errorText += $"\r\nStack Trace: {error.StackTrace}";
            }
            var statusCode = await telegramService.SendMessage(errorText);
            if (((int)statusCode >= 200) && ((int)statusCode <= 299))
            {
                error.TelegramNotificationSent = true;
            }
        }
        var closedErrors = await context.LoggedErrors
            .Where(e => e.EndTimeStamp != null && e.TelegramResolvedMessageSent == false && e.TelegramNotificationSent)
            .ToListAsync();
        foreach (var error in closedErrors)
        {
            if (!possibleIssues.GetIssueByKey(error.IssueKey).IsTelegramEnabled)
            {
                continue;
            }

            var resolvedText = $"Error with key {error.IssueKey} ";
            if (string.IsNullOrEmpty(error.Vin))
            {
                resolvedText += $"and VIN {error.Vin} ";
            }
            resolvedText += $"from {error.StartTimeStamp.ToLocalTime()} has been resolved after {error.FurtherOccurrences.Count + 1} occurrences at {error.EndTimeStamp?.ToLocalTime()}";
            var statusCode = await telegramService.SendMessage(resolvedText);
            if (((int)statusCode >= 200) && ((int)statusCode <= 299))
            {
                error.TelegramResolvedMessageSent = true;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task RemoveInvalidLoggedErrorsAsync()
    {
        logger.LogTrace("{method}()", nameof(RemoveInvalidLoggedErrorsAsync));
        // Get all issue keys using reflection
        var validIssueKeys = GetIssueKeysUsingReflection();

        // Split validIssueKeys into exact matches and prefixes
        var exactMatchKeys = validIssueKeys.Where(k => !k.EndsWith("_")).ToList();
        var prefixKeys = validIssueKeys.Where(k => k.EndsWith("_"))
            .Select(k => k.TrimEnd('_'))
            .ToList();

        // Build the query to select invalid LoggedErrors
        var errorsToRemove = context.LoggedErrors.Where(e =>
            !exactMatchKeys.Contains(e.IssueKey) &&
            !prefixKeys.Any(prefix => e.IssueKey.StartsWith(prefix)));

        var numberOfErrorsToDelete = await errorsToRemove.CountAsync();
        logger.LogInformation("Delete {numberOfErrorsToDelete} logged errors as they have unknown keys", numberOfErrorsToDelete);

        // Remove the invalid LoggedErrors
        context.LoggedErrors.RemoveRange(errorsToRemove);

        // Save changes to the database
        await context.SaveChangesAsync();
    }

    private async Task<Fin<List<LoggedError>>> GetUnfilterdLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        try
        {
            var loggedErrors = await context.LoggedErrors
                .Where(e => e.EndTimeStamp == default)
                .ToListAsync();
            return Fin<List<LoggedError>>.Succ(loggedErrors);
        }
        catch (Exception ex)
        {
            return Fin<List<LoggedError>>.Fail(Error.New(ex));
        }

    }

    private List<string> GetIssueKeysUsingReflection()
    {
        // Get all public properties of the interface IIssueKeys
        var issueKeyProperties = typeof(IIssueKeys).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Get the value of each property (the issue key string) from the _issueKeys instance
        var keys = issueKeyProperties.Select(prop => prop.GetValue(issueKeys)?.ToString())
            .Where(key => key != null)
            .ToList();

        return keys!;
    }

    private async Task DetectTokenStateIssues(List<LoggedError> activeErrors)
    {
        logger.LogTrace("{method}()", nameof(DetectTokenStateIssues));
        var backendTokenState = await tokenHelper.GetBackendTokenState(true);
        var fleetApiTokenState = await tokenHelper.GetFleetApiTokenState(true);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoBackendApiToken, "No Backen API token",
            "You are currently not connected to the backend. Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            backendTokenState == TokenState.NotAvailable).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.BackendTokenUnauthorized, "Backend Token Unauthorized",
            "You recently changed your Solar4Car password or did not use TSC for at least 30 days. Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            backendTokenState == TokenState.Unauthorized).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenUnauthorized, "Fleet API token is unauthorized",
            "You recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Unauthorized).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.NoFleetApiToken, "No Fleet API Token available.",
            "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            fleetApiTokenState == TokenState.NotAvailable).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenExpired, "Fleet API token is expired",
            "Either you recently changed your Tesla password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            fleetApiTokenState == TokenState.Expired).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenMissingScopes, "Your Tesla token has missing scopes.",
            "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Note: You need to allow all selectable scopes as otherwise TSC won't work properly.",
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

    private async Task<int> GetActiveIssueCountBySeverity(IssueSeverity severity)
    {
        var activeIssueKeys = await context.LoggedErrors
            .Where(e => e.EndTimeStamp == null)
            .Select(e => new { e.IssueKey, Occurrences = e.FurtherOccurrences.Count + 1, })
            .ToListAsync().ConfigureAwait(false);
        activeIssueKeys.RemoveAll(i => i.Occurrences < possibleIssues.GetIssueByKey(i.IssueKey).ShowErrorAfterOccurrences);

        return activeIssueKeys.Count(activeIssueKey => possibleIssues.GetIssueByKey(activeIssueKey.IssueKey).IssueSeverity == severity);
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
                MethodName = nameof(DetectErrors),
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
