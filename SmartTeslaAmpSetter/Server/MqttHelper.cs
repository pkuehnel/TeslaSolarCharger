using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace SmartTeslaAmpSetter.Server;

public class MqttHelper
{
    private readonly ILogger<MqttHelper> _logger;
    private readonly IConfiguration _configuration;
    private readonly MqttClient _mqttClient;
    private readonly MqttFactory _mqttFactory;

    public MqttHelper(ILogger<MqttHelper> logger, IConfiguration configuration, MqttClient mqttClient, MqttFactory mqttFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _mqttClient = mqttClient;
        _mqttFactory = mqttFactory;
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
            return Task.CompletedTask;
        };

        await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/display_name");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/battery_level");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/charge_limit_soc");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/geofence");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/charger_phases");
            })
            .WithTopicFilter(f =>
            {
                f.WithTopic("teslamate/cars/+/charger_voltage");
            })
            //ToDo: Add after next TeslaMateRelease
            //.WithTopicFilter(f =>
            //{
            //    f.WithTopic("teslamate/cars/?/charge_current_request");
            //})
            //.WithTopicFilter(f =>
            //{
            //    f.WithTopic("teslamate/cars/?/charge_current_request_max");
            //})
            .Build();

        await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
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
    public string Topic { get; set; }
    public string Value { get; set; }
}