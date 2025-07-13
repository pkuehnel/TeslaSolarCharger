using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
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
    private readonly ConcurrentDictionary<string, List<Action<object>>> _subscribers = new();
    private readonly ConcurrentDictionary<string, List<Action>> _triggerSubscribers = new();
    private readonly ConcurrentDictionary<string, bool> _subscribedDataTypes = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);

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

            _hubConnection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("SignalR reconnected with ID: {ConnectionId}", connectionId);

                // Re-subscribe to all data types after reconnection
                await ResubscribeToAllDataTypes();
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

        // Subscribe to this data type if not already subscribed
        await EnsureSubscribedToDataType(dataType);
        return default;
    }

    public async void Subscribe<T>(string dataType, Action<T> callback, string entityId = "") where T : class
    {
        var key = GetDataKey(dataType, entityId);

        // Add the callback
        _subscribers.AddOrUpdate(key,
            _ => new List<Action<object>> { obj => { if (obj is T typedObj) callback(typedObj); } },
            (_, list) =>
            {
                list.Add(obj => { if (obj is T typedObj) callback(typedObj); });
                return list;
            });

        // Subscribe to the data type if not already subscribed
        await EnsureSubscribedToDataType(dataType);

        // If we already have state, invoke callback immediately
        if (_stateStore.TryGetValue(key, out var existingState) && existingState is T typedState)
        {
            try
            {
                callback(typedState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking initial callback for {Key}", key);
            }
        }
    }

    public async void SubscribeToTrigger(string dataType, Action callback, string entityId = "")
    {
        var key = GetDataKey(dataType, entityId);

        // Add the callback
        _triggerSubscribers.AddOrUpdate(key,
            _ => new List<Action> { callback },
            (_, list) =>
            {
                list.Add(callback);
                return list;
            });

        // Subscribe to the data type if not already subscribed
        await EnsureSubscribedToDataType(dataType);

        // For triggers, we might want to invoke immediately if we've seen this trigger before
        // This depends on your business logic - you might not want this behavior for triggers
    }

    private async Task EnsureSubscribedToDataType(string dataType)
    {
        // Check if we've already subscribed to this data type
        if (_subscribedDataTypes.ContainsKey(dataType))
        {
            return;
        }

        await _subscriptionLock.WaitAsync();
        try
        {
            // Double-check after acquiring the lock
            if (_subscribedDataTypes.ContainsKey(dataType))
            {
                return;
            }

            // Subscribe to the data type
            await SubscribeToDataTypeAsync(dataType);

            // Mark as subscribed
            _subscribedDataTypes[dataType] = true;
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    private async Task SubscribeToDataTypeAsync(string dataType)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("SubscribeToDataType", dataType);
                _logger.LogDebug("Subscribed to data type: {DataType}", dataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to data type: {DataType}", dataType);
                throw;
            }
        }
        else
        {
            _logger.LogWarning("Cannot subscribe to {DataType} - SignalR connection not established", dataType);
        }
    }

    private async Task ResubscribeToAllDataTypes()
    {
        foreach (var dataType in _subscribedDataTypes.Keys)
        {
            try
            {
                await SubscribeToDataTypeAsync(dataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-subscribe to data type: {DataType}", dataType);
            }
        }
    }

    private static string GetDataKey(string dataType, string? entityId)
    {
        return string.IsNullOrEmpty(entityId) ? dataType : $"{dataType}:{entityId}";
    }

    private void HandleStateUpdate(StateUpdateDto update)
    {
        _logger.LogTrace("Received state update {@update}", update);
        var key = GetDataKey(update.DataType, update.EntityId);

        // Check if this is a trigger update (no state to update)
        if (_triggerSubscribers.ContainsKey(key))
        {
            NotifyTriggerSubscribers(key);
        }

        // Check if we have state to update
        if (_stateStore.TryGetValue(key, out var currentState))
        {
            // Apply changes to the current state
            var stateType = currentState.GetType();
            foreach (var change in update.ChangedProperties)
            {
                var property = stateType.GetProperty(change.Key);
                if (property != null && property.CanWrite)
                {
                    try
                    {
                        var value = JsonSerializer.Deserialize(
                            JsonSerializer.Serialize(change.Value),
                            property.PropertyType);
                        property.SetValue(currentState, value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to update property {Property} for {Key}", change.Key, key);
                    }
                }
            }

            // Notify subscribers with updated state
            NotifySubscribers(key, currentState);
        }
        else if (!_triggerSubscribers.ContainsKey(key))
        {
            // Only log warning if this isn't a trigger
            _logger.LogDebug("Received update for entity without local state: {Key}", key);
        }
    }

    private void HandleInitialState(string key, string jsonData)
    {
        _logger.LogTrace("Received initial state for {key}: {data}", key, jsonData);
        try
        {
            var dataType = key.Split(":").First();
            var stateType = GetStateType(dataType);
            if (stateType == null)
            {
                _logger.LogWarning("Unknown data type: {DataType}", dataType);
                return;
            }

            var state = JsonSerializer.Deserialize(jsonData, stateType);
            if (state != null)
            {
                _stateStore[key] = state;
                NotifySubscribers(key, state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling initial state for {DataType}", key);
        }
    }

    private Type? GetStateType(string dataType)
    {
        return dataType switch
        {
            DataTypeConstants.PvValues => typeof(TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues.DtoPvValues),
            DataTypeConstants.LoadPointOverviewValues => typeof(TeslaSolarCharger.Shared.Dtos.Home.DtoLoadPointWithCurrentChargingValues),
            // Add more mappings as needed
            _ => null,
        };
    }

    private void NotifySubscribers(string key, object state)
    {
        if (_subscribers.TryGetValue(key, out var callbacks))
        {
            foreach (var callback in callbacks.ToList()) // ToList to avoid modification during enumeration
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

    private void NotifyTriggerSubscribers(string key)
    {
        if (_triggerSubscribers.TryGetValue(key, out var callbacks))
        {
            foreach (var callback in callbacks.ToList()) // ToList to avoid modification during enumeration
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in trigger subscriber callback for {Key}", key);
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
        _subscriptionLock.Dispose();
    }
}
