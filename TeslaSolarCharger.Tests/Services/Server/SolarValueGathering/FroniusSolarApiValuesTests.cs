using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.ValueSetupServices.Fronius;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.SolarValueGathering;

/// <summary>
/// The values taken from a Fronius Solar API GetPowerFlowRealtimeData response.
/// </summary>
public class FroniusSolarApiValuesTests
{
    private static JObject Response(string pv, string grid, string akku, string? soc) => JObject.Parse($$"""
        {
          "Body": {
            "Data": {
              "Site": { "P_PV": {{pv}}, "P_Grid": {{grid}}, "P_Akku": {{akku}}, "P_Load": -400 },
              "Inverters": { "1": { "P": 100{{(soc == null ? "" : $", \"SOC\": {soc}")}} } }
            }
          }
        }
        """);

    private static decimal? Value(JObject response, ValueUsage usage)
    {
        var values = FroniusSolarApiSetupService.GetValues(response).Where(v => v.Key.ValueUsage == usage).ToList();
        return values.Count == 0 ? null : Assert.Single(values).Value;
    }

    [Fact]
    public void AHybridInverterDeliversEveryValueInTscSignConvention()
    {
        var response = Response("3000", "-1200", "500", "57.3");

        Assert.Equal(3000, Value(response, ValueUsage.InverterPower));
        //Fronius grid is import positive, TSC grid is export positive
        Assert.Equal(1200, Value(response, ValueUsage.GridPower));
        //Fronius battery is discharge positive, TSC battery is charge positive
        Assert.Equal(-500, Value(response, ValueUsage.HomeBatteryPower));
        Assert.Equal(57.3m, Value(response, ValueUsage.HomeBatterySoc));
    }

    [Fact]
    public void AnInverterWithoutMeterAndBatteryOnlyDeliversProduction()
    {
        var values = FroniusSolarApiSetupService.GetValues(Response("2500", "null", "null", null));

        var value = Assert.Single(values);
        Assert.Equal(ValueUsage.InverterPower, value.Key.ValueUsage);
        Assert.Equal(2500, value.Value);
    }

    [Fact]
    public void NoProductionIsZero()
    {
        //Fronius reports null for P_PV while the inverter does not produce.
        Assert.Equal(0, Value(Response("null", "-100", "null", null), ValueUsage.InverterPower));
    }

    [Fact]
    public void AnIdleBatteryWithoutPowerIsZero()
    {
        var response = Response("0", "300", "null", "80");

        Assert.Equal(0, Value(response, ValueUsage.HomeBatteryPower));
        Assert.Equal(80, Value(response, ValueUsage.HomeBatterySoc));
    }

    [Fact]
    public void AMeterWithoutBatteryDeliversGridButNoBatteryValues()
    {
        var response = Response("1000", "200", "null", null);

        Assert.Equal(-200, Value(response, ValueUsage.GridPower));
        Assert.Null(Value(response, ValueUsage.HomeBatteryPower));
        Assert.Null(Value(response, ValueUsage.HomeBatterySoc));
    }

    [Fact]
    public void AResponseWithoutSiteDataCanNotBeUsed()
    {
        Assert.Throws<InvalidDataException>(() => FroniusSolarApiSetupService.GetValues(JObject.Parse("""{ "Body": { "Data": {} } }""")));
    }
}
