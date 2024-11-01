using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Helper;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryWebSocketService(ILogger<FleetTelemetryWebSocketService> logger,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    IServiceProvider serviceProvider) : IFleetTelemetryWebSocketService
{
    private readonly TimeSpan _heartbeatsendTimeout = TimeSpan.FromSeconds(5);

    private List<DtoFleetTelemetryWebSocketClients> Clients { get; set; } = new();

    public async Task ReconnectWebSocketsForEnabledCars()
    {
        logger.LogTrace("{method}", nameof(ReconnectWebSocketsForEnabledCars));
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var cars = await context.Cars
            .Where(c => c.UseFleetTelemetry && (c.ShouldBeManaged == true))
            .Select(c => new
            {
                c.Vin,
                c.UseFleetTelemetryForLocationData,
            })
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
                if (existingClient.WebSocketClient.State == WebSocketState.Open)
                {
                    var segment = new ArraySegment<byte>(bytesToSend);
                    try
                    {
                        await existingClient.WebSocketClient.SendAsync(segment, WebSocketMessageType.Text, true,
                            new CancellationTokenSource(_heartbeatsendTimeout).Token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error sending heartbeat for car {vin}", car);
                        existingClient.WebSocketClient.Dispose();
                        Clients.Remove(existingClient);
                    }
                    continue;
                }
                else
                {
                    existingClient.WebSocketClient.Dispose();
                    Clients.Remove(existingClient);
                }
            }
            ConnectToFleetTelemetryApi(car.Vin, car.UseFleetTelemetryForLocationData);
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
                await client.WebSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
            }
            client.WebSocketClient.Dispose();
            Clients.Remove(client);
        }
    }

    private async Task ConnectToFleetTelemetryApi(string vin, bool useFleetTelemetryForLocationData)
    {
        logger.LogTrace("{method}({carId})", nameof(ConnectToFleetTelemetryApi), vin);
        var currentDate = dateTimeProvider.UtcNow();
        var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TeslaSolarChargerContext>();
        var token = await context.TeslaTokens
            .Where(t => t.ExpiresAtUtc > currentDate)
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if(token == default)
        {
            logger.LogError("Can not connect to WebSocket: No token found for car {vin}", vin);
            return;
        }
        var url = configurationWrapper.FleetTelemetryApiUrl() + $"teslaToken={token.AccessToken}&region={token.Region}&vin={vin}&forceReconfiguration=false&includeLocation={useFleetTelemetryForLocationData}";
        using var client = new ClientWebSocket();
        try
        {
            await client.ConnectAsync(new Uri(url), new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
            var cancellation = new CancellationTokenSource();
            var dtoClient = new DtoFleetTelemetryWebSocketClients
            {
                Vin = vin,
                WebSocketClient = client,
                CancellationToken = cancellation.Token,
            };
            Clients.Add(dtoClient);
            var carId = await context.Cars
                .Where(c => c.Vin == vin)
                .Select(c => c.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            try
            {
                await ReceiveMessages(client, dtoClient.CancellationToken, dtoClient.Vin, carId).ConfigureAwait(false);
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
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", new CancellationTokenSource(_heartbeatsendTimeout).Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to WebSocket for car {vin}", vin);
        }
    }

    private async Task ReceiveMessages(ClientWebSocket webSocket, CancellationToken ctx, string vin, int carId)
    {
        var buffer = new byte[1024 * 4]; // Buffer to store incoming data
        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                // Receive message from the WebSocket server
                var result = await webSocket.ReceiveAsync(new(buffer), ctx);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // If the server closed the connection, close the WebSocket
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, ctx);
                    logger.LogInformation("WebSocket connection closed by server.");
                }
                else
                {
                    // Decode the received message
                    var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if(jsonMessage == "Heartbeat")
                    {
                        logger.LogTrace("Received heartbeat: {message}", jsonMessage);
                        continue;
                    }
                    var message = DeserializeFleetTelemetryMessage(jsonMessage);
                    if (message != null)
                    {
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
                    }
                    else
                    {
                        logger.LogWarning("Could not deserialize non heartbeat message {string}", jsonMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not reveive message");
            }
        }
    }

    internal DtoTscFleetTelemetryMessage? DeserializeFleetTelemetryMessage(string jsonMessage)
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new EnumDefaultConverter<CarValueType>(CarValueType.Unknown),
            },
        };
        var message = JsonConvert.DeserializeObject<DtoTscFleetTelemetryMessage>(jsonMessage, settings);
        return message;
    }
}
