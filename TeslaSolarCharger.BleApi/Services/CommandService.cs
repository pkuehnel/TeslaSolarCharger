using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class CommandService(ILogger<CommandService> logger,
    ICommandLineExecutionService commandLineExecutionService,
    IConfiguration configuration,
    ISettings settings,
    IStartupService startupService,
    IBleWorkerService bleWorkerService) : ICommandService
{
    public async Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, string? domain,
        List<string> parameters, string? adapter, int? keepWarmSeconds, bool useDebug)
    {
        logger.LogTrace("{method}({vin}, {command}, {domain}, {@parameters}, {adapter}, {keepWarmSeconds}, {useDebug})",
            nameof(ExecuteCommand), vin, command, domain, parameters, adapter, keepWarmSeconds, useDebug);
        if (await CheckRequestsAllowed().ConfigureAwait(false) is { } notAllowedResult)
        {
            return notAllowedResult;
        }
        if (string.IsNullOrEmpty(configuration.GetValue<string>("PrivateKeyPath")))
        {
            logger.LogError("PrivateKeyPath is not set in the configuration");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "PrivateKeyPath is not set in the configuration",
                ErrorType = ErrorType.BleApiConfiguration,
                Outcome = BleCommandOutcome.InvalidRequest,
            };
        }
        //The domain parameter is accepted for interface stability but not forwarded: the worker knows per command
        //whether VCSEC or an infotainment session is needed.
        return await bleWorkerService.ExecuteCommand(adapter, vin, command, parameters, keepWarmSeconds, useDebug).ConfigureAwait(false);
    }

    public async Task<DtoBleBeaconScanResult> BeaconScan(List<string> vins, string? adapter, int? keepWarmSeconds,
        bool useDebug, int? windowMs = null)
    {
        logger.LogTrace("{method}({@vins}, {adapter}, {keepWarmSeconds}, {useDebug}, {windowMs})", nameof(BeaconScan), vins, adapter, keepWarmSeconds, useDebug, windowMs);
        if (await CheckRequestsAllowed().ConfigureAwait(false) is { } notAllowedResult)
        {
            return new DtoBleBeaconScanResult
            {
                Success = false,
                Outcome = notAllowedResult.Outcome,
                ResultMessage = notAllowedResult.ResultMessage,
            };
        }
        return await bleWorkerService.BeaconScan(adapter, vins, keepWarmSeconds, useDebug, windowMs).ConfigureAwait(false);
    }

    public async Task<DtoBleCommandResult> ListCommands()
    {
        logger.LogTrace("{method}", nameof(ListCommands));
        //tesla-control -h never touches the adapter, so it needs neither the worker nor its gate.
        var result = await commandLineExecutionService.ExecuteCommand("/app/go/tesla-control", "-h").ConfigureAwait(false);
        return result;
    }

    private async Task<DtoBleCommandResult?> CheckRequestsAllowed()
    {
        if (settings.BleRequestAllowed)
        {
            return null;
        }
        await startupService.UpdateRequestsAllowed().ConfigureAwait(false);
        if (settings.BleRequestAllowed)
        {
            return null;
        }
        logger.LogError("BleRequestNotAllowed update BleService to latest version.");
        return new DtoBleCommandResult()
        {
            Success = false,
            ResultMessage = "BleRequestNotAllowed. Check internet connection, restart service or update BleService to latest version.",
            ErrorType = ErrorType.BleApiConfiguration,
            Outcome = BleCommandOutcome.InvalidRequest,
        };
    }
}
