using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest;

/// <summary>
/// JSON REST value maps per vendor, transcribed from the evcc templates
/// (https://github.com/evcc-io/evcc/tree/master/templates/definition/meter).
/// Sign conventions in TSC: GridPower positive = export to grid, HomeBatteryPower positive = charging.
/// evcc uses the opposite conventions, so operators are flipped accordingly.
/// </summary>
public static class JsonRestTemplateDefinitions
{
    public static IReadOnlyDictionary<TemplateValueGatherType, JsonRestTemplateDefinition> Definitions { get; } = new Dictionary<TemplateValueGatherType, JsonRestTemplateDefinition>()
    {
        //evcc template sonnenbatterie. Battery control requires the JSON Write API to be enabled in the web
        //interface (under software integration) and the generated API token. Normal mode restores self consumption,
        //the time of use default mode is not supported.
        {
            TemplateValueGatherType.SonnenBatterieApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}:{port}/api/v1/status",
                        Values = new()
                        {
                            //GridFeedIn_W: positive = export
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.GridFeedIn_W" },
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.Production_W" },
                            //Pac_total_W: positive = discharging
                            new() { UsedFor = ValueUsage.HomeBatteryPower, JsonPath = "$.Pac_total_W", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.HomeBatterySoc, JsonPath = "$.USOC" },
                        },
                    },
                },
                BatteryControl = new()
                {
                    AuthType = JsonRestAuthType.TokenHeader,
                    TokenHeaderName = "Auth-Token",
                    NormalRequests = new()
                    {
                        //EM_OperatingMode 2 = self consumption
                        RestBatteryModeRequest.Put("http://{host}/api/v2/configurations", """{"EM_OperatingMode":"2"}"""),
                    },
                    HoldRequests = new()
                    {
                        //EM_OperatingMode 1 = manual
                        RestBatteryModeRequest.Put("http://{host}/api/v2/configurations", """{"EM_OperatingMode":"1"}"""),
                        RestBatteryModeRequest.Post("http://{host}/api/v2/setpoint/discharge/0"),
                        RestBatteryModeRequest.Post("http://{host}/api/v2/setpoint/charge/0"),
                    },
                    ChargeRequests = new()
                    {
                        RestBatteryModeRequest.Put("http://{host}/api/v2/configurations", """{"EM_OperatingMode":"1"}"""),
                        RestBatteryModeRequest.Post("http://{host}/api/v2/setpoint/discharge/0"),
                        RestBatteryModeRequest.Post("http://{host}/api/v2/setpoint/charge/{maxChargePowerW}"),
                    },
                },
            }
        },
        //evcc template sessy-smart-battery (battery only device)
        {
            TemplateValueGatherType.SessySmartBatteryApi, new()
            {
                ValueAuthType = JsonRestAuthType.Basic,
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}/api/v1/power/status",
                        Values = new()
                        {
                            //sessy.power: positive = discharging
                            new() { UsedFor = ValueUsage.HomeBatteryPower, JsonPath = "$.sessy.power", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.HomeBatterySoc, JsonPath = "$.sessy.state_of_charge", CorrectionFactor = 100 },
                        },
                    },
                },
                BatteryControl = new()
                {
                    AuthType = JsonRestAuthType.Basic,
                    NormalRequests = new()
                    {
                        RestBatteryModeRequest.Post("http://{host}/api/v1/power/active_strategy", """{"strategy": "POWER_STRATEGY_NOM"}"""),
                    },
                    HoldRequests = new()
                    {
                        RestBatteryModeRequest.Post("http://{host}/api/v1/power/active_strategy", """{"strategy": "POWER_STRATEGY_API"}"""),
                        RestBatteryModeRequest.Post("http://{host}/api/v1/power/setpoint", """{"setpoint": 0}"""),
                    },
                    ChargeRequests = new()
                    {
                        RestBatteryModeRequest.Post("http://{host}/api/v1/power/active_strategy", """{"strategy": "POWER_STRATEGY_API"}"""),
                        //Negative setpoint charges the battery
                        RestBatteryModeRequest.Post("http://{host}/api/v1/power/setpoint", """{"setpoint": -{maxChargePowerW}}"""),
                    },
                },
            }
        },
        //evcc template batterx (batterX Home). A second external solar inverter is not supported.
        {
            TemplateValueGatherType.BatterXApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}:{port}/api.php?get=currentstate",
                        Values = new()
                        {
                            //2913.0 grid meter total power: positive = import
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$['2913']['0']", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$['1634']['0']" },
                            //1121.1 battery power: positive = charging
                            new() { UsedFor = ValueUsage.HomeBatteryPower, JsonPath = "$['1121']['1']" },
                            new() { UsedFor = ValueUsage.HomeBatterySoc, JsonPath = "$['1074']['1']" },
                        },
                    },
                },
                BatteryControl = new()
                {
                    NormalRequests = new()
                    {
                        //type 20738 text1 3 = AC charging, text1 4 = discharging
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=3&text2=0"),
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=4&text2=1"),
                    },
                    HoldRequests = new()
                    {
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=3&text2=0"),
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=4&text2=0"),
                    },
                    ChargeRequests = new()
                    {
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=3&text2=1"),
                        RestBatteryModeRequest.Get("http://{host}:{port}/api.php?set=command&type=20738&text1=4&text2=0"),
                    },
                },
            }
        },
        //evcc template apsystems-ez1
        {
            TemplateValueGatherType.ApsystemsEz1Api, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}:{port}/getOutputData",
                        Values = new()
                        {
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.data.p1" },
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.data.p2" },
                        },
                    },
                },
            }
        },
        //evcc template hoymiles-opendtu
        {
            TemplateValueGatherType.HoymilesOpenDtuApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}/api/livedata/status",
                        Values = new()
                        {
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.total.Power.v" },
                        },
                    },
                },
            }
        },
        //evcc template hoymiles-ahoydtu. The device id is the inverter number starting at 0.
        {
            TemplateValueGatherType.HoymilesAhoyDtuApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}/api/inverter/id/{deviceId}",
                        Values = new()
                        {
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.ch[0][2]" },
                        },
                    },
                },
            }
        },
        //evcc template hoymiles-dtugateway
        {
            TemplateValueGatherType.HoymilesDtuGatewayApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}/api/data.json",
                        Values = new()
                        {
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.grid.p" },
                        },
                    },
                },
            }
        },
        //evcc template kostal-piko-pv (grid values require the built-in home consumption measurement)
        {
            TemplateValueGatherType.KostalPikoApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        //Grid = inverter AC output - home consumption from PV - home consumption from grid - home consumption from battery
                        UriTemplate = "http://{host}/api/dxs.json?dxsEntries=83886336&dxsEntries=83886848&dxsEntries=83886592&dxsEntries=67109120",
                        Values = new()
                        {
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.dxsEntries[?(@.dxsId == 83886336)].value", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.dxsEntries[?(@.dxsId == 83886848)].value", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.dxsEntries[?(@.dxsId == 83886592)].value", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.dxsEntries[?(@.dxsId == 67109120)].value" },
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.dxsEntries[?(@.dxsId == 67109120)].value" },
                        },
                    },
                },
            }
        },
        //evcc template smartfox
        {
            TemplateValueGatherType.SmartfoxApi, new()
            {
                ValueReads = new()
                {
                    new()
                    {
                        UriTemplate = "http://{host}/all",
                        Values = new()
                        {
                            //power_io: positive = import
                            new() { UsedFor = ValueUsage.GridPower, JsonPath = "$.power_io", Operator = ValueOperator.Minus },
                            new() { UsedFor = ValueUsage.InverterPower, JsonPath = "$.PvPower[0]" },
                        },
                    },
                },
            }
        },
    };
}
