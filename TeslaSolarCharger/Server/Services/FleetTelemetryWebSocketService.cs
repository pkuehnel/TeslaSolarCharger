using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks.Sources;
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
        var vins = await context.Cars
            .Where(c => c.UseFleetTelemetry)
            .Select(c => c.Vin)
            .ToListAsync();
        var bytesToSend = Encoding.UTF8.GetBytes("Heartbeat");
        foreach (var vin in vins)
        {
            if (string.IsNullOrEmpty(vin))
            {
                continue;
            }
            var existingClient = Clients.FirstOrDefault(c => c.Vin == vin);
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
                        logger.LogError(ex, "Error sending heartbeat for car {vin}", vin);
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
            ConnectToFleetTelemetryApi(vin);
        }
    }

    private async Task ConnectToFleetTelemetryApi(string vin)
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
        var url = configurationWrapper.FleetTelemetryApiUrl() + $"teslaToken={token.AccessToken}&region={token.Region}&vin={vin}&forceReconfiguration=false";
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
                    logger.LogTrace("Received message: {message}", jsonMessage);
                    if(jsonMessage == "Heartbeat")
                    {
                        continue;
                    }
                    logger.LogDebug("Received non heartbeat message {string}", jsonMessage);
                    // Deserialize the JSON message into a C# object
                    var message = DeserializeFleetTelemetryMessage(jsonMessage);
                    if (message != null)
                    {
                        logger.LogDebug("Saving fleet telemetry message");
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
                            BoolValue = message.BoolValue,
                            InvalidValue = message.InvalidValue,
                            Timestamp = message.TimeStamp.UtcDateTime,
                            Source = CarValueSource.FleetTelemetry,
                        };
                        context.CarValueLogs.Add(carValueLog);
                        await context.SaveChangesAsync().ConfigureAwait(false);
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
