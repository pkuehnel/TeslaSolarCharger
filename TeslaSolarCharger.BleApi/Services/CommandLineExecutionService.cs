using System.Diagnostics;
using System.Text.RegularExpressions;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Enums;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public partial class CommandLineExecutionService(ILogger<CommandLineExecutionService> logger, IConfiguration configuration) : ICommandLineExecutionService
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
            //Both streams must be drained while the process is running: with debug logging enabled tesla-control
            //writes more to stderr than the pipe buffer holds, and a process blocked on a full pipe never exits.
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            var executionTimeoutSeconds = configuration.GetValue<int>("ProcessExecutionTimeoutSeconds");
            logger.LogTrace("Using execution timout of {seconds} seconds", executionTimeoutSeconds);
            using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(executionTimeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeoutCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not kill process that did not exit in time");
                }
                throw new TimeoutException($"Process did not exit within {executionTimeoutSeconds} seconds.");
            }

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            logger.LogTrace("Stdout Result: {result}", standardOutput);
            logger.LogTrace("Stderr Result: {result}", standardError);
            logger.LogTrace("Process completed with exit code {exitCode}", process.ExitCode);
            //tesla-control writes its diagnostic log and its error messages both to stderr, so they are separated
            //here: the log stays out of the result message and is surfaced as debug output instead.
            var (errorMessage, debugOutput) = SplitStandardError(standardError);
            commandResult.DebugOutput = string.IsNullOrWhiteSpace(debugOutput) ? null : debugOutput;
            commandResult.ResultMessage = standardOutput;
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

    /// <summary>
    /// Splits the standard error output into the actual error message and the diagnostic log. tesla-control logs with
    /// a "&lt;timestamp&gt; [level]" prefix (only when started with -debug), while its error messages are written
    /// without any prefix.
    /// </summary>
    internal static (string ErrorMessage, string DebugOutput) SplitStandardError(string? standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError))
        {
            return (string.Empty, string.Empty);
        }
        var errorLines = new List<string>();
        var debugLines = new List<string>();
        foreach (var line in standardError.Split('\n'))
        {
            var trimmedLine = line.TrimEnd('\r');
            if (LogLineRegex().IsMatch(trimmedLine))
            {
                debugLines.Add(trimmedLine);
            }
            else
            {
                errorLines.Add(trimmedLine);
            }
        }
        return (string.Join("\n", errorLines).Trim(), string.Join("\n", debugLines).Trim());
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\S*\s+\[(?:debug|info|warn|error)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex LogLineRegex();
}
