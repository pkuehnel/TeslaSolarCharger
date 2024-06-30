using MQTTnet;
using MQTTnet.Client;
using System.Globalization;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaMateMqttService : ITeslaMateMqttService
{
    private readonly ILogger<TeslaMateMqttService> _logger;
    private readonly IMqttClient _mqttClient;
    private readonly MqttFactory _mqttFactory;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IConfigJsonService _configJsonService;
    private readonly IDateTimeProvider _dateTimeProvider;

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

    public bool IsMqttClientConnected => _mqttClient.IsConnected;

    public TeslaMateMqttService(ILogger<TeslaMateMqttService> logger, IMqttClient mqttClient, MqttFactory mqttFactory,
        ISettings settings, IConfigurationWrapper configurationWrapper,
        IConfigJsonService configJsonService, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _mqttClient = mqttClient;
        _mqttFactory = mqttFactory;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _configJsonService = configJsonService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task ConnectMqttClient()
    {
        _logger.LogTrace("{method}()", nameof(ConnectMqttClient));
        var guid = Guid.NewGuid();
        var mqqtClientId = _configurationWrapper.MqqtClientId() + guid;
        var mosquitoServer = _configurationWrapper.MosquitoServer();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTcpServer(mosquitoServer)
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var value = GetValueFromMessage(e.ApplicationMessage);
            if ((!_configurationWrapper.LogLocationData()) && (string.Equals(value.Topic, TopicLongitude) || string.Equals(value.Topic, TopicLatitude)))
            {
                _logger.LogTrace("Car Id: {carId}, Topic: {topic}, Value: xx.xxxxx", value.CarId, value.Topic);
            }
            else
            {
                _logger.LogTrace("Car Id: {carId}, Topic: {topic}, Value: {value}", value.CarId, value.Topic, value.Value);
            }
            UpdateCar(value);
            return Task.CompletedTask;
        };


        if (_mqttClient.IsConnected)
        {
            await DisconnectClient("Reconnecting with new configuration").ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        try
        {
            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not connect to TeslaMate mqtt server");
            return;
        }

        var topicPrefix = "teslamate/cars/+/";

        var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
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

        await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task DisconnectClient(string reason)
    {
        _logger.LogTrace("{method}({reason})", nameof(DisconnectClient), reason);
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync().ConfigureAwait(false);
        }
    }


    public async Task ConnectClientIfNotConnected()
    {
        _logger.LogTrace("{method}()", nameof(ConnectClientIfNotConnected));
        if (_mqttClient.IsConnected)
        {
            _logger.LogTrace("MqttClient is connected");
            return;
        }

        if (_configurationWrapper.GetVehicleDataFromTesla())
        {
            _logger.LogInformation("Not connecting to TeslaMate as data is retrieved from Teslas Fleet API");
            return;
        }
        _logger.LogWarning("MqttClient is not connected");
        await ConnectMqttClient().ConfigureAwait(false);
    }

    internal void UpdateCar(TeslaMateValue value)
    {
        _logger.LogTrace("{method}({@param})", nameof(UpdateCar), value);
        var car = _settings.Cars.FirstOrDefault(c => c.TeslaMateCarId == value.CarId);

        if (car == null)
        {
            // Logge einen Fehler oder handle den Fall, dass kein Auto gefunden wurde
            _logger.LogError($"No car found with TeslaMateCarId {value.CarId}");
            return; // oder andere geeignete Maßnahme
        }


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
                    car.SoC = Convert.ToInt32(value.Value);
                }
                break;
            case TopicChargeLimit:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.SocLimit = Convert.ToInt32(value.Value);
                    var minimumSettableSocLimit = 50;
                    if (car.MinimumSoC > car.SocLimit && car.SocLimit > minimumSettableSocLimit)
                    {
                        _logger.LogWarning("Reduce Minimum SoC {minimumSoC} as charge limit {chargeLimit} is lower.", car.MinimumSoC, car.SocLimit);
                        car.MinimumSoC = (int)car.SocLimit;
                        _logger.LogError("Can not handle lower Soc than minimumSoc");
                    }
                }
                break;
            case TopicChargerPhases:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerPhases = Convert.ToInt32(value.Value);
                }
                else
                {
                    //This is needed as TeslaMate sometime sends empty values during charger being connected.
                    _logger.LogDebug($"{nameof(TopicChargerPhases)} is {value.Value}. Do not overwrite charger phases.");
                    //car.CarState.ChargerPhases = null;
                }
                break;
            case TopicChargerVoltage:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerVoltage = Convert.ToInt32(value.Value);
                }
                else
                {
                    car.ChargerVoltage = null;
                }
                break;
            case TopicChargerActualCurrent:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerActualCurrent = Convert.ToInt32(value.Value);
                    if (car.ChargerActualCurrent < 5 &&
                        car.ChargerRequestedCurrent == car.LastSetAmp &&
                        car.LastSetAmp == car.ChargerActualCurrent - 1 &&
                        car.LastSetAmp > 0)
                    {
                        _logger.LogWarning("CarId {carId}: Reducing {actualCurrent} from {originalValue} to {newValue} due to error in TeslaApi", car.Id, nameof(car.ChargerActualCurrent), car.ChargerActualCurrent, car.LastSetAmp);
                        //ToDo: Set to average of requested and actual current
                        car.ChargerActualCurrent = car.LastSetAmp;
                    }

                    if (car.ChargerActualCurrent > 0 && car.PluggedIn != true)
                    {
                        _logger.LogWarning("Car {carId} is not detected as plugged in but actual current > 0 => set plugged in to true", car.Id);
                        car.PluggedIn = true;
                    }
                }
                else
                {
                    car.ChargerActualCurrent = null;
                }
                break;
            case TopicPluggedIn:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.PluggedIn = Convert.ToBoolean(value.Value);
                }
                break;
            case TopicIsClimateOn:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ClimateOn = Convert.ToBoolean(value.Value);
                }
                break;
            case TopicTimeToFullCharge:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.TimeUntilFullCharge = TimeSpan.FromHours(Convert.ToDouble(value.Value, CultureInfo.InvariantCulture));
                }
                else
                {
                    car.TimeUntilFullCharge = null;
                }
                break;
            case TopicState:
                switch (value.Value)
                {
                    case "asleep":
                        car.State = CarStateEnum.Asleep;
                        break;
                    case "offline":
                        car.State = CarStateEnum.Offline;
                        break;
                    case "online":
                        car.State = CarStateEnum.Online;
                        break;
                    case "charging":
                        car.State = CarStateEnum.Charging;
                        break;
                    case "suspended":
                        car.State = CarStateEnum.Suspended;
                        break;
                    case "driving":
                        car.State = CarStateEnum.Driving;
                        break;
                    case "updating":
                        car.State = CarStateEnum.Updating;
                        break;
                    default:
                        _logger.LogWarning("Unknown car state deteckted: {carState}", value.Value);
                        car.State = CarStateEnum.Unknown;
                        break;
                }
                break;
            case TopicHealthy:
                car.Healthy = Convert.ToBoolean(value.Value);
                _logger.LogTrace("Car healthiness if car {carId} changed to {healthiness}", car.Id, car.Healthy);
                break;
            case TopicChargeCurrentRequest:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerRequestedCurrent = Convert.ToInt32(value.Value);
                }
                break;
            case TopicChargeCurrentRequestMax:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.ChargerPilotCurrent = Convert.ToInt32(value.Value);
                }
                break;
            case TopicScheduledChargingStartTime:
                _logger.LogTrace("{topicName} changed to {value}", nameof(TopicScheduledChargingStartTime), value.Value);
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    var parsedScheduledChargingStartTime = DateTimeOffset.Parse(value.Value);
                    if (parsedScheduledChargingStartTime < _dateTimeProvider.DateTimeOffSetNow().AddDays(-14))
                    {
                        _logger.LogWarning("TeslaMate set scheduled charging start time to {teslaMateValue}. As this is in the past, it will be ignored.", parsedScheduledChargingStartTime);
                        car.ScheduledChargingStartTime = null;
                    }
                    else
                    {
                        car.ScheduledChargingStartTime = parsedScheduledChargingStartTime;
                    }
                }
                else
                {
                    car.ScheduledChargingStartTime = null;
                }
                break;
            case TopicLongitude:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.Longitude = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
                }
                break;
            case TopicLatitude:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.Latitude = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
                }
                break;
            case TopicSpeed:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    var speed = Convert.ToInt32(value.Value);
                    if (speed > 0 && car.PluggedIn == true)
                    {
                        car.PluggedIn = false;
                    }
                }
                break;
        }
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
