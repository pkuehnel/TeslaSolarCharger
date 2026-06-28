using System.Diagnostics;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Enums;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class CommandLineExecutionService(ILogger<CommandLineExecutionService> logger, IConfiguration configuration) : ICommandLineExecutionService
{
    public async Task<DtoBleCommandResult> ExecuteCommand(string filename, string parameters)
    {
        logger.LogTrace("{method}({fileName}, {parameters})", nameof(ExecuteCommand), filename, parameters);
        var processStartInfo = new ProcessStartInfo()
        {
            FileName = filename,
            Arguments = $"{parameters}",
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
            // Wait for the process to exit
            var executionTimeoutSeconds = configuration.GetValue<int>("ProcessExecutionTimeoutSeconds");
            logger.LogTrace("Using execution timout of {seconds} seconds", executionTimeoutSeconds);
            var hasExited = process.WaitForExit(TimeSpan.FromSeconds(executionTimeoutSeconds));
            logger.LogTrace("Process exited: {hasExited}", hasExited);
            if (!hasExited)
            {
                throw new TimeoutException($"Process did not exit within {executionTimeoutSeconds} seconds.");
            }

            var readOutputTimeout = TimeSpan.FromSeconds(5);
            var result = await process.StandardOutput.ReadToEndAsync(new CancellationTokenSource(readOutputTimeout).Token);
            logger.LogTrace("Stdout Result: {result}", result);
            logger.LogTrace("Process completed with exit code {exitCode}", process.ExitCode);
            // Check if the process completed successfully
            commandResult.ResultMessage = result;
            if (process.ExitCode == 0)
            {
                
                commandResult.Success = true;
            }
            else
            {
                commandResult.Success = false;
                if (string.IsNullOrEmpty(commandResult.ResultMessage))
                {
                    commandResult.ResultMessage = string.Empty;
                }
                try
                {
                    var errorMessage = await process.StandardError.ReadToEndAsync(new CancellationTokenSource(readOutputTimeout).Token);
                    logger.LogTrace("Stderr Result: {result}", errorMessage);
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        commandResult.ResultMessage = errorMessage;
                    }
                    var splittedString = errorMessage.Split("car could not execute command: ");
                    if (splittedString.Length == 2)
                    {
                        commandResult.ErrorType = ErrorType.CarExecution;
                        commandResult.CarErrorMessage = splittedString[1];
                    }
                    else
                    {
                        commandResult.ErrorType = ErrorType.TeslaControl;
                    }
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