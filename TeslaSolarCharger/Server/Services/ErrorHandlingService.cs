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
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using TeslaSolarCharger.SharedModel.Enums;
using Error = LanguageExt.Common.Error;

namespace TeslaSolarCharger.Server.Services;

public class ErrorHandlingService(ILogger<ErrorHandlingService> logger,
    IIssueKeys issueKeys,
    ITelegramService telegramService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IMapperConfigurationFactory mapperConfigurationFactory,
    ISettings settings,
    ITeslaFleetApiTokenHelper teslaFleetApiTokenHelper,
    IPossibleIssues possibleIssues) : IErrorHandlingService
{
    public async Task<Fin<List<DtoLoggedError>>> GetActiveLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        var unfilterdErrorsFin = await GetUnfilterdLoggedErrors();
        return unfilterdErrorsFin.Match(
            Succ: unfilteredErrors =>
            {
                var mappingConfiguration = mapperConfigurationFactory.Create(cfg =>
                {
                    cfg.CreateMap<LoggedError, DtoLoggedError>()
                        .ForMember(d => d.Occurrences, opt => opt.MapFrom(s => new List<DateTime>() { s.StartTimeStamp }.Concat(s.FurtherOccurrences)))
                        .ForMember(d => d.Severity, opt => opt.MapFrom(s => possibleIssues.GetIssueByKey(s.IssueKey).IssueSeverity))
                        ;
                });
                var mapper = mappingConfiguration.CreateMapper();
                var errors = unfilteredErrors
                    .Where(e => e.DismissedAt == default || (e.FurtherOccurrences.Any() && (e.DismissedAt < e.FurtherOccurrences.Max())))
                    .Select(e => mapper.Map<DtoLoggedError>(e))
                    .ToList();

                var removedErrorCount = errors.RemoveAll(e => e.Occurrences.Count < possibleIssues.GetIssueByKey(e.IssueKey).ShowErrorAfterOccurrences);
                logger.LogDebug("{removedErrorsCount} errors removed as did not reach minimum error count", removedErrorCount);
                return Fin<List<DtoLoggedError>>.Succ(errors);
            },
            Fail: error =>
            {
                return Fin<List<DtoLoggedError>>.Fail(error);
            });

    }
    
    public async Task<Fin<List<DtoHiddenError>>> GetHiddenErrors()
    {
        logger.LogTrace("{method}()", nameof(GetHiddenErrors));
        var unfilterdErrorsFin = await GetUnfilterdLoggedErrors();
        return unfilterdErrorsFin.Match(
            Succ: unfilteredErrors =>
            {
                var mappingConfiguration = mapperConfigurationFactory.Create(cfg =>
                {
                    cfg.CreateMap<LoggedError, DtoHiddenError>()
                        .ForMember(d => d.Occurrences, opt => opt.MapFrom(s => new List<DateTime>() { s.StartTimeStamp }.Concat(s.FurtherOccurrences)))
                        .ForMember(d => d.Severity, opt => opt.MapFrom(s => possibleIssues.GetIssueByKey(s.IssueKey).IssueSeverity))
                        ;
                });
                var mapper2 = mappingConfiguration.CreateMapper();
                var hiddenErrors = new List<DtoHiddenError>();
                foreach (var loggedError in unfilteredErrors)
                {
                    var occurrences = new List<DateTime>() { loggedError.StartTimeStamp }.Concat(loggedError.FurtherOccurrences).ToList();
                    if (occurrences.Count
                        < possibleIssues.GetIssueByKey(loggedError.IssueKey).ShowErrorAfterOccurrences)
                    {
                        var hiddenError = mapper2.Map<DtoHiddenError>(loggedError);
                        hiddenError.HideReason = LoggedErrorHideReason.NotEnoughOccurrences;
                        hiddenErrors.Add(hiddenError);
                    }
                    else if(loggedError.DismissedAt > occurrences.Max())
                    {
                        var hiddenError = mapper2.Map<DtoHiddenError>(loggedError);
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
        await DetectTokenStateIssues(activeErrors);
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
            return Fin<List<LoggedError>>.Succ(new List<LoggedError>());
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
        var tokenState = await teslaFleetApiTokenHelper.GetFleetApiTokenState();
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenNotRequested, "Fleet API token not requested",
            "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            tokenState == FleetApiTokenState.NotRequested).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenUnauthorized, "Fleet API token is unauthorized",
            "You recently changed your password or did not enable mobile access in your car. Enable mobile access in your car and open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token. Important: You need to allow access to all selectable scopes.",
            tokenState == FleetApiTokenState.TokenUnauthorized).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenMissingScopes, "Your Tesla token has missing scopes.",
            "Remove Tesla Solar Charger from your <a href=\"https://accounts.tesla.com/account-settings/security?tab=tpty-apps\" target=\"_blank\">third party apps</a> as you won't get asked again for the scopes. After that request a new token in the <a href=\"/BaseConfiguration\">Base Configuration</a> and select all available scopes.",
            tokenState == FleetApiTokenState.MissingScopes).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenRequestExpired, "Tesla Token could not be received",
            "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            tokenState == FleetApiTokenState.TokenRequestExpired).ConfigureAwait(false);
        await AddOrRemoveErrors(activeErrors, issueKeys.FleetApiTokenExpired, "Tesla Token expired",
            "Open the <a href=\"/BaseConfiguration\">Base Configuration</a> and request a new token.",
            tokenState == FleetApiTokenState.Expired).ConfigureAwait(false);

        //Remove all token related issue keys on token error because very likely it is because of the underlaying token issue.
        if (tokenState != FleetApiTokenState.UpToDate && tokenState != FleetApiTokenState.NotNeeded)
        {
            foreach (var activeError in activeErrors.Where(activeError => activeError.IssueKey.StartsWith(issueKeys.GetVehicleData)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.CarStateUnknown)
                                                                          || activeError.IssueKey.StartsWith(issueKeys.UnhandledCarStateRefresh)
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
            for (var i = 0; i < activeErrors.Count; i++)
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
