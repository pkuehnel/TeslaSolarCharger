namespace TeslaSolarCharger.Client.Services;

public class SubscriptionDisposable : IDisposable
{
    private readonly Action _unsubscribeAction;

    private bool _disposed;
    public SubscriptionDisposable(Action unsubscribeAction)
    {
        _unsubscribeAction = unsubscribeAction ?? throw new ArgumentNullException(nameof(unsubscribeAction));
    }
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _unsubscribeAction();
    }
}
