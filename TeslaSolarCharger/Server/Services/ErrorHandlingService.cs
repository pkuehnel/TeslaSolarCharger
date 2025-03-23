using Microsoft.EntityFrameworkCore;
using System.Reflection;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.LoggedError;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ErrorHandlingService(ILogger<ErrorHandlingService> logger,
    IIssueKeys issueKeys,
    ITelegramService telegramService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IPossibleIssues possibleIssues) : IErrorHandlingService
{
    public async Task<List<DtoLoggedError>> GetActiveLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        var unfilteredErrors = await GetUnfilterdLoggedErrors();
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
                HideOccurrenceCount = possibleIssues.GetIssueByKey(e.IssueKey).HideOccurrenceCount,
            })
            .ToList();

        var removedErrorCount = errors.RemoveAll(e => e.OccurrenceCount < possibleIssues.GetIssueByKey(e.IssueKey).ShowErrorAfterOccurrences);
        logger.LogDebug("{removedErrorsCount} errors removed as did not reach minimum error count", removedErrorCount);
        return errors;
    }

    public async Task<List<DtoHiddenError>> GetHiddenErrors()
    {
        logger.LogTrace("{method}()", nameof(GetHiddenErrors));
        var unfilteredErrors = await GetUnfilterdLoggedErrors();
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
            else if (loggedError.DismissedAt > occurrences.Max())
            {
                hiddenError.HideReason = LoggedErrorHideReason.Dismissed;
                hiddenErrors.Add(hiddenError);
            }
        }

        return hiddenErrors;
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
    
    public async Task<int> DismissError(int errorIdValue)
    {
        logger.LogTrace("{method}({errorId})", nameof(DismissError), errorIdValue);
        var error = await context.LoggedErrors.FindAsync(errorIdValue);
        if (error == default)
        {
            return errorIdValue;
        }

        error.DismissedAt = dateTimeProvider.UtcNow();
        await context.SaveChangesAsync();
        return errorIdValue;
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

    private async Task<List<LoggedError>> GetUnfilterdLoggedErrors()
    {
        logger.LogTrace("{method}()", nameof(GetActiveLoggedErrors));
        var loggedErrors = await context.LoggedErrors
            .Where(e => e.EndTimeStamp == default)
            .ToListAsync();
        return loggedErrors;
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

    private async Task<int> GetActiveIssueCountBySeverity(IssueSeverity severity)
    {
        var activeIssueKeys = await context.LoggedErrors
            .Where(e => e.EndTimeStamp == null)
            .Select(e => new { e.IssueKey, Occurrences = e.FurtherOccurrences.Count + 1, })
            .ToListAsync().ConfigureAwait(false);
        activeIssueKeys.RemoveAll(i => i.Occurrences < possibleIssues.GetIssueByKey(i.IssueKey).ShowErrorAfterOccurrences);

        return activeIssueKeys.Count(activeIssueKey => possibleIssues.GetIssueByKey(activeIssueKey.IssueKey).IssueSeverity == severity);
    }


}
