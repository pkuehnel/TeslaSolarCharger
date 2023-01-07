using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class SolarMqttService : TestBase
{
    public SolarMqttService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void Can_Extract_MqttServer()
    {
        var insertedValue = "192.168.1.50";
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.SolarMqttServer()).Returns(insertedValue);

        var solarMqttService = Mock.Create<TeslaSolarCharger.Server.Services.SolarMqttService>();
        var mqttServer = solarMqttService.GetMqttServerAndPort(out var mqttServerPort);
        Assert.Equal(insertedValue, mqttServer);
        Assert.Null(mqttServerPort);
    }

    [Fact]
    public void Can_Extract_MqttServerAndPort()
    {
        var insertedValue = "192.168.1.50:1883";
        Mock.Mock<IConfigurationWrapper>().Setup(s => s.SolarMqttServer()).Returns(insertedValue);

        var solarMqttService = Mock.Create<TeslaSolarCharger.Server.Services.SolarMqttService>();
        var mqttServer = solarMqttService.GetMqttServerAndPort(out var mqttServerPort);
        Assert.Equal("192.168.1.50", mqttServer);
        Assert.Equal(mqttServerPort, 1883);
    }
}
