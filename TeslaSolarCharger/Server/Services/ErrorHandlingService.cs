using Microsoft.EntityFrameworkCore;
using System.Net;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class ErrorHandlingService(ILogger<ErrorHandlingService> logger,
    IBackendApiService backendApiService,
    IIssueKeys issueKeys,
    ITelegramService telegramService,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider) : IErrorHandlingService
{
    public async Task HandleError(string source, string methodName, string message, string issueKey, string? vin,
        string? stackTrace)
    {
        logger.LogTrace("{method}({source}, {methodName}, {message}, {issueKey}, {vin}, {stackTrace})",
            nameof(HandleError), source, methodName, message, issueKey, vin, stackTrace);
        await backendApiService.PostErrorInformation(source, methodName, message, issueKey, vin, stackTrace);
        var isErrorOpen = await context.LoggedErrors
            .Where(e => e.IssueKey == issueKey
                        && e.Vin == vin
                        && e.EndTimeStamp == null)
            .AnyAsync();
        if (isErrorOpen)
        {
            return;
        }

        var telegramMessageSent = false;
        if (TelegramEnabledIssueKeys.Any(i => issueKey.StartsWith(i)))
        {
            var errorText = $"Error with key {issueKey} ";
            if (!string.IsNullOrEmpty(vin))
            {
                errorText += $"for car {vin} ";
            }
            errorText += $"in {source}.{methodName}: {message}";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                errorText += $"\r\nStack Trace: {stackTrace}";
            }
            var statusCode = await telegramService.SendMessage(errorText);
            if (((int)statusCode >= 200) && ((int)statusCode <= 299))
            {
                telegramMessageSent = true;
            }
        }

        var error = new LoggedError()
        {
            StartTimeStamp = dateTimeProvider.UtcNow(),
            IssueKey = issueKey,
            Vin = vin,
            TelegramNotificationSent = telegramMessageSent,
        };
        context.LoggedErrors.Add(error);
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

    private HashSet<string> TelegramEnabledIssueKeys =>
    [
        issueKeys.GridPowerNotAvailable,
        issueKeys.InverterPowerNotAvailable,
        issueKeys.HomeBatterySocNotAvailable,
        issueKeys.HomeBatterySocNotPlausible,
        issueKeys.HomeBatteryPowerNotAvailable,
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
    ];
}
