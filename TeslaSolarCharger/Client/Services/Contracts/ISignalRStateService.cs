namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ISignalRStateService
{
    event Action? OnConnectionStateChanged;
    bool IsConnected { get; }
    Task InitializeAsync();
    Task<T?> GetStateAsync<T>(string dataType, string entityId = "") where T : class;
    Task Subscribe<T>(string dataType, Action<T> callback, string entityId = "") where T : class;
    Task SubscribeToTrigger(string dataType, Action callback, string entityId = "");
}
