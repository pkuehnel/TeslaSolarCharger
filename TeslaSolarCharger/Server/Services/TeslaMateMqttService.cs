using MQTTnet;
using System.Globalization;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace TeslaSolarCharger.Server.Services;

public class TeslaMateMqttService(
    ILogger<TeslaMateMqttService> logger,
    IMqttClient mqttClient,
    MqttClientFactory mqttClientFactory,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IServiceScopeFactory serviceScopeFactory)
    : ITeslaMateMqttService
{

    // ReSharper disable once InconsistentNaming
    private const string TopicDisplayName = "display_name";
    // ReSharper disable once InconsistentNaming
    private const string TopicSoc = "battery_level";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargeLimit = "charge_limit_soc";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargerPhases = "charger_phases";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargerVoltage = "charger_voltage";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargerActualCurrent = "charger_actual_current";
    // ReSharper disable once InconsistentNaming
    private const string TopicPluggedIn = "plugged_in";
    // ReSharper disable once InconsistentNaming
    private const string TopicIsClimateOn = "is_climate_on";
    // ReSharper disable once InconsistentNaming
    private const string TopicTimeToFullCharge = "time_to_full_charge";
    // ReSharper disable once InconsistentNaming
    private const string TopicState = "state";
    // ReSharper disable once InconsistentNaming
    private const string TopicHealthy = "healthy";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargeCurrentRequest = "charge_current_request";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargeCurrentRequestMax = "charge_current_request_max";
    // ReSharper disable once InconsistentNaming
    private const string TopicScheduledChargingStartTime = "scheduled_charging_start_time";
    // ReSharper disable once InconsistentNaming
    private const string TopicLongitude = "longitude";
    // ReSharper disable once InconsistentNaming
    private const string TopicLatitude = "latitude";
    // ReSharper disable once InconsistentNaming
    private const string TopicSpeed = "speed";

    public bool IsMqttClientConnected => mqttClient.IsConnected;

    public async Task ConnectMqttClient()
    {
        logger.LogTrace("{method}()", nameof(ConnectMqttClient));
        var guid = Guid.NewGuid();
        var mqqtClientId = configurationWrapper.MqqtClientId() + guid;
        var mosquitoServer = configurationWrapper.MosquitoServer();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTcpServer(mosquitoServer)
            .Build();

        mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            var value = GetValueFromMessage(e.ApplicationMessage);
            if ((!configurationWrapper.LogLocationData()) && (string.Equals(value.Topic, TopicLongitude) || string.Equals(value.Topic, TopicLatitude)))
            {
                logger.LogTrace("Car Id: {carId}, Topic: {topic}, Value: xx.xxxxx", value.CarId, value.Topic);
            }
            else
            {
                logger.LogTrace("Car Id: {carId}, Topic: {topic}, Value: {value}", value.CarId, value.Topic, value.Value);
            }
            await UpdateCar(value);
        };


        if (mqttClient.IsConnected)
        {
            await DisconnectClient("Reconnecting with new configuration").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        try
        {
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not connect to TeslaMate mqtt server");
            return;
        }

        var topicPrefix = "teslamate/cars/+/";

        var mqttSubscribeOptions = mqttClientFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicDisplayName}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicSoc}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargeLimit}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargerPhases}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargerVoltage}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargerActualCurrent}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicPluggedIn}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicIsClimateOn}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicTimeToFullCharge}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicState}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicHealthy}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargeCurrentRequest}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicChargeCurrentRequestMax}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicScheduledChargingStartTime}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicLongitude}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicLatitude}");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{topicPrefix}{TopicSpeed}");
            })
            .Build();

        await mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task DisconnectClient(string reason)
    {
        logger.LogTrace("{method}({reason})", nameof(DisconnectClient), reason);
        if (mqttClient.IsConnected)
        {
            await mqttClient.DisconnectAsync().ConfigureAwait(false);
        }
    }


    public async Task ConnectClientIfNotConnected()
    {
        logger.LogTrace("{method}()", nameof(ConnectClientIfNotConnected));
        if (mqttClient.IsConnected)
        {
            logger.LogTrace("MqttClient is connected");
            return;
        }

        if (configurationWrapper.GetVehicleDataFromTesla())
        {
            logger.LogTrace("Not connecting to TeslaMate as data is retrieved from Teslas Fleet API");
            return;
        }
        logger.LogWarning("MqttClient is not connected");
        await ConnectMqttClient().ConfigureAwait(false);
    }

    internal async Task UpdateCar(TeslaMateValue value)
    {
        logger.LogTrace("{method}({@param})", nameof(UpdateCar), value);
        var car = settings.Cars.FirstOrDefault(c => c.TeslaMateCarId == value.CarId);

        if (car == null)
        {
            // Logge einen Fehler oder handle den Fall, dass kein Auto gefunden wurde
            logger.LogError($"No car found with TeslaMateCarId {value.CarId}");
            return; // oder andere geeignete Maßnahme
        }


        var currentDate = dateTimeProvider.UtcNow();
        var dateTimeOffset = new DateTimeOffset(currentDate, TimeSpan.Zero);
        switch (value.Topic)
        {
            case TopicDisplayName:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.Name = value.Value;
                }
                break;
            case TopicSoc:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.SoC.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.StateOfCharge,
                        IntValue = car.SoC.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicChargeLimit:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.SocLimit.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.StateOfChargeLimit,
                        IntValue = car.SocLimit.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                    var minimumSettableSocLimit = 50;
                    if (car.MinimumSoC > car.SocLimit.Value && car.SocLimit.Value > minimumSettableSocLimit)
                    {
                        logger.LogWarning("Reduce Minimum SoC {minimumSoC} as charge limit {chargeLimit} is lower.", car.MinimumSoC, car.SocLimit);
                        car.MinimumSoC = (int)car.SocLimit.Value;
                        logger.LogError("Can not handle lower Soc than minimumSoc");
                    }
                }
                break;
            case TopicChargerPhases:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerPhases.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.ChargerPhases,
                        IntValue = car.ChargerPhases.Value is null or > 1 ? 3 : 1,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                else
                {
                    //This is needed as TeslaMate sometime sends empty values during charger being connected.
                    logger.LogDebug($"{nameof(TopicChargerPhases)} is {value.Value}. Do not overwrite charger phases.");
                    //car.CarState.ChargerPhases = null;
                }
                break;
            case TopicChargerVoltage:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerVoltage.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.ChargerVoltage,
                        IntValue = car.ChargerVoltage.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                else
                {
                    car.ChargerVoltage.Update(dateTimeOffset, null);
                }
                break;
            case TopicChargerActualCurrent:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerActualCurrent.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.ChargeAmps,
                        IntValue = car.ChargerActualCurrent.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                    if (car.ChargerActualCurrent.Value > 0 && car.PluggedIn.Value != true)
                    {
                        logger.LogWarning("Car {carId} is not detected as plugged in but actual current > 0 => set plugged in to true", car.Id);
                        car.PluggedIn.Update(dateTimeProvider.DateTimeOffSetUtcNow(), true);
                    }
                }
                else
                {
                    car.ChargerActualCurrent.Update(dateTimeOffset, null);
                }
                break;
            case TopicPluggedIn:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.PluggedIn.Update(dateTimeProvider.DateTimeOffSetUtcNow(), Convert.ToBoolean(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.IsPluggedIn,
                        BooleanValue = car.PluggedIn.Value == true,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicIsClimateOn:
                break;
            case TopicTimeToFullCharge:
                break;
            case TopicState:
                var asleepValueLog = new CarValueLog()
                {
                    CarId = car.Id,
                    Timestamp = currentDate,
                    Source = CarValueSource.TeslaMate,
                    Type = CarValueType.AsleepOrOffline,
                };
                var chargingValueLog = new CarValueLog()
                {
                    CarId = car.Id,
                    Timestamp = currentDate,
                    Source = CarValueSource.TeslaMate,
                    Type = CarValueType.IsCharging,
                };
                switch (value.Value)
                {
                    case "asleep":
                        car.IsOnline.Update(dateTimeOffset, false);
                        car.IsCharging.Update(dateTimeOffset, false);
                        asleepValueLog.BooleanValue = true;
                        chargingValueLog.BooleanValue = false;
                        break;
                    case "offline":
                        car.IsOnline.Update(dateTimeOffset, false);
                        car.IsCharging.Update(dateTimeOffset, false);
                        asleepValueLog.BooleanValue = true;
                        chargingValueLog.BooleanValue = false;
                        break;
                    case "online":
                        car.IsOnline.Update(dateTimeOffset, true);
                        car.IsCharging.Update(dateTimeOffset, false);
                        asleepValueLog.BooleanValue = false;
                        chargingValueLog.BooleanValue = false;
                        break;
                    case "charging":
                        car.IsOnline.Update(dateTimeOffset, true);
                        car.IsCharging.Update(dateTimeOffset, true);
                        asleepValueLog.BooleanValue = false;
                        chargingValueLog.BooleanValue = true;
                        break;
                    case "suspended":
                        car.IsOnline.Update(dateTimeOffset, true);
                        car.IsCharging.Update(dateTimeOffset, false);
                        asleepValueLog.BooleanValue = false;
                        chargingValueLog.BooleanValue = false;
                        break;
                    case "driving":
                        car.IsOnline.Update(dateTimeOffset, true);
                        car.IsCharging.Update(dateTimeOffset, false);
                        asleepValueLog.BooleanValue = false;
                        chargingValueLog.BooleanValue = false;
                        break;
                    case "updating":
                        car.IsOnline.Update(dateTimeOffset, true);
                        asleepValueLog.BooleanValue = false;
                        break;
                    default:
                        logger.LogWarning("Unknown car state deteckted: {carState}", value.Value);
                        break;
                }
                teslaSolarChargerContext.CarValueLogs.Add(asleepValueLog);
                teslaSolarChargerContext.CarValueLogs.Add(chargingValueLog);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                break;
            case TopicHealthy:
                break;
            case TopicChargeCurrentRequest:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerRequestedCurrent.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.ChargeCurrentRequest,
                        IntValue = car.ChargerRequestedCurrent.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicChargeCurrentRequestMax:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerPilotCurrent.Update(dateTimeOffset, Convert.ToInt32(value.Value));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.ChargerPilotCurrent,
                        IntValue = car.ChargerPilotCurrent.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicScheduledChargingStartTime:
                break;
            case TopicLongitude:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.Longitude.Update(dateTimeOffset, Convert.ToDouble(value.Value, CultureInfo.InvariantCulture));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.Longitude,
                        DoubleValue = car.Longitude.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicLatitude:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.Latitude.Update(dateTimeOffset, Convert.ToDouble(value.Value, CultureInfo.InvariantCulture));
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = currentDate,
                        Source = CarValueSource.TeslaMate,
                        Type = CarValueType.Latitude,
                        DoubleValue = car.Latitude.Value,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
                break;
            case TopicSpeed:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    var speed = Convert.ToInt32(value.Value);
                    if (speed > 0 && car.PluggedIn.Value == true)
                    {
                        car.PluggedIn.Update(new DateTimeOffset(currentDate, TimeSpan.Zero), false);
                    }
                }
                break;
        }

        _ = Task.Run(async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var scopedLoadPointManagementService = scope.ServiceProvider.GetRequiredService<ILoadPointManagementService>();
            await scopedLoadPointManagementService.CarStateChanged(car.Id).ConfigureAwait(false);
        });
    }

    private TeslaMateValue GetValueFromMessage(MqttApplicationMessage mqttApplicationMessage)
    {
        var relevantString = mqttApplicationMessage.Topic.Substring(15, mqttApplicationMessage.Topic.Length - 15);

        var splittedString = relevantString.Split("/");

        return new TeslaMateValue()
        {
            CarId = Convert.ToInt32(splittedString[0]),
            Topic = splittedString[1],
            Value = mqttApplicationMessage.ConvertPayloadToString(),
        };
    }
}

public class TeslaMateValue
{
    public int CarId { get; set; }
    public string? Topic { get; set; }
    public string? Value { get; set; }
}
