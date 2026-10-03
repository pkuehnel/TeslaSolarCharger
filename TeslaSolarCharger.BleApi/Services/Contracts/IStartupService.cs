namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IStartupService
{
    Task UpdateRequestsAllowed();

    /// <summary>
    /// Makes sure the container has its key before the first request, so the BLE worker can start and listen for
    /// cars before any of them is paired. A failure is logged, not thrown: pairing tries again.
    /// </summary>
    Task EnsureKeyPair();
}