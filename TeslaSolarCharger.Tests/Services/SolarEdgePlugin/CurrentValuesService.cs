using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.SolarEdgePlugin;

public class CurrentValuesService : TestBase
{
    public CurrentValuesService(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Theory]
    [InlineData("{\"siteCurrentPowerFlow\":{\"updateRefreshRate\":3,\"unit\":\"kW\",\"connections\":[{\"from\":\"LOAD\",\"to\":\"Grid\"},{\"from\":\"PV\",\"to\":\"Load\"},{\"from\":\"PV\",\"to\":\"Storage\"}],\"GRID\":{\"status\":\"Active\",\"currentPower\":1.09},\"LOAD\":{\"status\":\"Active\",\"currentPower\":0.25},\"PV\":{\"status\":\"Active\",\"currentPower\":6.34},\"STORAGE\":{\"status\":\"Charging\",\"currentPower\":5.0,\"chargeLevel\":93,\"critical\":false}}}")]
    public void CanDeserializeCloudApiValue(string jsonString)
    {
        var currentValuesService = Mock.Create<Plugins.SolarEdge.Services.CurrentValuesService>();

        var value = currentValuesService.GetCloudApiValueFromString(jsonString);
        Assert.NotNull(value);
    }
}