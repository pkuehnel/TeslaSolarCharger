namespace TeslaSolarCharger.Client.Services;

public class SubscriptionDisposable : IDisposable
{
    private readonly Action _unsubscribeAction;

    public SubscriptionDisposable(Action unsubscribeAction)
    {
        _unsubscribeAction = unsubscribeAction;
    }

    public void Dispose()
    {
        _unsubscribeAction();
    }
}
