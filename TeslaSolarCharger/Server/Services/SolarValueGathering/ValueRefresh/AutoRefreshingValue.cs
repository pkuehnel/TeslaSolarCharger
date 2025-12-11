using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;

public interface IAutoRefreshingValue<T> : IGenericValue<T>
{
}


public sealed class AutoRefreshingValue<T> : GenericValueBase<T>, IAutoRefreshingValue<T>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Func<IServiceProvider, AutoRefreshingValue<T>, CancellationToken, Task> _startAsync;
    private readonly CancellationTokenSource _cts = new();
    private bool _isCanceled;

    public override SourceValueKey SourceValueKey { get; }

    public Task? RunningTask { get; private set; }
    public bool IsRunning { get; private set; }


    public AutoRefreshingValue(
        IServiceScopeFactory serviceScopeFactory,
        Func<IServiceProvider, AutoRefreshingValue<T>, CancellationToken, Task> startAsync,
        int historicValueCapacity,
        SourceValueKey sourceValueKey)
        : base(serviceScopeFactory, historicValueCapacity)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _startAsync = startAsync ?? throw new ArgumentNullException(nameof(startAsync));
        SourceValueKey = sourceValueKey;

        // Start the background logic immediately
        RunningTask = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        IsRunning = true;
        using var scope = _serviceScopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            await _startAsync(sp, this, _cts.Token).ConfigureAwait(false);
            ErrorMessage = null;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            SetErrorFromException(ex);
            throw;
        }
        finally
        {
            IsRunning = false;
        }
    }

    public override void Cancel()
    {
        if (_isCanceled)
            return;

        _isCanceled = true;

        try
        {
            _cts.Cancel();
        }
        catch
        {
            // ignored
        }
    }

    public override async ValueTask DisposeAsync()
    {
        Cancel();

        if (RunningTask is not null)
        {
            try
            {
                await RunningTask.ConfigureAwait(false);
            }
            catch
            {
                // ignored — error already recorded via SetErrorFromException
            }
        }

        _cts.Dispose();
    }
}
