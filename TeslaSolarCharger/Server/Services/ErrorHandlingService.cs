using AutoMapper.QueryableExtensions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Mvc.Formatters.Xml;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ErrorHandlingService(ILogger<ErrorHandlingService> logger,
    IBackendApiService backendApiService,
    IIssueKeys issueKeys,
    ITelegramService telegramService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IMapperConfigurationFactory mapperConfigurationFactory,
    ISettings settings) : IErrorHandlingService
{
    public async Task<Fin<List<DtoLoggedError>>> GetActiveLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        var mappingConfiguration = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<LoggedError, DtoLoggedError>()
                .ForMember(d => d.Occurrences, opt => opt.MapFrom(s => new List<DateTime>(){s.StartTimeStamp}.Concat(s.FurtherOccurrences)))
                ;
        });
        var mapper = mappingConfiguration.CreateMapper();
        try
        {
            var loggedErrors = await context.LoggedErrors
                .Where(e => e.EndTimeStamp == default)
                .ToListAsync();
            var errors = loggedErrors
                .Where(e => e.DismissedAt == default || (e.FurtherOccurrences.Any() && e.DismissedAt < e.FurtherOccurrences.Max()))
                .Select(e => mapper.Map<DtoLoggedError>(e))
                .ToList();
            return Fin<List<DtoLoggedError>>.Succ(errors);
        }
        catch (Exception ex)
        {
            return Fin<List<DtoLoggedError>>.Fail(Error.New(ex));
        }

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
            "Update TSC to the latest version.", solarValuesTooOld).ConfigureAwait(false);

    }

    public async Task<DtoValue<int>> ErrorCount()
    {
        var count = await context.LoggedErrors.Where(e => e.EndTimeStamp == null).CountAsync().ConfigureAwait(false);
        return new(count);
    }

    public async Task<DtoValue<int>> WarningCount()
    {
        //ToDo: need to differencitate between warnings and errors
        return await ErrorCount().ConfigureAwait(false);
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
        await backendApiService.PostErrorInformation(source, methodName, message, issueKey, vin, stackTrace);
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
            if (!TelegramEnabledIssueKeys.Any(i => error.IssueKey.StartsWith(i)))
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
            if (!TelegramEnabledIssueKeys.Any(i => error.IssueKey.StartsWith(i)))
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

    private async Task AddOrRemoveErrors(List<LoggedError> activeErrors, string issueKey, string headline, string message, bool shouldBeActive)
    {
        var filteredErrors = activeErrors.Where(e => e.IssueKey == issueKey).ToList();
        if (shouldBeActive && filteredErrors.Count < 1)
        {
            var loggedError = new LoggedError()
            {
                StartTimeStamp = dateTimeProvider.UtcNow(),
                IssueKey = issueKeys.RestartNeeded,
                Source = nameof(ErrorHandlingService),
                MethodName = nameof(DetectErrors),
                Headline = headline,
                Message = message,
            };
            context.LoggedErrors.Add(loggedError);
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

    private System.Collections.Generic.HashSet<string> TelegramEnabledIssueKeys =>
    [
        issueKeys.FleetApiTokenNotRequested,
        issueKeys.FleetApiTokenUnauthorized,
        issueKeys.FleetApiTokenMissingScopes,
        issueKeys.FleetApiTokenRequestExpired,
        issueKeys.FleetApiTokenNotReceived,
        issueKeys.FleetApiTokenExpired,
        issueKeys.FleetApiTokenNoApiRequestsAllowed,
        issueKeys.GetVehicle,
        issueKeys.GetVehicleData,
        issueKeys.CarStateUnknown,
        issueKeys.UnhandledCarStateRefresh,
        issueKeys.FleetApiNonSuccessStatusCode,
        issueKeys.FleetApiNonSuccessResult,
        issueKeys.UnsignedCommand,
        issueKeys.FleetApiTokenRefreshNonSuccessStatusCode,
        issueKeys.SolarValuesNotAvailable,
    ];
}
