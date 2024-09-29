namespace TeslaSolarCharger.Client.Helper;

public class PeriodicTaskHelper : IDisposable
{
    private CancellationTokenSource? _cts;

    public void Start(Func<Task> action, TimeSpan interval)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _ = RunPeriodicAsync(action, interval, _cts.Token);
    }

    private async Task RunPeriodicAsync(Func<Task> action, TimeSpan interval, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await action();
                await Task.Delay(interval, token);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
