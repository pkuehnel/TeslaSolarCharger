using MQTTnet;
using MQTTnet.Client;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;

namespace SmartTeslaAmpSetter.Server;

public class MqttHelper
{
    private readonly ILogger<MqttHelper> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttClient _mqttClient;
    private readonly MqttFactory _mqttFactory;
    private readonly ISettings _settings;

    private const string TopicDisplayName = "display_name";
    private const string TopicSoc = "battery_level";
    private const string TopicChargeLimit = "charge_limit_soc";
    private const string TopicGeofence = "geofence";
    private const string TopicChargerPhases = "charger_phases";
    private const string TopicChargerVoltage = "charger_voltage";
    private const string TopicChargerActualCurrent = "charger_actual_current";
    private const string TopicPluggedIn = "plugged_in";
    private const string TopicIsClimateOn = "is_climate_on";
    private const string TopicTimeToFullCharge = "time_to_full_charge";
    private const string TopicState = "state";
    //ToDo: Add after next TeslaMateRelease
    //private const string TopicChargeCurrentRequest = "charge_current_request";
    //public const string TopicChargeCurrentRequestMax = "charge_current_request_max";

    public MqttHelper(ILogger<MqttHelper> logger, IConfiguration configuration, MqttClient mqttClient, MqttFactory mqttFactory, ISettings settings)
    {
        _logger = logger;
        _configuration = configuration;
        _mqttClient = mqttClient;
        _mqttFactory = mqttFactory;
        _settings = settings;
    }

    public async Task ConfigureMqttClient()
    {
        _logger.LogTrace("{method}()", nameof(ConfigureMqttClient));
        var mqqtClientId = _configuration.GetValue<string>("MqqtClientId");
        var mosquitoServer = _configuration.GetValue<string>("MosquitoServer");
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

    private void UpdateCar(TeslaMateValue value)
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
                car.CarState.State = value.Value;
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