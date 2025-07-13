using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Text.Json;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Client.Services;

public class SignalRStateService : ISignalRStateService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SignalRStateService> _logger;
    private readonly ConcurrentDictionary<string, object> _stateStore = new();
    private readonly Dictionary<string, List<Action<object>>> _subscribers = new();
    private readonly Dictionary<string, List<Action>> _triggerSubscribers = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public SignalRStateService(NavigationManager navigationManager, ILogger<SignalRStateService> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection != null)
            {
                return;
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_navigationManager.ToAbsoluteUri("/appStateHub"))
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<StateUpdateDto>(nameof(IAppStateClient.ReceiveStateUpdate), HandleStateUpdate);
            _hubConnection.On<string, string>(nameof(IAppStateClient.ReceiveInitialState), HandleInitialState);

            _hubConnection.Reconnecting += (error) =>
            {
                _logger.LogWarning(error, "SignalR connection lost, reconnecting...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation("SignalR reconnected with ID: {ConnectionId}", connectionId);
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<T?> GetStateAsync<T>(string dataType, string entityId = "") where T : class
    {
        var key = GetDataKey(dataType, entityId);

        if (_stateStore.TryGetValue(key, out var state) && state is T typedState)
        {
            return typedState;
        }

        // First time request, subscribe to this data type
        await SubscribeToDataTypeAsync(dataType);
        return default;
    }

    private static string GetDataKey(string dataType, string? entityId)
    {
        return string.IsNullOrEmpty(entityId) ? dataType : $"{dataType}:{entityId}";
    }

    public void Subscribe<T>(string dataType, Action<T> callback, string entityId = "") where T : class
    {
        var key = GetDataKey(dataType, entityId);

        if (!_subscribers.ContainsKey(key))
        {
            _subscribers[key] = new List<Action<object>>();
        }

        _subscribers[key].Add(obj =>
        {
            if (obj is T typedObj)
            {
                callback(typedObj);
            }
        });

        // If we already have state, invoke callback immediately
        if (_stateStore.TryGetValue(key, out var existingState) && existingState is T typedState)
        {
            callback(typedState);
        }
    }

    public void SubscribeToTrigger(string dataType, Action callback, string entityId = "")
    {
        var key = GetDataKey(dataType, entityId);

        if (!_triggerSubscribers.ContainsKey(key))
        {
            _triggerSubscribers[key] = new List<Action>();
        }

        _triggerSubscribers[key].Add(callback);

        // If we already have state, invoke callback immediately
        if (_stateStore.ContainsKey(key))
        {
            callback();
        }
    }

    private async Task SubscribeToDataTypeAsync(string dataType)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SubscribeToDataType", dataType);
        }
    }

    private void HandleStateUpdate(StateUpdateDto update)
    {
        var key = GetDataKey(update.DataType, update.EntityId);

        if (!_stateStore.TryGetValue(key, out var currentState))
        {
            _logger.LogWarning("Received update for unknown entity: {Key}", key);
            return;
        }

        // Apply changes to the current state
        var stateType = currentState.GetType();
        foreach (var change in update.ChangedProperties)
        {
            var property = stateType.GetProperty(change.Key);
            if (property != null && property.CanWrite)
            {
                var value = JsonSerializer.Deserialize(
                    JsonSerializer.Serialize(change.Value),
                    property.PropertyType);
                property.SetValue(currentState, value);
            }
        }

        // Notify subscribers
        NotifySubscribers(key, currentState);
    }

    private void HandleInitialState(string dataType, string jsonData)
    {
        try
        {
            var stateType = GetStateType(dataType);
            if (stateType == null)
            {
                _logger.LogWarning("Unknown data type: {DataType}", dataType);
                return;
            }

            var state = JsonSerializer.Deserialize(jsonData, stateType);
            if (state != null)
            {
                _stateStore[dataType] = state;
                NotifySubscribers(dataType, state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling initial state for {DataType}", dataType);
        }
    }

    private Type? GetStateType(string dataType)
    {
        return dataType switch
        {
            DataTypeConstants.PvValues => typeof(TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues.DtoPvValues),
            // Add more mappings as needed
            _ => null,
        };
    }

    private void NotifySubscribers(string key, object state)
    {
        if (_subscribers.TryGetValue(key, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(state);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in subscriber callback for {Key}", key);
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        _connectionLock.Dispose();
    }
}
