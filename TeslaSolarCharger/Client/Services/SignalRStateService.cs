using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Client.Services;

public class SignalRStateService : ISignalRStateService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SignalRStateService> _logger;
    private readonly Dictionary<string, object> _stateStore = new();
    private readonly Dictionary<string, List<Action<object>>> _subscribers = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public SignalRStateService(NavigationManager navigationManager, ILogger<SignalRStateService> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(), },
        };
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
        var key = string.IsNullOrEmpty(entityId) ? dataType : $"{dataType}:{entityId}";

        if (_stateStore.TryGetValue(key, out var state) && state is T typedState)
        {
            return typedState;
        }

        // First time request, subscribe to this data type
        await SubscribeToDataTypeAsync(dataType);
        return default;
    }

    public void Subscribe<T>(string dataType, Action<T> callback, string entityId = "") where T : class
    {
        var key = string.IsNullOrEmpty(entityId) ? dataType : $"{dataType}:{entityId}";

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

    private async Task SubscribeToDataTypeAsync(string dataType)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("SubscribeToDataType", dataType);
        }
    }

    private void HandleStateUpdate(StateUpdateDto update)
    {
        var key = string.IsNullOrEmpty(update.EntityId) ? update.DataType : $"{update.DataType}:{update.EntityId}";

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

            var state = JsonSerializer.Deserialize(jsonData, stateType, _jsonOptions);
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
        _connectionLock?.Dispose();
    }
}
