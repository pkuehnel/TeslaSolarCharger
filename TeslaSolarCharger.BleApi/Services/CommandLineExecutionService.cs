using PkSoftwareService.Custom.Backend.Ble;
using System.Diagnostics;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

/// <summary>
/// Generic short lived process executor, used for openssl, tesla-control -h and pairing. BLE commands do not run
/// through here anymore; they go to the long living worker via <see cref="BleWorkerService"/>.
/// </summary>
public class CommandLineExecutionService(ILogger<CommandLineExecutionService> logger, IConfiguration configuration) : ICommandLineExecutionService
{
    public async Task<DtoBleCommandResult> ExecuteCommand(string filename, string parameters)
    {
        logger.LogTrace("{method}({fileName}, {parameters})", nameof(ExecuteCommand), filename, parameters);
        var processStartInfo = new ProcessStartInfo()
        {
            FileName = filename,
            Arguments = parameters,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = new Process();
        process.StartInfo = processStartInfo;
        var commandResult = new DtoBleCommandResult();
        try
        {
            process.Start();
            var executionTimeoutSeconds = configuration.GetValue<int>("ProcessExecutionTimeoutSeconds");
            logger.LogTrace("Using execution timout of {seconds} seconds", executionTimeoutSeconds);
            var hasExited = process.WaitForExit(TimeSpan.FromSeconds(executionTimeoutSeconds));
            logger.LogTrace("Process exited: {hasExited}", hasExited);
            if (!hasExited)
            {
                //A leaked process would keep the Bluetooth adapter bound and block every follow up request.
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception killException)
                {
                    logger.LogError(killException, "Could not kill process {fileName} after timeout", filename);
                }
                commandResult.Success = false;
                commandResult.ResultMessage = $"Process did not exit within {executionTimeoutSeconds} seconds and was killed.";
                commandResult.ErrorType = ErrorType.Exceptional;
                return commandResult;
            }

            var readOutputTimeout = TimeSpan.FromSeconds(5);
            var result = await process.StandardOutput.ReadToEndAsync(new CancellationTokenSource(readOutputTimeout).Token);
            logger.LogTrace("Stdout Result: {result}", result);
            logger.LogTrace("Process completed with exit code {exitCode}", process.ExitCode);
            commandResult.ResultMessage = result;
            if (process.ExitCode == 0)
            {
                commandResult.Success = true;
            }
            else
            {
                commandResult.Success = false;
                try
                {
                    var errorMessage = await process.StandardError.ReadToEndAsync(new CancellationTokenSource(readOutputTimeout).Token);
                    logger.LogTrace("Stderr Result: {result}", errorMessage);
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        commandResult.ResultMessage = errorMessage;
                    }
                    commandResult.ErrorType = ErrorType.TeslaControl;
                }
                catch (Exception ex)
                {
                    commandResult.ResultMessage += $"Neither standard output nor Standard error has a value. Error reading standard error: {ex.Message}";
                    commandResult.ErrorType = ErrorType.Exceptional;
                }
            }
        }
        catch (Exception ex)
        {
            commandResult.Success = false;
            commandResult.ResultMessage = $"Unhandled Error: {ex.Message}";
            commandResult.ErrorType = ErrorType.Exceptional;
            logger.LogError(ex, "Unhandled Error");
        }
        return commandResult;
    }
}
