using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Services;

public class BleAdapterGate : IBleAdapterGate
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public Task<bool> WaitAsync(TimeSpan timeout) => _semaphoreSlim.WaitAsync(timeout);

    public void Release() => _semaphoreSlim.Release();

    public string? HeldSessionVin { get; set; }
}
