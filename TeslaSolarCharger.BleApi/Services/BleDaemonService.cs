using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Enums;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class BleDaemonService : IBleDaemonService, IDisposable
{
    private const string WorkerPath = "/app/go/tesla-bled";
    private const int MaxEvents = 2000;

    private readonly ILogger<BleDaemonService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBleAdapterGate _bleAdapterGate;

    private readonly object _stateLock = new();
    private readonly ConcurrentQueue<DtoBleSessionEvent> _events = new();
    private readonly StringBuilder _errorBuffer = new();
    private readonly Timer _idleTimer;

    private Process? _process;
    private bool _useDebugOfRunningWorker;
    private string? _arguments;
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _lastRequestUtc;
    private int _requestsSent;
    private int _nextRequestId;
    private string? _lastError;
    private bool _stopRequested;
    private TaskCompletionSource<string>? _pendingResponse;

    public BleDaemonService(ILogger<BleDaemonService> logger, IConfiguration configuration, IBleAdapterGate bleAdapterGate)
    {
        _logger = logger;
        _configuration = configuration;
        _bleAdapterGate = bleAdapterGate;
        _idleTimer = new Timer(_ => StopWhenIdle(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, List<string> parameters, bool useDebug)
    {
        _logger.LogTrace("{method}({vin}, {command}, {@parameters}, {useDebug})", nameof(ExecuteCommand), vin, command, parameters, useDebug);
        return SendRequest(vin, command, parameters, useDebug);
    }

    public Task<DtoBleCommandResult> BeaconScan(string vin, bool useDebug)
    {
        _logger.LogTrace("{method}({vin})", nameof(BeaconScan), vin);
        return SendRequest(vin, "beacon-scan", new List<string>(), useDebug);
    }

    public async Task StopWorker()
    {
        await StopWorkerCore().ConfigureAwait(false);
    }

    private async Task<DtoBleCommandResult> SendRequest(string vin, string command, List<string> parameters, bool useDebug)
    {
        var gateWaitSeconds = _configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        if (!await _bleAdapterGate.WaitAsync(TimeSpan.FromSeconds(gateWaitSeconds)).ConfigureAwait(false))
        {
            _logger.LogError("Bluetooth adapter did not become free in time");
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = "SemaphoreSlim did not allow command execution in time",
                ErrorType = ErrorType.TeslaControl,
            };
        }
        try
        {
            await EnsureWorkerRunning(useDebug).ConfigureAwait(false);
            int requestId;
            TaskCompletionSource<string> responseCompletion;
            Process process;
            lock (_stateLock)
            {
                process = _process!;
                requestId = ++_nextRequestId;
                _requestsSent++;
                _lastRequestUtc = DateTimeOffset.UtcNow;
                _errorBuffer.Clear();
                responseCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingResponse = responseCompletion;
            }
            var payload = JsonSerializer.Serialize(new
            {
                id = requestId,
                vin,
                command,
                @params = parameters,
            });
            AddEvent("request", payload);
            await process.StandardInput.WriteLineAsync(payload).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            //The worker may have to connect first, so the request timeout covers connecting plus the command.
            var timeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("ConnectTimeoutSeconds")
                                               + _configuration.GetValue<int>("CommandTimeoutSeconds") + 15);
            var completedTask = await Task.WhenAny(responseCompletion.Task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completedTask != responseCompletion.Task)
            {
                //A worker that does not answer is unusable: stop it so the next request starts a fresh one.
                _logger.LogError("BLE worker did not answer within {seconds} s, restarting it", timeout.TotalSeconds);
                lock (_stateLock)
                {
                    _lastError = $"No answer within {timeout.TotalSeconds} s";
                }
                await StopWorkerCore().ConfigureAwait(false);
                return new DtoBleCommandResult()
                {
                    Success = false,
                    ResultMessage = $"BLE worker did not answer within {timeout.TotalSeconds:0} seconds.",
                    ErrorType = ErrorType.TeslaControl,
                };
            }
            var responseLine = await responseCompletion.Task.ConfigureAwait(false);
            return MapResponse(responseLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while executing BLE command");
            lock (_stateLock)
            {
                _lastError = ex.Message;
            }
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = $"Unhandled Error: {ex.Message}",
                ErrorType = ErrorType.Exceptional,
            };
        }
        finally
        {
            //No cooldown needed: unlike a fresh tesla-control process the worker does not reset the adapter.
            _bleAdapterGate.Release();
        }
    }

    private DtoBleCommandResult MapResponse(string responseLine)
    {
        var debugOutput = GetBufferedError();
        try
        {
            using var document = JsonDocument.Parse(responseLine);
            var root = document.RootElement;
            var isSuccess = root.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
            var result = new DtoBleCommandResult()
            {
                Success = isSuccess,
                DebugOutput = string.IsNullOrWhiteSpace(debugOutput) ? null : debugOutput,
            };
            if (isSuccess)
            {
                //Commands without a payload (e.g. charging-start) answer without a result object.
                result.ResultMessage = root.TryGetProperty("result", out var resultElement)
                    ? resultElement.GetRawText()
                    : string.Empty;
            }
            else
            {
                result.ResultMessage = root.TryGetProperty("error", out var errorElement)
                    ? errorElement.GetString()
                    : "BLE worker reported an unknown error";
                result.ErrorType = ErrorType.TeslaControl;
            }
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not parse answer of BLE worker: {answer}", responseLine);
            return new DtoBleCommandResult()
            {
                Success = false,
                ResultMessage = $"Could not parse answer of BLE worker: {responseLine}",
                ErrorType = ErrorType.Exceptional,
                DebugOutput = string.IsNullOrWhiteSpace(debugOutput) ? null : debugOutput,
            };
        }
    }

    private async Task EnsureWorkerRunning(bool useDebug)
    {
        bool needsStart;
        lock (_stateLock)
        {
            var isRunning = _process is { HasExited: false };
            //A changed debug setting can only be applied by restarting: the log level of the used library is global
            //per process.
            needsStart = !isRunning || _useDebugOfRunningWorker != useDebug;
        }
        if (!needsStart)
        {
            return;
        }
        await StopWorkerCore().ConfigureAwait(false);
        await StartWorker(useDebug).ConfigureAwait(false);
    }

    private async Task StartWorker(bool useDebug)
    {
        var privateKeyLocation = _configuration.GetValue<string>("PrivateKeyPath");
        if (string.IsNullOrEmpty(privateKeyLocation))
        {
            throw new InvalidOperationException("PrivateKeyPath is not set in the configuration");
        }
        var teslaCacheFilePath = _configuration.GetValue<string>("TeslaCacheFilePath");
        var bluetoothAdapter = _configuration.GetValue<string>("BluetoothAdapter");
        var bluetoothAdapterParameterString = string.IsNullOrEmpty(bluetoothAdapter) ? string.Empty : $"-bt-adapter {bluetoothAdapter} ";
        var debugParameterString = useDebug ? "-debug " : string.Empty;
        var connectionWindowSeconds = _configuration.GetValue<int>("BleDaemonConnectionWindowSeconds");
        var scanTimeoutSeconds = _configuration.GetValue<int>("BeaconScanTimeoutSeconds");
        var commandTimeoutSeconds = _configuration.GetValue<int>("CommandTimeoutSeconds");
        var connectTimeoutSeconds = _configuration.GetValue<int>("ConnectTimeoutSeconds");
        var arguments = $"{debugParameterString}{bluetoothAdapterParameterString}-session-cache {teslaCacheFilePath} " +
                        $"-key-file {privateKeyLocation} -connection-window {connectionWindowSeconds}s " +
                        $"-scan-timeout {scanTimeoutSeconds}s -command-timeout {commandTimeoutSeconds}s " +
                        $"-connect-timeout {connectTimeoutSeconds}s";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = WorkerPath,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };
        TaskCompletionSource<string> readyCompletion;
        lock (_stateLock)
        {
            _process = process;
            _useDebugOfRunningWorker = useDebug;
            _arguments = arguments;
            _startedAtUtc = DateTimeOffset.UtcNow;
            _lastError = null;
            _stopRequested = false;
            _errorBuffer.Clear();
            readyCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingResponse = readyCompletion;
        }
        process.Exited += (_, _) => HandleWorkerExited();
        AddEvent("start", $"Starting BLE worker: {arguments}");
        try
        {
            process.Start();
            _ = Task.Run(() => ReadStandardOutput(process));
            _ = Task.Run(() => ReadStandardError(process));
        }
        catch (Exception ex)
        {
            AddEvent("error", $"Could not start BLE worker: {ex.Message}");
            await StopWorkerCore().ConfigureAwait(false);
            throw new InvalidOperationException($"Could not start BLE worker: {ex.Message}", ex);
        }
        var completedTask = await Task.WhenAny(readyCompletion.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (completedTask != readyCompletion.Task || readyCompletion.Task.IsFaulted || !IsReadyMessage(await SafeResult(readyCompletion)))
        {
            var error = GetBufferedError();
            var answer = readyCompletion.Task.IsCompletedSuccessfully ? readyCompletion.Task.Result : string.Empty;
            AddEvent("error", $"BLE worker did not become ready. Answer: {answer} Error output: {error}");
            await StopWorkerCore().ConfigureAwait(false);
            throw new InvalidOperationException($"BLE worker did not become ready. {answer}{error}");
        }
        AddEvent("ready", "BLE worker is ready");
    }

    private static async Task<string> SafeResult(TaskCompletionSource<string> completionSource)
    {
        try
        {
            return await completionSource.Task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool IsReadyMessage(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "ready";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task StopWorkerCore()
    {
        Process? process;
        lock (_stateLock)
        {
            process = _process;
            _process = null;
            _stopRequested = true;
        }
        if (process == default)
        {
            return;
        }
        try
        {
            if (!process.HasExited)
            {
                //Closing stdin ends the worker's read loop so it can disconnect cleanly.
                await process.StandardInput.WriteLineAsync("{\"command\":\"exit\"}").ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                process.StandardInput.Close();
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not stop BLE worker gracefully, killing it");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception killException)
            {
                _logger.LogError(killException, "Could not kill BLE worker");
            }
        }
        finally
        {
            process.Dispose();
        }
        AddEvent("stop", "BLE worker stopped");
    }

    private void StopWhenIdle()
    {
        var idleTimeoutSeconds = _configuration.GetValue<int>("BleDaemonIdleTimeoutSeconds");
        if (idleTimeoutSeconds <= 0)
        {
            return;
        }
        lock (_stateLock)
        {
            if (_process is not { HasExited: false })
            {
                return;
            }
            var lastActivity = _lastRequestUtc ?? _startedAtUtc;
            if (lastActivity == default || (DateTimeOffset.UtcNow - lastActivity.Value).TotalSeconds < idleTimeoutSeconds)
            {
                return;
            }
        }
        _ = Task.Run(async () =>
        {
            //Only stop while no command is running, otherwise the adapter would be pulled away mid request.
            if (!await _bleAdapterGate.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false))
            {
                return;
            }
            try
            {
                AddEvent("idle", $"No request for {idleTimeoutSeconds} s, stopping BLE worker and freeing the adapter");
                await StopWorkerCore().ConfigureAwait(false);
            }
            finally
            {
                _bleAdapterGate.Release();
            }
        });
    }

    private async Task ReadStandardOutput(Process process)
    {
        try
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                if (line == default)
                {
                    break;
                }
                _logger.LogTrace("BLE worker stdout: {line}", line);
                TaskCompletionSource<string>? completionToSignal;
                lock (_stateLock)
                {
                    completionToSignal = _pendingResponse;
                    _pendingResponse = null;
                }
                AddEvent("response", line);
                completionToSignal?.TrySetResult(line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while reading BLE worker output");
        }
        //End of output means no answer can arrive anymore. Faulting here rather than in the exit event makes sure a
        //final message (e.g. why starting failed) is still delivered to the caller.
        TaskCompletionSource<string>? pending;
        lock (_stateLock)
        {
            pending = _pendingResponse;
            _pendingResponse = null;
        }
        pending?.TrySetException(new InvalidOperationException("BLE worker ended without answering"));
    }

    private async Task ReadStandardError(Process process)
    {
        try
        {
            while (true)
            {
                var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                if (line == default)
                {
                    break;
                }
                _logger.LogDebug("BLE worker stderr: {line}", line);
                lock (_stateLock)
                {
                    _errorBuffer.AppendLine(line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while reading BLE worker error output");
        }
    }

    private string GetBufferedError()
    {
        lock (_stateLock)
        {
            return _errorBuffer.ToString().Trim();
        }
    }

    private void HandleWorkerExited()
    {
        AddEvent("exited", $"BLE worker exited. Error output: {GetBufferedError()}");
        lock (_stateLock)
        {
            //Stopping on purpose (idle, debug change, pairing) is not an error.
            if (!_stopRequested)
            {
                _lastError ??= "BLE worker exited unexpectedly";
            }
        }
    }

    public DtoBleDaemonStatus GetStatus()
    {
        lock (_stateLock)
        {
            var isRunning = _process is { HasExited: false };
            var idleTimeoutSeconds = _configuration.GetValue<int>("BleDaemonIdleTimeoutSeconds");
            var lastActivity = _lastRequestUtc ?? _startedAtUtc;
            return new DtoBleDaemonStatus
            {
                IsRunning = isRunning,
                UseDebug = _useDebugOfRunningWorker,
                Arguments = _arguments,
                StartedAtUtc = _startedAtUtc,
                UptimeSeconds = _startedAtUtc is { } startedAtUtc && isRunning
                    ? (DateTimeOffset.UtcNow - startedAtUtc).TotalSeconds
                    : null,
                LastRequestUtc = _lastRequestUtc,
                RequestsSent = _requestsSent,
                LastError = _lastError,
                SecondsUntilIdleStop = isRunning && lastActivity is { } activity && idleTimeoutSeconds > 0
                    ? Math.Max(0, idleTimeoutSeconds - (DateTimeOffset.UtcNow - activity).TotalSeconds)
                    : null,
            };
        }
    }

    public List<DtoBleSessionEvent> GetEvents(int? tail)
    {
        var events = _events.ToList();
        if (tail is > 0 && events.Count > tail.Value)
        {
            events = events.Skip(events.Count - tail.Value).ToList();
        }
        return events;
    }

    private void AddEvent(string kind, string message)
    {
        _events.Enqueue(new DtoBleSessionEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Kind = kind,
            Message = message,
        });
        while (_events.Count > MaxEvents && _events.TryDequeue(out _))
        {
        }
        _logger.LogDebug("BLE worker [{kind}]: {message}", kind, message);
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        StopWorkerCore().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
