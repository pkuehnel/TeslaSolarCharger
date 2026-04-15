using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryWebSocketService : IFleetTelemetryWebSocketService, IAsyncDisposable
{
    private readonly ILogger<FleetTelemetryWebSocketService> _logger;
    private readonly IServiceProvider _serviceProvider;

    private HubConnection? _hubConnection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Track subscribed VINs and when they were connected
    private readonly ConcurrentDictionary<string, DateTimeOffset> _subscribedVins = new();

    public FleetTelemetryWebSocketService(
        ILogger<FleetTelemetryWebSocketService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public bool IsClientConnected(string vin)
    {
        _logger.LogTrace("{method}({vin})", nameof(IsClientConnected), vin);
        return _hubConnection?.State == HubConnectionState.Connected && _subscribedVins.ContainsKey(vin);
    }

    public DateTimeOffset? ClientConnectedSince(string vin)
    {
        _logger.LogTrace("{method}({vin})", nameof(ClientConnectedSince), vin);
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return default;
        }

        if (_subscribedVins.TryGetValue(vin, out var connectedSince))
        {
            return connectedSince;
        }

        return default;
    }

    public async Task ReconnectWebSocketsForEnabledCars()
    {
        _logger.LogTrace("{method}", nameof(ReconnectWebSocketsForEnabledCars));
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var backendApiService = scope.ServiceProvider.GetRequiredService<IBackendApiService>();

        var cars = await context.Cars
            .Where(c => c.ShouldBeManaged == true
                && (c.UseFleetTelemetry || (c.CarType != CarType.Tesla)))
            .Select(c => new { c.Vin, c.IncludeTrackingRelevantFields })
            .ToListAsync();

        var isBaseAppLicensed = await backendApiService.IsBaseAppLicensed(true).ConfigureAwait(false);
        if (cars.Any() && (isBaseAppLicensed.Data != true))
        {
            _logger.LogWarning("Base App is not licensed, do not connect to Fleet Telemetry");
            return;
        }

        var validVins = new HashSet<string>();

        foreach (var car in cars)
        {
            if (string.IsNullOrEmpty(car.Vin))
            {
                continue;
            }

            if (car.IncludeTrackingRelevantFields && (!await backendApiService.IsFleetApiLicensed(car.Vin, true)))
            {
                _logger.LogWarning("Car {vin} is not licensed for Fleet API, do not connect as IncludeTrackingRelevant fields is enabled", car.Vin);
                continue;
            }

            validVins.Add(car.Vin);
        }

        await ManageSignalRConnectionAsync(validVins).ConfigureAwait(false);
    }

    private async Task ManageSignalRConnectionAsync(HashSet<string> targetVins)
    {
        _logger.LogTrace("{method}({@vins})", nameof(ManageSignalRConnectionAsync), targetVins);
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Start connection if needed
            if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
            {
                if (targetVins.Any())
                {
                    var success = await InitializeAndStartConnectionAsync().ConfigureAwait(false);
                    if (!success)
                    {
                        return; // Failed to connect, will try again on next job run
                    }
                }
                else
                {
                    return; // No VINs to track, and not connected
                }
            }

            // If we are still not connected (e.g., Reconnecting), we just update the _subscribedVins list
            // The Reconnected event will handle resubscribing if needed.
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                // Unsubscribe from VINs we no longer need (update internal list only)
                var vinsToUnsubscribeReconnecting = _subscribedVins.Keys.Except(targetVins).ToList();
                foreach (var vin in vinsToUnsubscribeReconnecting)
                {
                    _subscribedVins.TryRemove(vin, out _);
                }
                return;
            }

            // Unsubscribe from VINs we no longer need
            var vinsToUnsubscribe = _subscribedVins.Keys.Except(targetVins).ToList();
            foreach (var vin in vinsToUnsubscribe)
            {
                try
                {
                    _logger.LogInformation("Unsubscribing from VIN {vin}", vin);
                    await _hubConnection.InvokeAsync("UnsubscribeFromVin", vin).ConfigureAwait(false);
                    _subscribedVins.TryRemove(vin, out _);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from VIN {vin}", vin);
                }
            }

            using var scope = _serviceProvider.CreateScope();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            // Subscribe to new VINs
            var vinsToSubscribe = targetVins.Except(_subscribedVins.Keys).ToList();
            foreach (var vin in vinsToSubscribe)
            {
                try
                {
                    _logger.LogInformation("Subscribing to VIN {vin}", vin);
                    await _hubConnection.InvokeAsync("SubscribeToVin", vin).ConfigureAwait(false);
                    _subscribedVins[vin] = dateTimeProvider.DateTimeOffSetUtcNow();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to VIN {vin}", vin);
                }
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<bool> InitializeAndStartConnectionAsync()
    {
        _logger.LogTrace("{method}()", nameof(InitializeAndStartConnectionAsync));
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var authToken = await context.BackendTokens.AsNoTracking().SingleOrDefaultAsync();
        if (authToken == default || authToken.ExpiresAtUtc < dateTimeProvider.DateTimeOffSetUtcNow())
        {
            _logger.LogError("Can not connect to SignalR: No unexpired token found");
            return false;
        }

        var url = configurationWrapper.FleetTelemetryApiUrl();

        _logger.LogInformation("Initializing SignalR connection to {url}", url);

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
        _subscribedVins.Clear();

        var jsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new EnumDefaultConverter<CarValueType>(CarValueType.Unknown) },
        };

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    using var tokenScope = _serviceProvider.CreateScope();
                    var tokenContext = tokenScope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
                    var tokenDateTimeProvider = tokenScope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                    var token = await tokenContext.BackendTokens.AsNoTracking().SingleOrDefaultAsync();
                    if (token == default || token.ExpiresAtUtc < tokenDateTimeProvider.DateTimeOffSetUtcNow())
                    {
                        return null;
                    }
                    return token.AccessToken;
                };
            })
            .WithAutomaticReconnect(new JitteredExponentialBackoffRetryPolicy())
            .AddNewtonsoftJsonProtocol(options =>
            {
                options.PayloadSerializerSettings = jsonSerializerSettings;
            })
            .Build();

        _hubConnection.On<string, List<DtoTscFleetTelemetryMessage>>("ReceiveTelemetryData", async (vin, messages) =>
        {
            try
            {
                using var innerScope = _serviceProvider.CreateScope();
                var innerConfig = innerScope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
                await HandleFleetTelemetryMessages(vin, messages, innerConfig, innerScope).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ReceiveTelemetryData for VIN {vin}", vin);
            }
        });

        _hubConnection.Reconnected += async (_) =>
        {
            _logger.LogInformation("SignalR Reconnected. Resubscribing to active VINs.");
            await ResubscribeAllVinsAsync().ConfigureAwait(false);
        };

        _hubConnection.Closed += (error) =>
        {
            _logger.LogWarning(error, "SignalR connection closed.");
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync().ConfigureAwait(false);
            _logger.LogInformation("SignalR connection started successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            return false;
        }
    }

    private class JitteredExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private const double MinCapSeconds = 240.0; // 4 minutes
        private const double MaxCapSeconds = 300.0; // 5 minutes

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            // 1. Protect against Math.Pow overflow for extremely long-running disconnections
            if (retryContext.PreviousRetryCount > 12)
            {
                return GetRandomCappedDelay();
            }

            // 2. Calculate base exponential backoff: 2^retryCount
            // Attempt 0 = 1s, Attempt 1 = 2s, Attempt 2 = 4s... Attempt 8 = 256s
            var baseDelaySeconds = Math.Pow(2, retryContext.PreviousRetryCount);

            // 3. If the calculated delay reaches or exceeds our 4-minute minimum cap, 
            // switch to the 4-5 minute random window.
            if (baseDelaySeconds >= MinCapSeconds)
            {
                return GetRandomCappedDelay();
            }

            // 4. Add Jitter to the exponential backoff (e.g., +/- 20% randomness).
            // If base is 4s, the delay will be randomly chosen between 3.2s and 4.8s.
            var jitterMultiplier = 0.8 + (Random.Shared.NextDouble() * 0.4);
            var jitteredDelaySeconds = baseDelaySeconds * jitterMultiplier;

            return TimeSpan.FromSeconds(jitteredDelaySeconds);
        }

        private TimeSpan GetRandomCappedDelay()
        {
            // Generates a random value between 240 seconds (4 mins) and 300 seconds (5 mins)
            var randomSeconds = MinCapSeconds + (Random.Shared.NextDouble() * (MaxCapSeconds - MinCapSeconds));
            return TimeSpan.FromSeconds(randomSeconds);
        }
    }

    private async Task ResubscribeAllVinsAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                return;
            }

            var vinsToResubscribe = _subscribedVins.Keys.ToList();

            using var scope = _serviceProvider.CreateScope();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            foreach (var vin in vinsToResubscribe)
            {
                try
                {
                    _logger.LogInformation("Resubscribing to VIN {vin}", vin);
                    await _hubConnection.InvokeAsync("SubscribeToVin", vin).ConfigureAwait(false);
                    _subscribedVins[vin] = dateTimeProvider.DateTimeOffSetUtcNow();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resubscribe to VIN {vin}", vin);
                    // Remove from tracked list if we failed, so the job will try again
                    _subscribedVins.TryRemove(vin, out _);
                }
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task HandleFleetTelemetryMessages(string vin, List<DtoTscFleetTelemetryMessage> messages, 
        IConfigurationWrapper configurationWrapper, IServiceScope scope)
    {
        _logger.LogTrace("Handle {count} messages for VIN {vin}", messages.Count, vin);
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var scopedSettings = scope.ServiceProvider.GetRequiredService<ISettings>();

        var settingsCar = scopedSettings.Cars.FirstOrDefault(c => c.Vin == vin);
        if (settingsCar == default)
        {
            _logger.LogWarning("Received telemetry for untracked VIN {vin}", vin);
            return;
        }

        HomeDetectionVia? homeDetectionVia = null;
        var anyHomeDetectionRelevantMessage = messages
            .Any(m => m.Type == CarValueType.LocatedAtHome
                      || m.Type == CarValueType.LocatedAtWork
                      || m.Type == CarValueType.LocatedAtFavorite);
        if (anyHomeDetectionRelevantMessage)
        {
            homeDetectionVia = await context.Cars
                .Where(c => c.Id == settingsCar.Id)
                .Select(c => c.HomeDetectionVia)
                .FirstAsync();
        }

        foreach (var message in messages)
        {
            if (configurationWrapper.LogLocationData() ||
            (message.Type != CarValueType.Latitude && message.Type != CarValueType.Longitude))
            {
                _logger.LogDebug("Save fleet telemetry message {@message}", message);
            }
            else
            {
                _logger.LogDebug("Save location message for car {vin}", vin);
            }
            
            var carValueLog = new CarValueLog
            {
                CarId = settingsCar.Id,
                Type = message.Type,
                DoubleValue = message.DoubleValue,
                IntValue = message.IntValue,
                StringValue = message.StringValue,
                UnknownValue = message.UnknownValue,
                BooleanValue = message.BooleanValue,
                InvalidValue = message.InvalidValue,
                Timestamp = message.TimeStamp.UtcDateTime,
                Source = CarValueSource.FleetTelemetry,
            };
            context.CarValueLogs.Add(carValueLog);
            if (configurationWrapper.GetVehicleDataFromTesla())
            {
                var shouldUpdateProperty = false;
                
                switch (message.Type)
                {
                    case CarValueType.ChargeAmps:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ChargeCurrentRequest:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.IsPluggedIn:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ModuleTempMin:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ModuleTempMax:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.IsCharging:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ChargerPilotCurrent:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.Longitude:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.Latitude:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.StateOfCharge:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.StateOfChargeLimit:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ChargerPhases:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.ChargerVoltage:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.VehicleName:
                        shouldUpdateProperty = true;
                        break;
                    case CarValueType.AsleepOrOffline:
                        settingsCar.IsOnline.Update(new DateTimeOffset(carValueLog.Timestamp, TimeSpan.Zero),
                            carValueLog.BooleanValue == false);
                        break;
                    case CarValueType.LocatedAtHome:
                        if (homeDetectionVia == HomeDetectionVia.LocatedAtHome)
                        {
                            settingsCar.IsHomeGeofence.Update(new DateTimeOffset(carValueLog.Timestamp, TimeSpan.Zero),
                                carValueLog.BooleanValue == true);
                        }
                        break;
                    case CarValueType.LocatedAtWork:
                        if (homeDetectionVia == HomeDetectionVia.LocatedAtWork)
                        {
                            settingsCar.IsHomeGeofence.Update(new DateTimeOffset(carValueLog.Timestamp, TimeSpan.Zero),
                                carValueLog.BooleanValue == true);
                        }
                        break;
                    case CarValueType.LocatedAtFavorite:
                        if (homeDetectionVia == HomeDetectionVia.LocatedAtFavorite)
                        {
                            settingsCar.IsHomeGeofence.Update(new DateTimeOffset(carValueLog.Timestamp, TimeSpan.Zero),
                                carValueLog.BooleanValue == true);
                        }
                        break;
                }

                if (shouldUpdateProperty)
                {
                    var carPropertyUpdateHelper = scope.ServiceProvider.GetRequiredService<ICarPropertyUpdateHelper>();
                    carPropertyUpdateHelper.UpdateDtoCarProperty(settingsCar, carValueLog);
                }
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
        var loadPointManagementService = scope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
        try
        {
            await loadPointManagementService.CarStateChanged(settingsCar.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing CarStateChanged for car ID {carId}", settingsCar.Id);
        }
    }

    internal DtoTscFleetTelemetryMessage? DeserializeFleetTelemetryMessage(string jsonMessage)
    {
        var jsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new EnumDefaultConverter<CarValueType>(CarValueType.Unknown), },
        };
        var message = JsonConvert.DeserializeObject<DtoTscFleetTelemetryMessage>(jsonMessage, jsonSerializerSettings);
        return message;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        }
        _connectionLock.Dispose();
    }
}
