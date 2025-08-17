using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;
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

public class FleetTelemetryWebSocketService(
    ILogger<FleetTelemetryWebSocketService> logger,
    IServiceProvider serviceProvider) : IFleetTelemetryWebSocketService
{
    private readonly TimeSpan _heartbeatsendTimeout = TimeSpan.FromSeconds(5);

    private List<DtoFleetTelemetryWebSocketClients> Clients { get; set; } = new();

    public bool IsClientConnected(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(IsClientConnected), vin);
        return Clients.Any(c => c.Vin == vin && c.WebSocketClient.State == WebSocketState.Open);
    }

    public DateTimeOffset? ClientConnectedSince(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(ClientConnectedSince), vin);
        var client = Clients.FirstOrDefault(c => c.Vin == vin);
        if (client == default)
        {
            return default;
        }
        if (client.WebSocketClient.State != WebSocketState.Open)
        {
            return default;
        }
        return client.ConnectedSince;
    }

    public async Task ReconnectWebSocketsForEnabledCars()
    {
        logger.LogTrace("{method}", nameof(ReconnectWebSocketsForEnabledCars));
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var backendApiService = scope.ServiceProvider.GetRequiredService<IBackendApiService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var cars = await context.Cars
            .Where(c => c.UseFleetTelemetry
                        && (c.ShouldBeManaged == true)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotWorking)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.OpenedLinkButNotTested)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotConfigured)
                        && (c.IsFleetTelemetryHardwareIncompatible == false))
            .Select(c => new { c.Vin, c.IncludeTrackingRelevantFields, })
            .ToListAsync();
        var isBaseAppLicensed = await backendApiService.IsBaseAppLicensed(true).ConfigureAwait(false);
        if (cars.Any() && (isBaseAppLicensed.Data != true))
        {
            logger.LogWarning("Base App is not licensed, do not connect to Fleet Telemetry");
            return;
        }
        var bytesToSend = Encoding.UTF8.GetBytes("Heartbeat");
        foreach (var car in cars)
        {
            if (string.IsNullOrEmpty(car.Vin))
            {
                continue;
            }

            if (car.IncludeTrackingRelevantFields && (!await backendApiService.IsFleetApiLicensed(car.Vin, true)))
            {
                logger.LogWarning("Car {vin} is not licensed for Fleet API, do not connect as IncludeTrackingRelevant fields is enabled", car.Vin);
                continue;
            }
            var existingClient = Clients.FirstOrDefault(c => c.Vin == car.Vin);
            if (existingClient != default)
            {
                var currentTime = dateTimeProvider.DateTimeOffSetUtcNow();
                //When intervall is changed, change it also in the server WebSocketConnectionHandlingService.SendHeartbeatsTask
                var serverSideHeartbeatIntervall = TimeSpan.FromSeconds(54);
                var additionalIntervallbuffer = TimeSpan.FromSeconds(30);
                var maxLastHeartbeatAge = serverSideHeartbeatIntervall + additionalIntervallbuffer;
                var earliestPossibleLastHeartbeat = currentTime - maxLastHeartbeatAge;
                if ((existingClient.WebSocketClient.State == WebSocketState.Open) && (existingClient.LastReceivedHeartbeat > earliestPossibleLastHeartbeat))
                {
                    var segment = new ArraySegment<byte>(bytesToSend);
                    try
                    {
                        logger.LogDebug("Sending Heartbeat to websocket client for car {vin}", existingClient.Vin);
                        await existingClient.WebSocketClient.SendAsync(segment, WebSocketMessageType.Text, true,
                            new CancellationTokenSource(_heartbeatsendTimeout).Token);
                        logger.LogDebug("Heartbeat to websocket client for car {vin} sent", existingClient.Vin);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending heartbeat for car {vin}", car);
                        existingClient.WebSocketClient.Dispose();
                        Clients.Remove(existingClient);
                    }

                    continue;
                }

                logger.LogInformation("Websocket Client State for car {vin} is {state}, last heartbeat is {lastHeartbeat} while earliest Possible Heartbeat is {earliestPossibleHeartbeat}. Disposing client",
                    car.Vin, existingClient.WebSocketClient.State, existingClient.LastReceivedHeartbeat, earliestPossibleLastHeartbeat);
                existingClient.WebSocketClient.Dispose();
                Clients.Remove(existingClient);
            }

            _ = ConnectToFleetTelemetryApi(car.Vin);
        }
    }

    private async Task ConnectToFleetTelemetryApi(string vin)
    {
        logger.LogTrace("{method}({carId})", nameof(ConnectToFleetTelemetryApi), vin);
        var scope = serviceProvider.CreateScope();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var url = configurationWrapper.FleetTelemetryApiUrl() + $"vin={vin}";
        var authToken = await context.BackendTokens.AsNoTracking().SingleOrDefaultAsync();
        if (authToken == default)
        {
            logger.LogError("Can not connect to WebSocket: No token found for car {vin}", vin);
            return;
        }
        using var client = new ClientWebSocket();
        try
        {
            logger.LogInformation("Connecting Fleet Telemetry for car {vin}.", vin);
            client.Options.SetRequestHeader("Authorization", $"Bearer {authToken.AccessToken}");
            await client.ConnectAsync(new Uri(url), new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
            var cancellation = new CancellationTokenSource();
            var dtoClient = new DtoFleetTelemetryWebSocketClients
            {
                Vin = vin,
                WebSocketClient = client,
                CancellationToken = cancellation.Token,
                LastReceivedHeartbeat = currentDate,
                ConnectedSince = currentDate,
            };
            Clients.Add(dtoClient);
            var carId = await context.Cars
                .Where(c => c.Vin == vin)
                .Select(c => c.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            var loadPointManagementService = scope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
            await loadPointManagementService.CarStateChanged(carId).ConfigureAwait(false);
            try
            {
                await ReceiveMessages(dtoClient, dtoClient.Vin, carId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error receiving messages for car {vin}", vin);
            }
            finally
            {
                Clients.Remove(dtoClient);
                if (dtoClient.WebSocketClient.State != WebSocketState.Closed && dtoClient.WebSocketClient.State != WebSocketState.Aborted)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                        new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to WebSocket for car {vin}", vin);
        }
    }

    private async Task ReceiveMessages(DtoFleetTelemetryWebSocketClients client, string vin, int carId)
    {
        logger.LogTrace("{method}(webSocket, ctx, {vin}, {carId})", nameof(ReceiveMessages), vin, carId);
        var buffer = new byte[1024 * 4]; // Buffer to store incoming data
        while (client.WebSocketClient.State == WebSocketState.Open)
        {
            try
            {
                var scope = serviceProvider.CreateScope();
                var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
                logger.LogTrace("Waiting for new fleet telemetry message for car {vin}", vin);
                var result = await client.WebSocketClient.ReceiveAsync(new(buffer), client.CancellationToken);
                logger.LogTrace("Received new fleet telemetry message for car {vin}", vin);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // If the server closed the connection, close the WebSocket
                    await client.WebSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, client.CancellationToken);
                    logger.LogInformation("WebSocket connection closed by server.");
                }
                else
                {
                    // Decode the received message
                    var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (jsonMessage == "Heartbeat")
                    {
                        logger.LogTrace("Received heartbeat: {message}", jsonMessage);
                        client.LastReceivedHeartbeat = dateTimeProvider.DateTimeOffSetUtcNow();
                        continue;
                    }
                    logger.LogTrace("Received non heartbeat message.");
                    var jObject = JObject.Parse(jsonMessage);
                    var messageType = jObject[nameof(FleetTelemetryMessageBase.MessageType)]?.ToObject<FleetTelemetryMessageType>();
                    if (messageType == FleetTelemetryMessageType.Error)
                    {
                        var couldHandleErrorMessage = await HandleErrorMessage(jsonMessage);
                        if (!couldHandleErrorMessage)
                        {
                            logger.LogWarning("Could not deserialize non heartbeat message {string}", jsonMessage);
                        }
                        continue;
                    }
                    var message = DeserializeFleetTelemetryMessage(jsonMessage);
                    if (message == default)
                    {
                        logger.LogWarning("Could not deserialize non heartbeat message {string}", jsonMessage);
                        continue;
                    }
                    if (configurationWrapper.LogLocationData() ||
                        (message.Type != CarValueType.Latitude && message.Type != CarValueType.Longitude))
                    {
                        logger.LogDebug("Save fleet telemetry message {@message}", message);
                    }
                    else
                    {
                        logger.LogDebug("Save location message for car {carId}", carId);
                    }


                    var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
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
                    await context.SaveChangesAsync().ConfigureAwait(false);
                    if (configurationWrapper.GetVehicleDataFromTesla())
                    {
                        var settings = scope.ServiceProvider.GetRequiredService<ISettings>();
                        var settingsCar = settings.Cars.First(c => c.Vin == vin);
                        var shouldUpdateProperty = false;
                        HomeDetectionVia? homeDetectionVia = null;
                        if (message.Type == CarValueType.LocatedAtHome
                            || message.Type == CarValueType.LocatedAtWork
                            || message.Type == CarValueType.LocatedAtFavorite)
                        {
                            homeDetectionVia = await context.Cars
                                .Where(c => c.Id == settingsCar.Id)
                                .Select(c => c.HomeDetectionVia)
                                .FirstAsync();
                        }
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
                                    shouldUpdateProperty = true;
                                }
                                break;
                            case CarValueType.LocatedAtWork:
                                if (homeDetectionVia == HomeDetectionVia.LocatedAtWork)
                                {
                                    shouldUpdateProperty = true;
                                }
                                break;
                            case CarValueType.LocatedAtFavorite:
                                if (homeDetectionVia == HomeDetectionVia.LocatedAtFavorite)
                                {
                                    shouldUpdateProperty = true;
                                }
                                break;
                        }

                        if (shouldUpdateProperty)
                        {
                            var carPropertyUpdateHelper = scope.ServiceProvider.GetRequiredService<ICarPropertyUpdateHelper>();
                            carPropertyUpdateHelper.UpdateDtoCarProperty(settingsCar, carValueLog);
                        }
                        var loadPointManagementService = scope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await loadPointManagementService.CarStateChanged(settingsCar.Id);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error occurred while processing CarStateChanged for car ID {carId}", settingsCar.Id);
                            }
                            finally
                            {
                                scope.Dispose();
                            }
                        });
                    }

                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not reveive message");
            }
        }
    }

    private async Task<bool> HandleErrorMessage(string jsonMessage)
    {
        logger.LogTrace("{method}({jsonMessage}", nameof(HandleErrorMessage), jsonMessage);
        var message = JsonConvert.DeserializeObject<DtoFleetTelemetryErrorMessage>(jsonMessage);
        if (message == default)
        {
            return false;
        }
        foreach (var vin in message.MissingKeyVins)
        {
            var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
            var car = context.Cars.FirstOrDefault(c => c.Vin == vin);
            if (car == default)
            {
                continue;
            }
            logger.LogError("Set Fleet API state for car {vin} to not working", vin);
            car.TeslaFleetApiState = TeslaCarFleetApiState.NotWorking;
            await context.SaveChangesAsync();
        }

        foreach (var vin in message.UnsupportedFirmwareVins)
        {
            logger.LogError("Disable Fleet Telemetry for car {vin} as firmware is not supported", vin);
            await DisableFleetTelemetryForCar(vin).ConfigureAwait(false);
        }

        foreach (var vin in message.UnsupportedHardwareVins)
        {
            logger.LogError("Disable Fleet Telemetry for car {vin} as hardware is not supported", vin);
            await SetCarToFleetTelemetryHardwareIncompatible(vin).ConfigureAwait(false);
            await DisableFleetTelemetryForCar(vin).ConfigureAwait(false);
        }

        foreach (var vin in message.MaxConfigsVins)
        {
            logger.LogError("Car {vin} has already has max allowed Fleet Telemetry configs", vin);
        }

        return true;
    }

    private async Task SetCarToFleetTelemetryHardwareIncompatible(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(SetCarToFleetTelemetryHardwareIncompatible), vin);
        var scope = serviceProvider.CreateScope();
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
        logger.LogTrace("{method}({vin})", nameof(DisableFleetTelemetryForCar), vin);
        var scope = serviceProvider.CreateScope();
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

    internal DtoTscFleetTelemetryMessage? DeserializeFleetTelemetryMessage(string jsonMessage)
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new EnumDefaultConverter<CarValueType>(CarValueType.Unknown), },
        };
        var message = JsonConvert.DeserializeObject<DtoTscFleetTelemetryMessage>(jsonMessage, settings);
        return message;
    }
}
