using MQTTnet;
using MQTTnet.Client;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using CarState = TeslaSolarCharger.Shared.Enums.CarState;

namespace TeslaSolarCharger.Server.Services;

public class MqttService : IMqttService
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttClient _mqttClient;
    private readonly MqttFactory _mqttFactory;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;

    // ReSharper disable once InconsistentNaming
    private const string TopicDisplayName = "display_name";
    // ReSharper disable once InconsistentNaming
    private const string TopicSoc = "battery_level";
    // ReSharper disable once InconsistentNaming
    private const string TopicChargeLimit = "charge_limit_soc";
    // ReSharper disable once InconsistentNaming
    private const string TopicGeofence = "geofence";
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
    //ToDo: Add after next TeslaMateRelease
    //private const string TopicChargeCurrentRequest = "charge_current_request";
    //public const string TopicChargeCurrentRequestMax = "charge_current_request_max";

    public MqttService(ILogger<MqttService> logger, MqttClient mqttClient, MqttFactory mqttFactory, 
        ISettings settings, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _mqttClient = mqttClient;
        _mqttFactory = mqttFactory;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
    }

    public async Task ConfigureMqttClient()
    {
        _logger.LogTrace("{method}()", nameof(ConfigureMqttClient));
        var mqqtClientId = _configurationWrapper.MqqtClientId();
        var mosquitoServer = _configurationWrapper.MosquitoServer();
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTcpServer(mosquitoServer)
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var value = GetValueFromMessage(e.ApplicationMessage);
            _logger.LogDebug("Car Id: {carId}, Topic: {topic}, Value: {value}", value.CarId, value.Topic, value.Value);
            UpdateCar(value);
            return Task.CompletedTask;
        };

        await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

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
                f.WithTopic($"{topicPrefix}{TopicGeofence}");
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
            //ToDo: Add after next TeslaMateRelease
            //.WithTopicFilter(f =>
            //{
            //    f.WithTopic($"{topicPrefix}{TopicChargeCurrentRequest}");
            //})
            //.WithTopicFilter(f =>
            //{
            //    f.WithTopic($"{topicPrefix}{TopicChargeCurrentRequestMax");
            //})
            .Build();

        await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
    }

    internal void UpdateCar(TeslaMateValue value)
    {
        var car = _settings.Cars.First(c => c.Id == value.CarId);

        switch (value.Topic)
        {
            case TopicDisplayName:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.Name = value.Value;
                }
                break;
            case TopicSoc:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.SoC = Convert.ToInt32(value.Value);
                }
                break;
            case TopicChargeLimit:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.SocLimit = Convert.ToInt32(value.Value);
                }
                break;
            case TopicGeofence:
                car.CarState.Geofence = value.Value;
                break;
            case TopicChargerPhases:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.ChargerPhases = Convert.ToInt32(value.Value);
                }
                else
                {
                    car.CarState.ChargerPhases = null;
                }
                break;
            case TopicChargerVoltage:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.ChargerVoltage = Convert.ToInt32(value.Value);
                }
                else
                {
                    car.CarState.ChargerVoltage = null;
                }
                break;
            case TopicChargerActualCurrent:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.ChargerActualCurrent = Convert.ToInt32(value.Value);
                    if (car.CarState.ChargerActualCurrent < 5 &&
                        car.CarState.LastSetAmp == car.CarState.ChargerActualCurrent - 1 &&
                        car.CarState.LastSetAmp > 0)
                    {
                        _logger.LogWarning("CarId {carId}: Reducing {actualCurrent} from {originalValue} to {newValue} due to error in TeslaApi", car.Id, nameof(car.CarState.ChargerActualCurrent), car.CarState.ChargerActualCurrent, car.CarState.LastSetAmp);
                        car.CarState.ChargerActualCurrent = car.CarState.LastSetAmp;
                    }
                }
                else
                {
                    car.CarState.ChargerActualCurrent = null;
                }
                break;
            case TopicPluggedIn:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.PluggedIn = Convert.ToBoolean(value.Value);
                }
                break;
            case TopicIsClimateOn:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.ClimateOn = Convert.ToBoolean(value.Value);
                }
                break;
            case TopicTimeToFullCharge:
                if (!string.IsNullOrWhiteSpace(value.Value))
                {
                    car.CarState.TimeUntilFullCharge = TimeSpan.FromHours(Convert.ToDouble(value.Value));
                }
                else
                {
                    car.CarState.TimeUntilFullCharge = null;
                }
                break;
            case TopicState:
                car.CarState.StateString = value.Value;
                switch (value.Value)
                {
                    case "asleep":
                        car.CarState.State = CarState.Asleep;
                        break;
                    case "offline":
                        car.CarState.State = CarState.Offline;
                        break;
                    case "online":
                        car.CarState.State = CarState.Online;
                        break;
                    case "charging":
                        car.CarState.State = CarState.Charging;
                        break;
                    case "suspended":
                        car.CarState.State = CarState.Suspended;
                        break;
                    case "driving":
                        car.CarState.State = CarState.Driving;
                        break;
                    case "updating":
                        car.CarState.State = CarState.Updating;
                        break;
                    default:
                        _logger.LogWarning("Unknown car state deteckted: {carState}", value.Value);
                        car.CarState.State = CarState.Unknown;
                        break;
                }
                _logger.LogDebug("New car state detected {car state}", car.CarState.StateString);
                break;
            case TopicHealthy:
                car.CarState.Healthy = Convert.ToBoolean(value.Value);
                _logger.LogDebug("Car healthiness if car {carId} changed to {healthiness}", car.Id, car.CarState.Healthy);
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