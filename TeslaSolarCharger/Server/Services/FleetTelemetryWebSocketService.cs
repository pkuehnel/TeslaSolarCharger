using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Enums;
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
    private readonly ISettings _settings;

    private HubConnection? _hubConnection;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DtoFleetTelemetryClient> _subscribedVins = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // We only need the base URL, SignalR handles the rest
    private string? _currentApiUrl;

    // Cache for performance
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _cachedCarIds = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, HomeDetectionVia> _cachedHomeDetectionVia = new();

    public FleetTelemetryWebSocketService(
        ILogger<FleetTelemetryWebSocketService> logger,
        IServiceProvider serviceProvider,
        ISettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
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

        if (_subscribedVins.TryGetValue(vin, out var client))
        {
            return client.ConnectedSince;
        }

        return default;
    }

    public async Task ReconnectWebSocketsForEnabledCars()
    {
        _logger.LogTrace("{method}", nameof(ReconnectWebSocketsForEnabledCars));
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var backendApiService = scope.ServiceProvider.GetRequiredService<IBackendApiService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();

        var cars = await context.Cars
            .Where(c => c.UseFleetTelemetry
                        && (c.ShouldBeManaged == true)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotWorking)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.OpenedLinkButNotTested)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotConfigured)
                        && (c.IsFleetTelemetryHardwareIncompatible == false))
            .Select(c => new { c.Vin, c.IncludeTrackingRelevantFields })
            .ToListAsync();

        var isBaseAppLicensed = await backendApiService.IsBaseAppLicensed(true).ConfigureAwait(false);
        if (cars.Any() && (isBaseAppLicensed.Data != true))
        {
            _logger.LogWarning("Base App is not licensed, do not connect to Fleet Telemetry");
            return;
        }

        var vinsToSubscribe = new HashSet<string>();
        foreach (var car in cars)
        {
            if (string.IsNullOrEmpty(car.Vin)) continue;

            if (car.IncludeTrackingRelevantFields && (!await backendApiService.IsFleetApiLicensed(car.Vin, true)))
            {
                _logger.LogWarning("Car {vin} is not licensed for Fleet API, do not connect as IncludeTrackingRelevant fields is enabled", car.Vin);
                continue;
            }
            vinsToSubscribe.Add(car.Vin);
        }

        var apiUrl = configurationWrapper.FleetTelemetryApiUrl();
        // If there are no cars to connect to, we can just stop the connection if it exists
        if (vinsToSubscribe.Count == 0)
        {
            if (_hubConnection != null)
            {
                await StopConnectionAsync();
            }
            return;
        }

        await EnsureConnectionAsync(apiUrl);

        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            var currentSubscribedVins = _subscribedVins.Keys.ToList();
            var vinsToUnsubscribe = currentSubscribedVins.Except(vinsToSubscribe).ToList();
            var vinsToAddNew = vinsToSubscribe.Except(currentSubscribedVins).ToList();

            foreach (var vin in vinsToUnsubscribe)
            {
                try
                {
                    _logger.LogInformation("Unsubscribing from VIN {vin}", vin);
                    await _hubConnection.InvokeAsync("UnsubscribeFromVin", vin);
                    _subscribedVins.TryRemove(vin, out _);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from VIN {vin}", vin);
                }
            }

            foreach (var vin in vinsToAddNew)
            {
                try
                {
                    _logger.LogInformation("Subscribing to VIN {vin}", vin);
                    await _hubConnection.InvokeAsync("SubscribeToVin", vin);
                    _subscribedVins.AddOrUpdate(vin, new DtoFleetTelemetryClient
                    {
                        Vin = vin,
                        ConnectedSince = dateTimeProvider.DateTimeOffSetUtcNow()
                    }, (key, existing) => existing);

                    await UpdateCarOnlineStateAsync(vin);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to VIN {vin}", vin);
                }
            }
        }
    }

    private async Task UpdateCarOnlineStateAsync(string vin)
    {
        using var scope = _serviceProvider.CreateScope();
        var teslaFleetApiService = scope.ServiceProvider.GetRequiredService<ITeslaFleetApiService>();
        var car = _settings.Cars.FirstOrDefault(c => c.Vin == vin);
        if (car != default)
        {
            await teslaFleetApiService.RefreshVehicleOnlineState(car);
        }

        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var carId = await context.Cars
            .Where(c => c.Vin == vin)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (carId > 0)
        {
            var loadPointManagementService = scope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
            await loadPointManagementService.CarStateChanged(carId).ConfigureAwait(false);
        }
    }

    private async Task EnsureConnectionAsync(string apiUrl)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection != null && _currentApiUrl != apiUrl)
            {
                await StopConnectionAsync();
            }

            if (_hubConnection == null)
            {
                _currentApiUrl = apiUrl;

                var hubUrl = apiUrl;
                if (hubUrl.StartsWith("wss://"))
                {
                    hubUrl = hubUrl.Replace("wss://", "https://");
                }
                else if (hubUrl.StartsWith("ws://"))
                {
                    hubUrl = hubUrl.Replace("ws://", "http://");
                }

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.AccessTokenProvider = async () =>
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
                            var authToken = await context.BackendTokens.AsNoTracking().SingleOrDefaultAsync();
                            return authToken?.AccessToken;
                        };
                    })
                    .AddNewtonsoftJsonProtocol(options =>
                    {
                        options.PayloadSerializerSettings.Converters.Add(new EnumDefaultConverter<CarValueType>(CarValueType.Unknown));
                    })
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<string, List<DtoTscFleetTelemetryMessage>>("ReceiveTelemetryData", async (vin, messages) =>
                {
                    await ProcessTelemetryMessagesAsync(vin, messages);
                });

                _hubConnection.Reconnected += async (connectionId) =>
                {
                    _logger.LogInformation("SignalR reconnected to Fleet Hub. Re-subscribing to VINs.");
                    var vinsToResubscribe = _subscribedVins.Keys.ToList();
                    _subscribedVins.Clear(); // Clear so we can re-add them with fresh connection times

                    using var scope = _serviceProvider.CreateScope();
                    var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

                    foreach (var vin in vinsToResubscribe)
                    {
                        try
                        {
                            _logger.LogInformation("Re-subscribing to VIN {vin}", vin);
                            await _hubConnection.InvokeAsync("SubscribeToVin", vin);
                            _subscribedVins.AddOrUpdate(vin, new DtoFleetTelemetryClient
                            {
                                Vin = vin,
                                ConnectedSince = dateTimeProvider.DateTimeOffSetUtcNow()
                            }, (key, existing) => existing);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to re-subscribe to VIN {vin}", vin);
                        }
                    }
                };

                _hubConnection.Closed += (error) =>
                {
                    _logger.LogWarning(error, "SignalR connection to Fleet Hub closed.");
                    return Task.CompletedTask;
                };
            }

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                _logger.LogInformation("Starting SignalR connection to {url}", apiUrl);
                await _hubConnection.StartAsync();
                _logger.LogInformation("Successfully connected to Fleet Hub SignalR.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting SignalR connection to {url}", apiUrl);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task StopConnectionAsync()
    {
        if (_hubConnection != null)
        {
            _logger.LogInformation("Stopping existing SignalR connection to Fleet Hub.");
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            _subscribedVins.Clear();
        }
    }

    private async Task ProcessTelemetryMessagesAsync(string vin, List<DtoTscFleetTelemetryMessage> messages)
    {
        _logger.LogTrace("Received {count} fleet telemetry messages for car {vin}", messages?.Count ?? 0, vin);

        if (messages == null || messages.Count == 0)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        if (!_cachedCarIds.TryGetValue(vin, out var carId))
        {
            carId = await context.Cars
                .Where(c => c.Vin == vin)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (carId > 0)
            {
                _cachedCarIds.TryAdd(vin, carId);
            }
        }

        if (carId == 0)
        {
            _logger.LogWarning("Received telemetry for VIN {vin} but car was not found in database", vin);
            return;
        }

        var scopedSettings = scope.ServiceProvider.GetRequiredService<ISettings>();
        var settingsCar = scopedSettings.Cars.FirstOrDefault(c => c.Vin == vin);
        if (settingsCar == null)
        {
            return;
        }

        bool batchHasAnyUpdateProperty = false;

        HomeDetectionVia? homeDetectionVia = null;
        if (messages.Any(m => m.Type == CarValueType.LocatedAtHome || m.Type == CarValueType.LocatedAtWork || m.Type == CarValueType.LocatedAtFavorite))
        {
             if (!_cachedHomeDetectionVia.TryGetValue(carId, out var cachedHomeVia))
             {
                 var dbHomeVia = await context.Cars
                    .Where(c => c.Id == settingsCar.Id)
                    .Select(c => c.HomeDetectionVia)
                    .FirstOrDefaultAsync();

                 _cachedHomeDetectionVia.TryAdd(carId, dbHomeVia);
                 homeDetectionVia = dbHomeVia;
             }
             else
             {
                 homeDetectionVia = cachedHomeVia;
             }
        }

        foreach (var message in messages)
        {
            // Error messages are no longer sent as part of the normal DtoTscFleetTelemetryMessage stream,
            // but if there's any special handling needed, it should be done here.

            if (configurationWrapper.LogLocationData() ||
                (message.Type != CarValueType.Latitude && message.Type != CarValueType.Longitude))
            {
                _logger.LogDebug("Save fleet telemetry message {@message}", message);
            }
            else
            {
                _logger.LogDebug("Save location message for car {carId}", carId);
            }

            var carValueLog = new CarValueLog
            {
                CarId = carId,
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

            bool shouldUpdateProperty = false;

            if (configurationWrapper.GetVehicleDataFromTesla())
            {
                switch (message.Type)
                {
                    case CarValueType.ChargeAmps:
                    case CarValueType.ChargeCurrentRequest:
                    case CarValueType.IsPluggedIn:
                    case CarValueType.ModuleTempMin:
                    case CarValueType.ModuleTempMax:
                    case CarValueType.IsCharging:
                    case CarValueType.ChargerPilotCurrent:
                    case CarValueType.Longitude:
                    case CarValueType.Latitude:
                    case CarValueType.StateOfCharge:
                    case CarValueType.StateOfChargeLimit:
                    case CarValueType.ChargerPhases:
                    case CarValueType.ChargerVoltage:
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
                    batchHasAnyUpdateProperty = true;
                    var carPropertyUpdateHelper = scope.ServiceProvider.GetRequiredService<ICarPropertyUpdateHelper>();
                    carPropertyUpdateHelper.UpdateDtoCarProperty(settingsCar, carValueLog);
                }
            }
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        if (batchHasAnyUpdateProperty)
        {
            _ = Task.Run(async () =>
            {
                using var innerScope = _serviceProvider.CreateScope();
                var loadPointManagementService = innerScope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
                try
                {
                    await loadPointManagementService.CarStateChanged(settingsCar.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing CarStateChanged for car ID {carId}", settingsCar.Id);
                }
            });
        }
    }

    private async Task SetCarToFleetTelemetryHardwareIncompatible(string vin)
    {
        _logger.LogTrace("{method}({vin})", nameof(SetCarToFleetTelemetryHardwareIncompatible), vin);
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var car = context.Cars.FirstOrDefault(c => c.Vin == vin);
        if (car == default)
        {
            return;
        }
        car.IsFleetTelemetryHardwareIncompatible = true;
        await context.SaveChangesAsync();
    }

    private async Task DisableFleetTelemetryForCar(string vin)
    {
        _logger.LogTrace("{method}({vin})", nameof(DisableFleetTelemetryForCar), vin);
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var car = context.Cars.FirstOrDefault(c => c.Vin == vin);
        if (car == default)
        {
            return;
        }
        car.UseFleetTelemetry = false;
        car.IncludeTrackingRelevantFields = false;
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopConnectionAsync();
        _connectionLock.Dispose();
    }
}
