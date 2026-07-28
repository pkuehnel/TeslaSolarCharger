using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Enums;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class CommandService(ILogger<CommandService> logger,
    ICommandLineExecutionService commandLineExecutionService,
    IConfiguration configuration,
    ISettings settings,
    IStartupService startupService,
    IBleAdapterGate bleAdapterGate,
    TimeProvider timeProvider) : ICommandService
{
    private readonly string _guid = Guid.NewGuid().ToString();

    /// <summary>
    /// While a test session holds a connection the adapter is blocked for everything else, so waiting for it would
    /// only run into the semaphore timeout.
    /// </summary>
    private DtoBleCommandResult? GetHeldSessionResult()
    {
        var heldSessionVin = bleAdapterGate.HeldSessionVin;
        if (heldSessionVin == default)
        {
            return default;
        }
        logger.LogWarning("A BLE test session is currently held for car {vin}, no other BLE access is possible", heldSessionVin);
        return new DtoBleCommandResult()
        {
            Success = false,
            ResultMessage = $"A BLE test session is currently held for car {heldSessionVin}. Stop the session before sending other BLE requests.",
            ErrorType = ErrorType.BleApiConfiguration,
        };
    }

    public async Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, string? domain,
        List<string> parameters, bool useDebug)
    {
        logger.LogTrace("{method}({vin}, {command}, {domain}, {@parameters}, {useDebug})", nameof(ExecuteCommand), vin, command, domain, parameters, useDebug);
        var heldSessionResult = GetHeldSessionResult();
        if (heldSessionResult != default)
        {
            return heldSessionResult;
        }
        var fleetApiRequestsAllowed = settings.BleRequestAllowed;
        if (!fleetApiRequestsAllowed)
        {
            if (settings.LastBleAllowedRequest < timeProvider.GetUtcNow().AddMinutes(1))
            {
                await startupService.UpdateRequestsAllowed();
            }
            
            logger.LogError("BleRequestNotAllowed update BleService to latest version.");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "BleRequestNotAllowed. Check internet connection, restart service or update BleService to latest version.",
                ErrorType = ErrorType.BleApiConfiguration,
            };
        }
        var file = "/app/go/tesla-control";
        var privateKeyLocation = configuration.GetValue<string>("PrivateKeyPath");
        if (string.IsNullOrEmpty(privateKeyLocation))
        {
            logger.LogError("PrivateKeyPath is not set in the configuration");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "PrivateKeyPath is not set in the configuration",
                ErrorType = ErrorType.BleApiConfiguration,
            };
        }

        var domainPrefix = string.IsNullOrEmpty(domain) ? string.Empty : $"-domain {domain} ";
        //Debug is requested per command by TSC (enabled per car), so a single car can be debugged without
        //restarting the container or making every other car verbose.
        var debugParameterString = useDebug ? "-debug " : string.Empty;
        var commandTimeoutSeconds = configuration.GetValue<int>("CommandTimeoutSeconds");
        var connectTimeoutSeconds = configuration.GetValue<int>("ConnectTimeoutSeconds");
        var teslaCacheFilePath = configuration.GetValue<string>("TeslaCacheFilePath");
        var bluetoothAdapter = configuration.GetValue<string>("BluetoothAdapter");
        var bluetoothAdapterParameterString = string.IsNullOrEmpty(bluetoothAdapter) ? string.Empty : $"-bt-adapter {bluetoothAdapter} ";
        var parameterString =
            $"{domainPrefix}-ble {debugParameterString}{bluetoothAdapterParameterString}-session-cache {teslaCacheFilePath} -vin {vin} -key-file {privateKeyLocation} -command-timeout {commandTimeoutSeconds}s -connect-timeout {connectTimeoutSeconds}s {command} {string.Join(" ", parameters)}";
        logger.LogTrace("Waiting for semaphoreSlim to allow command execution in {guid}", _guid);
        var semaphoreSlimWaitTimeoutSeconds = configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        //Return before the try/finally on a wait timeout: the semaphore was never acquired, so the
        //delayed release in the finally must not run (it would over-release and throw).
        if (!await bleAdapterGate.WaitAsync(TimeSpan.FromSeconds(semaphoreSlimWaitTimeoutSeconds)))
        {
            logger.LogError("SemaphoreSlim did not allow command execution in time");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "SemaphoreSlim did not allow command execution in time",
                ErrorType = ErrorType.TeslaControl,
            };
        }
        try
        {
            logger.LogTrace("SemaphoreSlim allowed command execution");
            var result = await commandLineExecutionService.ExecuteCommand(file, parameterString);
            result.ResultMessage = result.ResultMessage?.Trim();
            result.CarErrorMessage = result.CarErrorMessage?.Trim();
            if (!string.IsNullOrWhiteSpace(result.DebugOutput))
            {
                //Logged so the debug output is also part of the downloadable container logs, not only of the response.
                logger.LogInformation("Debug output of command {command} for car {vin}:\n{debugOutput}", command, vin, result.DebugOutput);
            }
            if (!result.Success)
            {
                if (!string.IsNullOrEmpty(teslaCacheFilePath))
                {
                    try
                    {
                        if (File.Exists(teslaCacheFilePath))
                        {
                            File.Delete(teslaCacheFilePath);
                            logger.LogInformation("Deleted cache file at {cacheFilePath} due to command failure", teslaCacheFilePath);
                        }
                        else
                        {
                            logger.LogWarning("Could not find cache file {cacheFilePath} to delete although an error occurred.", teslaCacheFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error deleting cache file at {cacheFilePath}", teslaCacheFilePath);
                    }

                }
            }
            return result;
        }
        finally
        {
            ReleaseSemaphoreAfterCooldown();
        }
    }

    public async Task<DtoBleCommandResult> BeaconScan(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(BeaconScan), vin);
        var heldSessionResult = GetHeldSessionResult();
        if (heldSessionResult != default)
        {
            return heldSessionResult;
        }
        var bleRequestsAllowed = settings.BleRequestAllowed;
        if (!bleRequestsAllowed)
        {
            if (settings.LastBleAllowedRequest < timeProvider.GetUtcNow().AddMinutes(1))
            {
                await startupService.UpdateRequestsAllowed();
            }

            logger.LogError("BleRequestNotAllowed update BleService to latest version.");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "BleRequestNotAllowed. Check internet connection, restart service or update BleService to latest version.",
                ErrorType = ErrorType.BleApiConfiguration,
            };
        }
        var file = "/app/go/tesla-beacon-scan";
        var beaconScanTimeoutSeconds = configuration.GetValue<int>("BeaconScanTimeoutSeconds");
        var bluetoothAdapter = configuration.GetValue<string>("BluetoothAdapter");
        var bluetoothAdapterParameterString = string.IsNullOrEmpty(bluetoothAdapter) ? string.Empty : $"-bt-adapter {bluetoothAdapter} ";
        var parameterString = $"{bluetoothAdapterParameterString}-vin {vin} -timeout {beaconScanTimeoutSeconds}s";
        logger.LogTrace("Waiting for semaphoreSlim to allow beacon scan in {guid}", _guid);
        var semaphoreSlimWaitTimeoutSeconds = configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        //The beacon scanner opens the same HCI adapter as tesla-control and go-ble resets the adapter on
        //init, so a scan must never run concurrently with another BLE process: share the semaphore.
        if (!await bleAdapterGate.WaitAsync(TimeSpan.FromSeconds(semaphoreSlimWaitTimeoutSeconds)))
        {
            logger.LogError("SemaphoreSlim did not allow beacon scan in time");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "SemaphoreSlim did not allow command execution in time",
                ErrorType = ErrorType.TeslaControl,
            };
        }
        try
        {
            logger.LogTrace("SemaphoreSlim allowed beacon scan");
            var result = await commandLineExecutionService.ExecuteCommand(file, parameterString);
            result.ResultMessage = result.ResultMessage?.Trim();
            //A failed scan says nothing about the tesla-control session, so the session cache is kept.
            return result;
        }
        finally
        {
            ReleaseSemaphoreAfterCooldown();
        }
    }

    /// <summary>
    /// Releases the semaphore after the configured cooldown so the BLE adapter can settle between two
    /// processes using it. Must only be called when the semaphore was actually acquired.
    /// </summary>
    private void ReleaseSemaphoreAfterCooldown()
    {
        _ = Task.Run(async () =>
        {
            var millisecondsToWait = configuration.GetValue<int>("MinimumWaitTimeBetweenCommandsMilliseconds");
            logger.LogTrace("Waiting {millisecondsToWait} ms before allowing next command execution", millisecondsToWait);
            await Task.Delay(millisecondsToWait);
            bleAdapterGate.Release();
        });
    }

    public async Task<DtoBleCommandResult> ListCommands()
    {
        logger.LogTrace("{method}", nameof(ListCommands));
        var file = "/app/go/tesla-control";
        var result = await commandLineExecutionService.ExecuteCommand(file, "-h");
        return result;
    }
}