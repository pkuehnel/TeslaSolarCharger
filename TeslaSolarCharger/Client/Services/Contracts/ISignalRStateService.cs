namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ISignalRStateService
{
    bool IsConnected { get; }
    Task InitializeAsync();
    Task<T?> GetStateAsync<T>(string dataType, string entityId = "") where T : class;
    void Subscribe<T>(string dataType, Action<T> callback, string entityId = "") where T : class;
    void SubscribeToTrigger(string dataType, Action callback, string entityId = "");
}
