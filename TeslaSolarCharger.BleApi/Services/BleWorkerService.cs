using PkSoftwareService.Custom.Backend.Ble;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Dtos.Worker;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

/// <summary>
/// Supervises one long living tesla-bled worker per Bluetooth adapter. Each worker opens its adapter exactly once
/// (no HCIDEVDOWN/UP cycling, the only remaining hard failure class of the per-command tesla-control model) and is
/// lazy started on the first request for its adapter. Requests to different adapters run concurrently; requests to
/// the same adapter serialize on that adapter's gate.
/// </summary>
public class BleWorkerService : IBleWorkerService, IDisposable
{
    private const string WorkerPath = "/app/go/tesla-bled";
    private const int MaxEvents = 2000;
    private static readonly TimeSpan[] StartBackoffs =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(60),
    };

    private readonly ILogger<BleWorkerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAdapterEnumerationService _adapterEnumerationService;
    private readonly IBlePresenceRegistry _presenceRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, WorkerInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<DtoBleWorkerEvent> _events = new();
    private readonly Timer _sweepTimer;

    public BleWorkerService(ILogger<BleWorkerService> logger,
        IConfiguration configuration,
        IAdapterEnumerationService adapterEnumerationService,
        IBlePresenceRegistry presenceRegistry,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _adapterEnumerationService = adapterEnumerationService;
        _presenceRegistry = presenceRegistry;
        _timeProvider = timeProvider;
        _sweepTimer = new Timer(_ => Sweep(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Per adapter worker state. Everything that used to be container global (gate, keep warm window, ownership
    /// guard, backoff) lives here so adapters never interfere with each other.
    /// </summary>
    private sealed class WorkerInstance
    {
        public required string Key { get; init; }
        public required string HciId { get; set; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public object StateLock { get; } = new();
        public Process? Process;
        public bool UseDebugOfRunningWorker;
        public bool StopRequested;
        public DateTimeOffset? StartedAtUtc;
        public DateTimeOffset? LastRequestUtc;
        public DateTimeOffset? KeepWarmUntil;
        /// <summary>
        /// Last moment any process owned this adapter's exclusive user channel (worker exit or pairing tesla-control
        /// exit). Opening the adapter again too fast after that fails with "can't init hci"; measured: 33 % failures
        /// at 0 s, 0 % at 2 s.
        /// </summary>
        public DateTimeOffset LastAdapterOwnerExitUtc = DateTimeOffset.MinValue;
        public DateTimeOffset BackoffUntil = DateTimeOffset.MinValue;
        /// <summary>Last worker restart caused by a deaf adapter, so a broken one is not restarted every sweep.</summary>
        public DateTimeOffset LastDeafRestartUtc = DateTimeOffset.MinValue;
        /// <summary>
        /// Consecutive failures to reach a car this adapter can hear. Any command that got through resets it, so this
        /// only grows while the radio is listening to a car it cannot open a connection to.
        /// </summary>
        public int ConsecutiveUnreachableWhileAudible;
        /// <summary>
        /// Last worker restart caused by that, so a car that refuses connections for its own reasons does not restart
        /// the worker over and over.
        /// </summary>
        public DateTimeOffset LastUnreachableRestartUtc = DateTimeOffset.MinValue;
        public int ConsecutiveStartFailures;
        public int RequestsSent;
        public int NextRequestId;
        public int PendingRequestId;
        public TaskCompletionSource<string>? PendingResponse;
        public StringBuilder ErrorBuffer { get; } = new();
        public string? LastError;
        public ConcurrentDictionary<string, int> OutcomeCounts { get; } = new();
    }

    public async Task<DtoBleCommandResult> ExecuteCommand(string? adapter, string vin, string command, List<string> parameters,
        int? keepWarmSeconds, bool useDebug)
    {
        _logger.LogTrace("{method}({adapter}, {vin}, {command}, {@parameters}, {keepWarmSeconds}, {useDebug})",
            nameof(ExecuteCommand), adapter, vin, command, parameters, keepWarmSeconds, useDebug);
        var resolution = _adapterEnumerationService.Resolve(adapter);
        if (!resolution.Found)
        {
            return CountOutcome(resolution.Key, WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.AdapterNotFound,
                $"The configured Bluetooth adapter {adapter} is not present on this host. Check the adapter selection of the car or replug the adapter."));
        }
        var instance = GetInstance(resolution);
        UpdateKeepWarm(instance, keepWarmSeconds);
        var payload = new
        {
            id = 0,
            kind = "command",
            vin,
            command,
            @params = parameters,
        };
        //The worker may have to connect first, so the response timeout covers connecting plus the command.
        var responseTimeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("ConnectTimeoutSeconds")
                                                   + _configuration.GetValue<int>("CommandTimeoutSeconds") + 5);
        var (response, failure) = await SendRequest(instance, requestId => payload with { id = requestId }, responseTimeout, useDebug).ConfigureAwait(false);
        if (failure != default)
        {
            return CountOutcome(instance, WorkerResponseMapper.CreateLocalFailure(failure.Outcome, failure.Message));
        }
        var result = WorkerResponseMapper.ToCommandResult(response!);
        result.ResultMessage = result.ResultMessage?.Trim();
        var now = _timeProvider.GetUtcNow();
        _presenceRegistry.NoteCommandOutcome(instance.Key, vin, result.Outcome, now);
        var heardRecently = _presenceRegistry.WasHeardWithin(instance.Key, vin, PresenceMaxAge(null), now);
        if (result.Outcome == BleCommandOutcome.LinkFailed && !heardRecently)
        {
            //The worker only reports that it could not reach the car. Whether that means the car is gone is decided
            //here, from what the background scan heard: it is the only place that knows.
            result.Outcome = BleCommandOutcome.CarAbsent;
            result.ErrorType = WorkerResponseMapper.MapLegacyErrorType(BleCommandOutcome.CarAbsent);
        }
        NoteReachability(instance, result.Outcome, heardRecently);
        return CountOutcome(instance, result);
    }

    /// <summary>
    /// What is known about the given cars, answered from the presence registry without a worker round trip. Starts
    /// the worker when none is running, because the background scan only exists while it does.
    /// </summary>
    public async Task<DtoBlePresenceResult> Presence(string? adapter, List<string> vins, int? keepWarmSeconds, int? maxAgeSeconds)
    {
        _logger.LogTrace("{method}({adapter}, {@vins}, {keepWarmSeconds}, {maxAgeSeconds})", nameof(Presence), adapter, vins, keepWarmSeconds, maxAgeSeconds);
        var resolution = _adapterEnumerationService.Resolve(adapter);
        if (!resolution.Found)
        {
            return new DtoBlePresenceResult
            {
                Adapter = adapter,
                ErrorMessage = $"The configured Bluetooth adapter {adapter} is not present on this host. Check the adapter selection of the car or replug the adapter.",
            };
        }
        var instance = GetInstance(resolution);
        UpdateKeepWarm(instance, keepWarmSeconds);
        await EnsureWorkerStartedForScanning(instance).ConfigureAwait(false);
        return _presenceRegistry.GetPresence(instance.Key, vins, PresenceMaxAge(maxAgeSeconds), _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// How long an adapter may hear nothing at all before it counts as deaf. Well above any normal quiet period: a
    /// site with no other Bluetooth devices still hears its own cars every few tens of milliseconds.
    /// </summary>
    private TimeSpan DeafnessThreshold() =>
        TimeSpan.FromSeconds(_configuration.GetValue<int?>("ScanDeafAfterSeconds") ?? 90);

    /// <summary>
    /// Whether the adapter has gone deaf and enough time has passed since the last attempt to fix it. The cooldown
    /// keeps a permanently broken adapter - a dead USB dongle, say - from restarting its worker every sweep forever;
    /// each restart cycles the adapter, so retrying every few minutes is the most that is useful.
    /// </summary>
    private bool ShouldRestartForDeafness(WorkerInstance instance, DateTimeOffset now)
    {
        if (!_presenceRegistry.IsDeaf(instance.Key, DeafnessThreshold(), now))
        {
            return false;
        }
        var cooldown = TimeSpan.FromSeconds(_configuration.GetValue<int?>("ScanDeafRestartCooldownSeconds") ?? 300);
        lock (instance.StateLock)
        {
            if (now - instance.LastDeafRestartUtc < cooldown)
            {
                return false;
            }
            instance.LastDeafRestartUtc = now;
        }
        return true;
    }

    private TimeSpan PresenceMaxAge(int? maxAgeSeconds)
    {
        var seconds = maxAgeSeconds ?? _configuration.GetValue<int?>("PresenceMaxAgeSeconds") ?? 90;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 10, 3600));
    }

    /// <summary>
    /// Makes sure the adapter's worker runs so its background scan feeds the registry. Never waits for the gate: a
    /// held gate means a request is in flight, which means the worker is already running, and a presence answer must
    /// not queue behind a command.
    /// </summary>
    private async Task EnsureWorkerStartedForScanning(WorkerInstance instance)
    {
        if (!await instance.Gate.WaitAsync(TimeSpan.Zero).ConfigureAwait(false))
        {
            return;
        }
        try
        {
            await EnsureWorkerRunning(instance, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start the BLE worker for {adapter} to feed the presence registry", instance.Key);
        }
        finally
        {
            instance.Gate.Release();
        }
    }

    public async Task<DtoBleCommandResult> RunWithExclusiveAdapter(string? adapter, Func<string, Task<DtoBleCommandResult>> action)
    {
        _logger.LogTrace("{method}({adapter})", nameof(RunWithExclusiveAdapter), adapter);
        var resolution = _adapterEnumerationService.Resolve(adapter);
        if (!resolution.Found)
        {
            return WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.AdapterNotFound,
                $"The configured Bluetooth adapter {adapter} is not present on this host. Check the adapter selection of the car or replug the adapter.");
        }
        var instance = GetInstance(resolution);
        var gateWaitSeconds = _configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        if (!await instance.Gate.WaitAsync(TimeSpan.FromSeconds(gateWaitSeconds)).ConfigureAwait(false))
        {
            return WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.WorkerTimeout,
                "The Bluetooth adapter did not become free in time.");
        }
        try
        {
            //The external process needs the adapter exclusively: stop the worker for the duration. It starts again
            //lazily on the next request for this adapter; other adapters' workers keep serving.
            await StopWorkerCore(instance, "adapter handed over to an external process").ConfigureAwait(false);
            await WaitForOwnershipGuard(instance).ConfigureAwait(false);
            var result = await action(instance.HciId).ConfigureAwait(false);
            //The external process owned the adapter; the next worker start must respect the guard again.
            RecordAdapterOwnerExit(instance);
            return result;
        }
        finally
        {
            instance.Gate.Release();
        }
    }

    public async Task<DtoBleCommandResult> PingWorker(string? adapter)
    {
        _logger.LogTrace("{method}({adapter})", nameof(PingWorker), adapter);
        var resolution = _adapterEnumerationService.Resolve(adapter);
        if (!resolution.Found)
        {
            return WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.AdapterNotFound,
                $"The Bluetooth adapter {adapter} is not present on this host.");
        }
        if (!_instances.TryGetValue(resolution.Key, out var instance))
        {
            return WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.WorkerError,
                $"No BLE worker has been started for adapter {resolution.Key} yet.");
        }
        lock (instance.StateLock)
        {
            if (instance.Process is not { HasExited: false })
            {
                return WorkerResponseMapper.CreateLocalFailure(BleCommandOutcome.WorkerError,
                    $"The BLE worker for adapter {resolution.Key} is not running.");
            }
        }
        var payload = new { id = 0, kind = "ping", };
        var (response, failure) = await SendRequest(instance, requestId => payload with { id = requestId },
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        return failure != default
            ? WorkerResponseMapper.CreateLocalFailure(failure.Outcome, failure.Message)
            : WorkerResponseMapper.ToCommandResult(response!);
    }

    public async Task RestartWorkers(string? adapter, string reason)
    {
        _logger.LogTrace("{method}({adapter}, {reason})", nameof(RestartWorkers), adapter, reason);
        var instances = _instances.Values.ToList();
        if (!string.IsNullOrEmpty(adapter))
        {
            var resolution = _adapterEnumerationService.Resolve(adapter);
            instances = instances.Where(i => string.Equals(i.Key, resolution.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        foreach (var instance in instances)
        {
            //Only stop while no request runs, otherwise the adapter would be pulled away mid command.
            var gateWaitSeconds = _configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
            if (!await instance.Gate.WaitAsync(TimeSpan.FromSeconds(gateWaitSeconds)).ConfigureAwait(false))
            {
                _logger.LogWarning("Could not stop the BLE worker for {adapter}: it stayed busy", instance.Key);
                continue;
            }
            try
            {
                await StopWorkerCore(instance, reason).ConfigureAwait(false);
            }
            finally
            {
                instance.Gate.Release();
            }
        }
    }

    public List<DtoBleWorkerStatus> GetStatuses()
    {
        var statuses = new List<DtoBleWorkerStatus>();
        var idleTimeoutSeconds = _configuration.GetValue<int>("BleDaemonIdleTimeoutSeconds");
        foreach (var instance in _instances.Values.OrderBy(i => i.Key))
        {
            lock (instance.StateLock)
            {
                var isRunning = instance.Process is { HasExited: false };
                var lastActivity = instance.LastRequestUtc ?? instance.StartedAtUtc;
                var now = _timeProvider.GetUtcNow();
                statuses.Add(new DtoBleWorkerStatus
                {
                    Adapter = instance.Key,
                    HciId = instance.HciId,
                    IsRunning = isRunning,
                    UseDebug = instance.UseDebugOfRunningWorker,
                    StartedAtUtc = instance.StartedAtUtc,
                    UptimeSeconds = isRunning && instance.StartedAtUtc is { } startedAt ? (now - startedAt).TotalSeconds : null,
                    LastRequestUtc = instance.LastRequestUtc,
                    RequestsSent = instance.RequestsSent,
                    KeepWarmUntil = instance.KeepWarmUntil,
                    SecondsUntilIdleStop = isRunning && lastActivity is { } activity && idleTimeoutSeconds > 0
                        ? Math.Max(0, idleTimeoutSeconds - (now - activity).TotalSeconds)
                        : null,
                    WorkerRssBytes = isRunning ? ReadWorkerRss(instance.Process) : null,
                    WorkerCpuSeconds = isRunning ? ReadWorkerCpuSeconds(instance.Process) : null,
                    LastError = instance.LastError,
                    OutcomeCounts = new Dictionary<string, int>(instance.OutcomeCounts),
                });
            }
        }
        return statuses;
    }

    public List<DtoBleWorkerEvent> GetEvents(string? adapter, int? tail)
    {
        var events = _events.ToList();
        if (!string.IsNullOrEmpty(adapter))
        {
            events = events.Where(e => string.Equals(e.Adapter, adapter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (tail is > 0 && events.Count > tail.Value)
        {
            events = events.Skip(events.Count - tail.Value).ToList();
        }
        return events;
    }

    public IReadOnlyCollection<string> GetRunningAdapterKeys()
    {
        return _instances.Values
            .Where(i => { lock (i.StateLock) { return i.Process is { HasExited: false }; } })
            .Select(i => i.Key)
            .ToList();
    }

    private WorkerInstance GetInstance(AdapterResolution resolution)
    {
        var instance = _instances.GetOrAdd(resolution.Key, key => new WorkerInstance { Key = key, HciId = resolution.HciId });
        //hciX numbering can change between reboots and replugs; always track the latest resolution.
        lock (instance.StateLock)
        {
            if (!string.IsNullOrEmpty(resolution.HciId))
            {
                instance.HciId = resolution.HciId;
            }
        }
        return instance;
    }

    private void UpdateKeepWarm(WorkerInstance instance, int? keepWarmSeconds)
    {
        //Requests without the parameter never touch the stored window - neither extend nor clear it.
        if (keepWarmSeconds is not { } keepWarm)
        {
            return;
        }
        if (keepWarm is < 1 or > 86400)
        {
            _logger.LogWarning("Ignoring out of range keepWarmSeconds value {value}", keepWarm);
            return;
        }
        lock (instance.StateLock)
        {
            instance.KeepWarmUntil = _timeProvider.GetUtcNow().AddSeconds(keepWarm);
        }
    }

    private sealed record WorkerFailure(BleCommandOutcome Outcome, string Message);

    private async Task<(WorkerResponse? Response, WorkerFailure? Failure)> SendRequest(WorkerInstance instance,
        Func<int, object> createPayload, TimeSpan responseTimeout, bool? useDebug = null)
    {
        var gateWaitSeconds = _configuration.GetValue<int>("SemaphoreSlimWaitTimeoutSeconds");
        if (!await instance.Gate.WaitAsync(TimeSpan.FromSeconds(gateWaitSeconds)).ConfigureAwait(false))
        {
            _logger.LogError("The Bluetooth adapter {adapter} did not become free in time", instance.Key);
            return (null, new WorkerFailure(BleCommandOutcome.WorkerTimeout, "The Bluetooth adapter did not become free in time."));
        }
        try
        {
            try
            {
                await EnsureWorkerRunning(instance, useDebug).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (instance.StateLock)
                {
                    instance.LastError = ex.Message;
                }
                return (null, new WorkerFailure(BleCommandOutcome.AdapterUnavailable, ex.Message));
            }
            int requestId;
            TaskCompletionSource<string> responseCompletion;
            Process process;
            lock (instance.StateLock)
            {
                process = instance.Process!;
                requestId = ++instance.NextRequestId;
                instance.RequestsSent++;
                instance.LastRequestUtc = _timeProvider.GetUtcNow();
                instance.ErrorBuffer.Clear();
                responseCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                instance.PendingResponse = responseCompletion;
                instance.PendingRequestId = requestId;
            }
            var payload = WorkerResponseMapper.SerializeRequest(createPayload(requestId));
            AddEvent(instance, "request", payload);
            await process.StandardInput.WriteLineAsync(payload).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            var completedTask = await Task.WhenAny(responseCompletion.Task, Task.Delay(responseTimeout)).ConfigureAwait(false);
            if (completedTask != responseCompletion.Task)
            {
                //A worker that does not answer is unusable: kill it so the next request starts a fresh one.
                _logger.LogError("BLE worker for {adapter} did not answer within {seconds} s, killing it", instance.Key, responseTimeout.TotalSeconds);
                lock (instance.StateLock)
                {
                    instance.LastError = $"No answer within {responseTimeout.TotalSeconds:0} s";
                }
                await StopWorkerCore(instance, "worker did not answer in time", killImmediately: true).ConfigureAwait(false);
                return (null, new WorkerFailure(BleCommandOutcome.WorkerTimeout,
                    $"The BLE worker did not answer within {responseTimeout.TotalSeconds:0} seconds and was restarted."));
            }
            string responseLine;
            try
            {
                responseLine = await responseCompletion.Task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var stderrTail = GetBufferedError(instance);
                var message = string.IsNullOrEmpty(stderrTail) ? ex.Message : $"{ex.Message} Worker error output: {stderrTail}";
                return (null, new WorkerFailure(BleCommandOutcome.WorkerError, message));
            }
            var response = WorkerResponseMapper.ParseLine(responseLine);
            if (response == default)
            {
                return (null, new WorkerFailure(BleCommandOutcome.WorkerError, $"Could not parse the BLE worker's answer: {responseLine}"));
            }
            return (response, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while talking to the BLE worker for {adapter}", instance.Key);
            lock (instance.StateLock)
            {
                instance.LastError = ex.Message;
            }
            return (null, new WorkerFailure(BleCommandOutcome.WorkerError, $"Unhandled Error: {ex.Message}"));
        }
        finally
        {
            lock (instance.StateLock)
            {
                instance.LastRequestUtc = _timeProvider.GetUtcNow();
            }
            instance.Gate.Release();
        }
    }

    /// <summary>
    /// Starts the worker of the adapter if needed. <paramref name="requestedUseDebug"/> is the debug setting TSC sent
    /// with the request; null means "keep whatever the running worker was started with", which is what the keep warm
    /// restart and the liveness probe need as neither of them carries a setting of its own.
    /// </summary>
    private async Task EnsureWorkerRunning(WorkerInstance instance, bool? requestedUseDebug)
    {
        bool needsStart;
        bool useDebug;
        lock (instance.StateLock)
        {
            useDebug = requestedUseDebug ?? instance.UseDebugOfRunningWorker;
            var isRunning = instance.Process is { HasExited: false };
            //A changed debug setting can only be applied by restarting: the log level of the used library is global
            //per process.
            needsStart = !isRunning || instance.UseDebugOfRunningWorker != useDebug;
            if (needsStart && _timeProvider.GetUtcNow() < instance.BackoffUntil)
            {
                var message = $"The BLE worker for adapter {instance.Key} failed to start {instance.ConsecutiveStartFailures} times in a row; " +
                              $"next attempt after {instance.BackoffUntil:O}. Last error: {instance.LastError}";
                throw new InvalidOperationException(message);
            }
        }
        if (!needsStart)
        {
            return;
        }
        await StopWorkerCore(instance, "restart required").ConfigureAwait(false);
        try
        {
            await StartWorker(instance, useDebug).ConfigureAwait(false);
            lock (instance.StateLock)
            {
                instance.ConsecutiveStartFailures = 0;
                instance.BackoffUntil = DateTimeOffset.MinValue;
            }
        }
        catch (Exception)
        {
            lock (instance.StateLock)
            {
                instance.ConsecutiveStartFailures++;
                var backoff = StartBackoffs[Math.Min(instance.ConsecutiveStartFailures, StartBackoffs.Length) - 1];
                instance.BackoffUntil = _timeProvider.GetUtcNow().Add(backoff);
            }
            throw;
        }
    }

    private async Task StartWorker(WorkerInstance instance, bool useDebug)
    {
        var privateKeyLocation = _configuration.GetValue<string>("PrivateKeyPath");
        if (string.IsNullOrEmpty(privateKeyLocation))
        {
            throw new InvalidOperationException("PrivateKeyPath is not set in the configuration");
        }
        await WaitForOwnershipGuard(instance).ConfigureAwait(false);
        var teslaCacheFilePath = _configuration.GetValue<string>("TeslaCacheFilePath");
        var adapterParameterString = string.IsNullOrEmpty(instance.HciId) ? string.Empty : $"-bt-adapter {instance.HciId} ";
        var debugParameterString = useDebug ? "-debug " : string.Empty;
        var connectionWindowSeconds = _configuration.GetValue<int>("BleDaemonConnectionWindowSeconds");
        var commandTimeoutSeconds = _configuration.GetValue<int>("CommandTimeoutSeconds");
        var connectTimeoutSeconds = _configuration.GetValue<int>("ConnectTimeoutSeconds");
        var scanStream = _configuration.GetValue<bool?>("ScanStreamEnabled") ?? true;
        var digestIntervalMs = _configuration.GetValue<int?>("ScanDigestIntervalMs") ?? 500;
        var digestIdleSeconds = _configuration.GetValue<int?>("ScanDigestIdleIntervalSeconds") ?? 5;
        var maxDigestDevices = _configuration.GetValue<int?>("ScanMaxDigestDevices") ?? 64;
        var arguments = $"{debugParameterString}{adapterParameterString}-session-cache {teslaCacheFilePath} " +
                        $"-key-file {privateKeyLocation} -connection-window {connectionWindowSeconds}s " +
                        $"-command-timeout {commandTimeoutSeconds}s -connect-timeout {connectTimeoutSeconds}s " +
                        $"-scan-stream={scanStream.ToString().ToLowerInvariant()} " +
                        $"-digest-interval {digestIntervalMs}ms -digest-idle-interval {digestIdleSeconds}s " +
                        $"-max-digest-devices {maxDigestDevices}";
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
        lock (instance.StateLock)
        {
            instance.Process = process;
            instance.UseDebugOfRunningWorker = useDebug;
            instance.StartedAtUtc = _timeProvider.GetUtcNow();
            instance.LastError = null;
            instance.StopRequested = false;
            instance.ErrorBuffer.Clear();
            readyCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            instance.PendingResponse = readyCompletion;
            instance.PendingRequestId = 0;
        }
        process.Exited += (_, _) => HandleWorkerExited(instance);
        AddEvent(instance, "start", $"Starting BLE worker: {arguments}");
        try
        {
            process.Start();
            _ = Task.Run(() => ReadStandardOutput(instance, process));
            _ = Task.Run(() => ReadStandardError(instance, process));
        }
        catch (Exception ex)
        {
            AddEvent(instance, "error", $"Could not start BLE worker: {ex.Message}");
            await StopWorkerCore(instance, "start failed").ConfigureAwait(false);
            throw new InvalidOperationException($"Could not start BLE worker: {ex.Message}", ex);
        }
        var readyTimeoutSeconds = _configuration.GetValue<int?>("WorkerReadyTimeoutSeconds") ?? 30;
        var completedTask = await Task.WhenAny(readyCompletion.Task, Task.Delay(TimeSpan.FromSeconds(readyTimeoutSeconds))).ConfigureAwait(false);
        if (completedTask != readyCompletion.Task || readyCompletion.Task.IsFaulted || !IsReadyMessage(await SafeResult(readyCompletion)))
        {
            var error = GetBufferedError(instance);
            var answer = readyCompletion.Task.IsCompletedSuccessfully ? readyCompletion.Task.Result : string.Empty;
            AddEvent(instance, "error", $"BLE worker did not become ready. Answer: {answer} Error output: {error}");
            await StopWorkerCore(instance, "worker did not become ready", killImmediately: true).ConfigureAwait(false);
            throw new InvalidOperationException($"BLE worker did not become ready. {answer} {error}".Trim());
        }
        AddEvent(instance, "ready", "BLE worker is ready");
    }

    /// <summary>
    /// Waits until the measured safe gap since the last adapter ownership transition has passed. Opening the HCI user
    /// channel too soon after the previous owner released it fails with "can't init hci".
    /// </summary>
    private async Task WaitForOwnershipGuard(WorkerInstance instance)
    {
        var guardSeconds = _configuration.GetValue<int?>("AdapterRestartGuardSeconds") ?? 2;
        DateTimeOffset lastOwnerExit;
        lock (instance.StateLock)
        {
            lastOwnerExit = instance.LastAdapterOwnerExitUtc;
        }
        var waitUntil = lastOwnerExit.AddSeconds(guardSeconds);
        var delay = waitUntil - _timeProvider.GetUtcNow();
        if (delay > TimeSpan.Zero)
        {
            _logger.LogDebug("Waiting {delay} before reopening adapter {adapter}", delay, instance.Key);
            await Task.Delay(delay).ConfigureAwait(false);
        }
    }

    private void RecordAdapterOwnerExit(WorkerInstance instance)
    {
        lock (instance.StateLock)
        {
            instance.LastAdapterOwnerExitUtc = _timeProvider.GetUtcNow();
        }
    }

    private async Task StopWorkerCore(WorkerInstance instance, string reason, bool killImmediately = false)
    {
        Process? process;
        lock (instance.StateLock)
        {
            process = instance.Process;
            instance.Process = null;
            instance.StopRequested = true;
        }
        if (process == default)
        {
            return;
        }
        try
        {
            if (!process.HasExited)
            {
                if (killImmediately)
                {
                    process.Kill(entireProcessTree: true);
                }
                else
                {
                    //A graceful exit lets the worker disconnect the vehicle and persist its sessions.
                    await process.StandardInput.WriteLineAsync("{\"kind\":\"exit\"}").ConfigureAwait(false);
                    await process.StandardInput.FlushAsync().ConfigureAwait(false);
                    process.StandardInput.Close();
                    using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await process.WaitForExitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not stop BLE worker for {adapter} gracefully, killing it", instance.Key);
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception killException)
            {
                _logger.LogError(killException, "Could not kill BLE worker for {adapter}", instance.Key);
            }
        }
        finally
        {
            process.Dispose();
            RecordAdapterOwnerExit(instance);
        }
        //Nothing is being heard on this adapter any more, so no car may be judged absent until a new scan has been
        //observing for a full max age again. The per car history is kept: a worker restart is not a car leaving.
        _presenceRegistry.ForgetAdapter(instance.Key);
        AddEvent(instance, "stop", $"BLE worker stopped ({reason})");
    }

    /// <summary>
    /// Periodic sweep over all instances: stops idle workers whose keep warm window has passed and proactively
    /// restarts a crashed worker while its keep warm window is still active, so the next command does not pay a cold
    /// start.
    /// </summary>
    private void Sweep()
    {
        var idleTimeoutSeconds = _configuration.GetValue<int>("BleDaemonIdleTimeoutSeconds");
        foreach (var instance in _instances.Values)
        {
            var now = _timeProvider.GetUtcNow();
            bool isRunning;
            bool keepWarmActive;
            DateTimeOffset? lastActivity;
            DateTimeOffset backoffUntil;
            lock (instance.StateLock)
            {
                isRunning = instance.Process is { HasExited: false };
                keepWarmActive = instance.KeepWarmUntil is { } keepWarmUntil && keepWarmUntil > now;
                lastActivity = instance.LastRequestUtc ?? instance.StartedAtUtc;
                backoffUntil = instance.BackoffUntil;
            }
            if (isRunning)
            {
                //Checked before the idle and keep warm exits below: a deaf adapter is almost always one that is
                //being kept warm, so testing it after those would never run at all.
                if (ShouldRestartForDeafness(instance, now))
                {
                    _ = Task.Run(async () =>
                    {
                        //The adapter is up and scanning but has received nothing at all. Only a fresh adapter bind
                        //recovers from that, so the worker is restarted rather than the scan merely re-armed.
                        AddEvent(instance, "deaf", "The adapter received no advertisement at all, restarting its worker");
                        await RestartWorkers(instance.Key, "adapter heard nothing").ConfigureAwait(false);
                    });
                    continue;
                }
                if (ShouldRestartForUnreachableCar(instance, now))
                {
                    _ = Task.Run(async () =>
                    {
                        //The other half of a healthy radio: hearing a car is worth nothing if no connection to it can
                        //be opened. This state does not clear itself, and a restart clears it immediately.
                        AddEvent(instance, "unreachable", "The adapter could not connect to a car it can hear, restarting its worker");
                        await RestartWorkers(instance.Key, "car audible but unreachable").ConfigureAwait(false);
                    });
                    continue;
                }
                if (idleTimeoutSeconds <= 0 || keepWarmActive)
                {
                    continue;
                }
                if (lastActivity is not { } activity || (now - activity).TotalSeconds < idleTimeoutSeconds)
                {
                    continue;
                }
                _ = Task.Run(async () =>
                {
                    //Only stop while no command is running, otherwise the adapter would be pulled away mid request.
                    if (!await instance.Gate.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        return;
                    }
                    try
                    {
                        AddEvent(instance, "idle", $"No request for {idleTimeoutSeconds} s and no keep warm window, stopping BLE worker");
                        await StopWorkerCore(instance, "idle").ConfigureAwait(false);
                    }
                    finally
                    {
                        instance.Gate.Release();
                    }
                });
            }
            else if (keepWarmActive && now >= backoffUntil)
            {
                _ = Task.Run(async () =>
                {
                    if (!await instance.Gate.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false))
                    {
                        return;
                    }
                    try
                    {
                        AddEvent(instance, "keepWarm", "Keep warm window is active but the worker is not running, restarting it");
                        //No request behind this restart, so keep the debug setting of the last real request.
                        await EnsureWorkerRunning(instance, null).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not proactively restart the BLE worker for {adapter}", instance.Key);
                    }
                    finally
                    {
                        instance.Gate.Release();
                    }
                });
            }
        }
    }

    private async Task ReadStandardOutput(WorkerInstance instance, Process process)
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
                _logger.LogTrace("BLE worker {adapter} stdout: {line}", instance.Key, line);
                HandleWorkerLine(instance, line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while reading BLE worker output for {adapter}", instance.Key);
        }
        //End of output means no answer can arrive anymore. Faulting here rather than in the exit event makes sure a
        //final message (e.g. why starting failed) is still delivered to the caller.
        TaskCompletionSource<string>? pending;
        lock (instance.StateLock)
        {
            pending = instance.PendingResponse;
            instance.PendingResponse = null;
        }
        pending?.TrySetException(new InvalidOperationException("BLE worker ended without answering"));
    }

    private void HandleWorkerLine(WorkerInstance instance, string line)
    {
        var parsed = WorkerResponseMapper.ParseLine(line);
        if (WorkerResponseMapper.IsScanEvent(parsed))
        {
            //Unsolicited output of the background scan. It shares this pipe with request answers, so it is routed
            //before anything else looks at it and can never be mistaken for a response.
            var now = _timeProvider.GetUtcNow();
            if (parsed!.Kind == "adv")
            {
                _presenceRegistry.ApplyDigest(instance.Key, parsed, now);
            }
            else
            {
                _presenceRegistry.ApplyScanState(instance.Key, parsed.State, parsed.Reason, now);
                //Only state changes are recorded: at two digests per second the event ring would otherwise hold
                //nothing but advertisements.
                AddEvent(instance, "scan", line);
            }
            return;
        }
        TaskCompletionSource<string>? completionToSignal = null;
        lock (instance.StateLock)
        {
            if (instance.PendingResponse == default)
            {
                //Nothing waits for this line (e.g. the reply to an exit request).
                AddEvent(instance, "unmatched", line);
                return;
            }
            //During startup the pending id is 0 and the expected line is the ready message; afterwards a result line
            //must carry the id of the in-flight request. Anything else is discarded so a stale line can never be
            //mistaken for the answer to a newer request.
            if (!WorkerResponseMapper.IsResponseTo(parsed, instance.PendingRequestId))
            {
                _logger.LogWarning("Discarding unexpected BLE worker line for {adapter}: {line}", instance.Key, line);
                AddEvent(instance, "unmatched", line);
                return;
            }
            completionToSignal = instance.PendingResponse;
            instance.PendingResponse = null;
        }
        AddEvent(instance, "response", line);
        completionToSignal.TrySetResult(line);
    }

    private async Task ReadStandardError(WorkerInstance instance, Process process)
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
                _logger.LogDebug("BLE worker {adapter} stderr: {line}", instance.Key, line);
                lock (instance.StateLock)
                {
                    instance.ErrorBuffer.AppendLine(line);
                    //Bounded: with -debug the worker logs every packet and the buffer would otherwise grow without
                    //limit between requests.
                    if (instance.ErrorBuffer.Length > 64 * 1024)
                    {
                        instance.ErrorBuffer.Remove(0, instance.ErrorBuffer.Length - 32 * 1024);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while reading BLE worker error output for {adapter}", instance.Key);
        }
    }

    private string GetBufferedError(WorkerInstance instance)
    {
        lock (instance.StateLock)
        {
            return instance.ErrorBuffer.ToString().Trim();
        }
    }

    private void HandleWorkerExited(WorkerInstance instance)
    {
        AddEvent(instance, "exited", $"BLE worker exited. Error output: {GetBufferedError(instance)}");
        RecordAdapterOwnerExit(instance);
        lock (instance.StateLock)
        {
            //Stopping on purpose (idle, debug change, pairing) is not an error.
            if (!instance.StopRequested)
            {
                instance.LastError ??= "BLE worker exited unexpectedly";
            }
        }
    }

    /// <summary>
    /// Records whether the car could actually be reached, which is what the connect watchdog acts on.
    ///
    /// Only the combination matters: the radio hears the car, and we still cannot open a connection to it. Measured on
    /// the live system, a worker gets into that state and stays there - ten connect attempts in a row timing out at
    /// their full ten seconds while the car sat in the driveway at -46 dBm - and a worker restart clears it instantly,
    /// with the next connect taking 752 ms. Nothing below the worker recovers on its own.
    /// </summary>
    private void NoteReachability(WorkerInstance instance, BleCommandOutcome? outcome, bool heardRecently)
    {
        int streak;
        lock (instance.StateLock)
        {
            streak = NextUnreachableStreak(instance.ConsecutiveUnreachableWhileAudible, outcome, heardRecently);
            instance.ConsecutiveUnreachableWhileAudible = streak;
        }
        if (streak > 0)
        {
            _logger.LogDebug("Could not reach an audible car on {adapter}, {streak} times in a row", instance.Key, streak);
        }
    }

    /// <summary>
    /// The streak after one command outcome.
    ///
    /// Only a car that was heard and still could not be reached counts. Anything the car answered clears the streak,
    /// whatever it answered - a refusal proves the link as well as a success does. Everything else leaves the streak
    /// alone: a car that is not being heard says nothing about the radio, and outcomes that never got as far as the
    /// link say nothing either.
    /// </summary>
    public static int NextUnreachableStreak(int currentStreak, BleCommandOutcome? outcome, bool heardRecently) =>
        outcome switch
        {
            BleCommandOutcome.Ok or BleCommandOutcome.CarRefused or BleCommandOutcome.CarAsleep => 0,
            BleCommandOutcome.LinkFailed when heardRecently => currentStreak + 1,
            _ => currentStreak,
        };

    /// <summary>
    /// Whether a streak of failures to reach an audible car has gone on long enough to restart the worker.
    ///
    /// The cooldown keeps a car that refuses connections for its own reasons from restarting the worker on every
    /// sweep. A threshold of zero or less turns the watchdog off.
    /// </summary>
    public static bool ShouldRestartForUnreachable(int streak, int threshold,
        DateTimeOffset lastRestart, TimeSpan cooldown, DateTimeOffset now) =>
        (threshold > 0) && (streak >= threshold) && ((now - lastRestart) >= cooldown);

    /// <summary>
    /// Whether the adapter has stopped being able to connect to cars it can hear, and enough time has passed since the
    /// last attempt to fix it.
    /// </summary>
    private bool ShouldRestartForUnreachableCar(WorkerInstance instance, DateTimeOffset now)
    {
        var threshold = _configuration.GetValue<int?>("ConnectFailuresBeforeWorkerRestart") ?? 5;
        var cooldown = TimeSpan.FromSeconds(_configuration.GetValue<int?>("UnreachableRestartCooldownSeconds") ?? 60);
        lock (instance.StateLock)
        {
            if (!ShouldRestartForUnreachable(instance.ConsecutiveUnreachableWhileAudible, threshold,
                    instance.LastUnreachableRestartUtc, cooldown, now))
            {
                return false;
            }
            instance.LastUnreachableRestartUtc = now;
            //Cleared here rather than after the restart, so the streak counts attempts since this restart.
            instance.ConsecutiveUnreachableWhileAudible = 0;
        }
        return true;
    }

    private DtoBleCommandResult CountOutcome(WorkerInstance instance, DtoBleCommandResult result)
    {
        instance.OutcomeCounts.AddOrUpdate(result.Outcome?.ToString() ?? "unknown", 1, (_, count) => count + 1);
        return result;
    }

    private DtoBleCommandResult CountOutcome(string adapterKey, DtoBleCommandResult result)
    {
        //AdapterNotFound failures have no instance; they are only visible in the container log and the result.
        _logger.LogError("BLE request for adapter {adapter} failed: {message}", adapterKey, result.ResultMessage);
        return result;
    }

    private static long? ReadWorkerRss(Process? process)
    {
        if (process == default)
        {
            return null;
        }
        try
        {
            //Best effort, Linux only: VmRSS of the worker process.
            foreach (var line in File.ReadLines($"/proc/{process.Id}/status"))
            {
                if (!line.StartsWith("VmRSS:", StringComparison.Ordinal))
                {
                    continue;
                }
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kiloBytes))
                {
                    return kiloBytes * 1024;
                }
            }
        }
        catch (Exception)
        {
            //Not available outside Linux.
        }
        return null;
    }

    /// <summary>
    /// Best effort, Linux only: user plus system CPU time of the worker process, read from /proc/[pid]/stat where
    /// fields 14 and 15 carry them in clock ticks.
    /// </summary>
    private static double? ReadWorkerCpuSeconds(Process? process)
    {
        if (process == default)
        {
            return null;
        }
        try
        {
            var stat = File.ReadAllText($"/proc/{process.Id}/stat");
            //The second field is the executable name in brackets and may itself contain spaces, so parsing starts
            //behind the closing bracket.
            var fieldsStart = stat.LastIndexOf(')');
            if (fieldsStart < 0)
            {
                return null;
            }
            var fields = stat[(fieldsStart + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            //After the name the fields shift by two: utime is field 14, which is index 11 here.
            if (fields.Length < 13 || !long.TryParse(fields[11], out var userTicks) || !long.TryParse(fields[12], out var systemTicks))
            {
                return null;
            }
            const double ticksPerSecond = 100d;
            return (userTicks + systemTicks) / ticksPerSecond;
        }
        catch (Exception)
        {
            //Not available outside Linux.
        }
        return null;
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
        return WorkerResponseMapper.ParseLine(line)?.Kind == "ready";
    }

    private void AddEvent(WorkerInstance instance, string kind, string message)
    {
        _events.Enqueue(new DtoBleWorkerEvent
        {
            TimestampUtc = _timeProvider.GetUtcNow(),
            Adapter = instance.Key,
            Kind = kind,
            Message = message,
        });
        while (_events.Count > MaxEvents && _events.TryDequeue(out _))
        {
        }
        _logger.LogDebug("BLE worker {adapter} [{kind}]: {message}", instance.Key, kind, message);
    }

    public void Dispose()
    {
        _sweepTimer.Dispose();
        foreach (var instance in _instances.Values)
        {
            StopWorkerCore(instance, "container shutdown").GetAwaiter().GetResult();
        }
        GC.SuppressFinalize(this);
    }
}
