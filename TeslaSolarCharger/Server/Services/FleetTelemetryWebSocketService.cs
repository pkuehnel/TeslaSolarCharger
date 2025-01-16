using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryWebSocketService(
    ILogger<FleetTelemetryWebSocketService> logger,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IServiceProvider serviceProvider,
    ISettings settings) : IFleetTelemetryWebSocketService
{
    private readonly TimeSpan _heartbeatsendTimeout = TimeSpan.FromSeconds(5);

    private List<DtoFleetTelemetryWebSocketClients> Clients { get; set; } = new();

    public bool IsClientConnected(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(IsClientConnected), vin);
        return Clients.Any(c => c.Vin == vin && c.WebSocketClient.State == WebSocketState.Open);
    }

    public async Task ReconnectWebSocketsForEnabledCars()
    {
        logger.LogTrace("{method}", nameof(ReconnectWebSocketsForEnabledCars));
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var cars = await context.Cars
            .Where(c => c.UseFleetTelemetry
                        && (c.ShouldBeManaged == true)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotWorking)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.OpenedLinkButNotTested)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotConfigured)
                        && (c.IsFleetTelemetryHardwareIncompatible == false))
            .Select(c => new { c.Vin, IncludeTrackingRelevantFields = c.IncludeTrackingRelevantFields, })
            .ToListAsync();
        var bytesToSend = Encoding.UTF8.GetBytes("Heartbeat");
        foreach (var car in cars)
        {
            if (string.IsNullOrEmpty(car.Vin))
            {
                continue;
            }

            var existingClient = Clients.FirstOrDefault(c => c.Vin == car.Vin);
            if (existingClient != default)
            {
                var currentTime = dateTimeProvider.UtcNow();
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

            ConnectToFleetTelemetryApi(car.Vin, car.IncludeTrackingRelevantFields);
        }
    }

    public async Task DisconnectWebSocketsByVin(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(DisconnectWebSocketsByVin), vin);
        var client = Clients.FirstOrDefault(c => c.Vin == vin);
        if (client != default)
        {
            if (client.WebSocketClient.State == WebSocketState.Open)
            {
                await client.WebSocketClient
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", new CancellationTokenSource(_heartbeatsendTimeout).Token)
                    .ConfigureAwait(false);
            }

            client.WebSocketClient.Dispose();
            Clients.Remove(client);
        }
    }

    private async Task ConnectToFleetTelemetryApi(string vin, bool includeTrackingRelevantFields)
    {
        logger.LogTrace("{method}({carId})", nameof(ConnectToFleetTelemetryApi), vin);
        var currentDate = dateTimeProvider.UtcNow();
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var tscConfigurationService = scope.ServiceProvider.GetRequiredService<ITscConfigurationService>();
        var constants = scope.ServiceProvider.GetRequiredService<IConstants>();
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            throw new InvalidOperationException("No Decryption key found.");
        }
        var url = configurationWrapper.FleetTelemetryApiUrl() +
                  $"vin={vin}&forceReconfiguration=false&includeTrackingRelevantFields={includeTrackingRelevantFields}&encryptionKey={Uri.EscapeDataString(decryptionKey)}";
        var authToken = await context.BackendTokens.AsNoTracking().SingleOrDefaultAsync();
        if(authToken == default)
        {
            logger.LogError("Can not connect to WebSocket: No token found for car {vin}", vin);
            return;
        }
        using var client = new ClientWebSocket();
        try
        {
            client.Options.SetRequestHeader("Authorization", $"Bearer {authToken.AccessToken}");
            await client.ConnectAsync(new Uri(url), new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
            var cancellation = new CancellationTokenSource();
            var dtoClient = new DtoFleetTelemetryWebSocketClients
            {
                Vin = vin,
                WebSocketClient = client,
                CancellationToken = cancellation.Token,
                LastReceivedHeartbeat = currentDate,
            };
            Clients.Add(dtoClient);
            var carId = await context.Cars
                .Where(c => c.Vin == vin)
                .Select(c => c.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
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
                // Receive message from the WebSocket server
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
                        client.LastReceivedHeartbeat = dateTimeProvider.UtcNow();
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

                    var scope = serviceProvider.CreateScope();
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
                        var settingsCar = settings.Cars.First(c => c.Vin == vin);
                        string? propertyName = null;
                        switch (message.Type)
                        {
                            case CarValueType.ChargeAmps:
                                propertyName = nameof(DtoCar.ChargerActualCurrent);
                                break;
                            case CarValueType.ChargeCurrentRequest:
                                propertyName = nameof(DtoCar.ChargerRequestedCurrent);
                                break;
                            case CarValueType.IsPluggedIn:
                                propertyName = nameof(DtoCar.PluggedIn);
                                break;
                            case CarValueType.IsCharging:
                                if (carValueLog.BooleanValue == true && settingsCar.State != CarStateEnum.Charging)
                                {
                                    logger.LogDebug("Set car state for car {carId} to charging", carId);
                                    settingsCar.State = CarStateEnum.Charging;
                                }
                                else if (carValueLog.BooleanValue == false && settingsCar.State == CarStateEnum.Charging)
                                {
                                    logger.LogDebug("Set car state for car {carId} to online", carId);
                                    settingsCar.State = CarStateEnum.Online;
                                }
                                break;
                            case CarValueType.ChargerPilotCurrent:
                                propertyName = nameof(DtoCar.ChargerPilotCurrent);
                                break;
                            case CarValueType.Longitude:
                                propertyName = nameof(DtoCar.Longitude);
                                break;
                            case CarValueType.Latitude:
                                propertyName = nameof(DtoCar.Latitude);
                                break;
                            case CarValueType.StateOfCharge:
                                propertyName = nameof(DtoCar.SoC);
                                break;
                            case CarValueType.StateOfChargeLimit:
                                propertyName = nameof(DtoCar.SocLimit);
                                break;
                            case CarValueType.ChargerPhases:
                                propertyName = nameof(DtoCar.ChargerPhases);
                                break;
                            case CarValueType.ChargerVoltage:
                                propertyName = nameof(DtoCar.ChargerVoltage);
                                break;
                            case CarValueType.VehicleName:
                                propertyName = nameof(DtoCar.Name);
                                break;
                        }

                        if (propertyName != default)
                        {
                            UpdateDtoCarProperty(settingsCar, carValueLog, propertyName);
                        }
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
        if(message == default)
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
            logger.LogInformation("Set Fleet API state for car {vin} to not working", vin);
            car.TeslaFleetApiState = TeslaCarFleetApiState.NotWorking;
            await context.SaveChangesAsync();
        }

        foreach (var vin in message.UnsupportedFirmwareVins)
        {
            logger.LogInformation("Disable Fleet Telemetry for car {vin} as firmware is not supported", vin);
            await DisableFleetTelemetryForCar(vin).ConfigureAwait(false);
        }

        foreach (var vin in message.UnsupportedHardwareVins)
        {
            logger.LogInformation("Disable Fleet Telemetry for car {vin} as hardware is not supported", vin);
            await SetCarToFleetTelemetryHardwareIncompatible(vin).ConfigureAwait(false);
            await DisableFleetTelemetryForCar(vin).ConfigureAwait(false);
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

    internal void UpdateDtoCarProperty(DtoCar car, CarValueLog carValueLog, string propertyName)
    {
        logger.LogTrace("{method}({carId}, ***secret***, {propertyName})", nameof(UpdateDtoCarProperty), car.Id, propertyName);
        // List of relevant property names
        var relevantPropertyNames = new List<string>
        {
            nameof(CarValueLog.DoubleValue),
            nameof(CarValueLog.IntValue),
            nameof(CarValueLog.StringValue),
            nameof(CarValueLog.UnknownValue),
            nameof(CarValueLog.BooleanValue),
        };

        // Filter properties to only the relevant ones
        var carValueProperties = typeof(CarValueLog)
            .GetProperties()
            .Where(p => relevantPropertyNames.Contains(p.Name));

        object valueToConvert = null;

        // Find the first non-null property in CarValueLog among the relevant ones
        foreach (var prop in carValueProperties)
        {
            var value = prop.GetValue(carValueLog);
            if (value != null)
            {
                valueToConvert = value;
                break;
            }
        }

        if (valueToConvert != null)
        {
            var dtoProperty = typeof(DtoCar).GetProperty(propertyName);
            if (dtoProperty != null)
            {
                var dtoPropertyType = dtoProperty.PropertyType;

                // Handle nullable types
                var targetType = Nullable.GetUnderlyingType(dtoPropertyType) ?? dtoPropertyType;
                object? convertedValue = null;

                try
                {
                    // Directly handle numeric conversions without converting to string
                    if (targetType == typeof(int))
                    {
                        if (valueToConvert is int intValue)
                        {
                            convertedValue = intValue;
                        }
                        else if (valueToConvert is double doubleValue)
                        {
                            // Decide how to handle the fractional part
                            intValue = (int)Math.Round(doubleValue); // Or Math.Floor(doubleValue), Math.Ceiling(doubleValue)
                            convertedValue = intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            // Use InvariantCulture when parsing the string
                            if (int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                            {
                                convertedValue = intValue;
                            }
                            else if (double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                            {
                                intValue = (int)Math.Round(doubleValue);
                                convertedValue = intValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToInt32(valueToConvert);
                        }
                    }
                    else if (targetType == typeof(double))
                    {
                        if (valueToConvert is double doubleValue)
                        {
                            convertedValue = doubleValue;
                        }
                        else if (valueToConvert is int intValue)
                        {
                            convertedValue = (double)intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (double.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out doubleValue))
                            {
                                convertedValue = doubleValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToDouble(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(decimal))
                    {
                        if (valueToConvert is decimal decimalValue)
                        {
                            convertedValue = decimalValue;
                        }
                        else if (valueToConvert is double doubleValue)
                        {
                            convertedValue = (decimal)doubleValue;
                        }
                        else if (valueToConvert is int intValue)
                        {
                            convertedValue = (decimal)intValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (decimal.TryParse(valueString, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimalValue))
                            {
                                convertedValue = decimalValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToDecimal(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(bool))
                    {
                        if (valueToConvert is bool boolValue)
                        {
                            convertedValue = boolValue;
                        }
                        else if (valueToConvert is string valueString)
                        {
                            if (bool.TryParse(valueString, out boolValue))
                            {
                                convertedValue = boolValue;
                            }
                        }
                        else if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ToBoolean(valueToConvert, CultureInfo.InvariantCulture);
                        }
                    }
                    else if (targetType == typeof(string))
                    {
                        // Use InvariantCulture to ensure consistent formatting
                        convertedValue = Convert.ToString(valueToConvert, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        // For other types, attempt to convert using ChangeType
                        if (valueToConvert is IConvertible)
                        {
                            convertedValue = Convert.ChangeType(valueToConvert, targetType, CultureInfo.InvariantCulture);
                        }
                        else if (targetType.IsAssignableFrom(valueToConvert.GetType()))
                        {
                            convertedValue = valueToConvert;
                        }
                    }

                    // Update the property if conversion was successful
                    if (convertedValue != null)
                    {
                        dtoProperty.SetValue(car, convertedValue);
                    }
                    else
                    {
                        logger.LogInformation("Do not update {propertyName} on car {carId} as converted value is null", propertyName, car.Id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error converting {propertyName} on car {carId}", propertyName, car.Id);
                }
            }
        }
    }
}
