using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

/// <summary>
/// Which manufacturer each supported integration belongs to. Kept in one place because both the detailed editor and
/// the setup assistant offer the same list, and a device that appears under two different brand names in the two
/// places would look like two different products.
/// </summary>
public static class TemplateValueGatherTypeVendors
{
    public static string GetVendor(TemplateValueGatherType type) => type switch
    {
        TemplateValueGatherType.SmaEnergyMeter
            or TemplateValueGatherType.SmaInverterModbus
            or TemplateValueGatherType.SmaHybridInverterModbus
            or TemplateValueGatherType.SmaSunnyBoyStorageModbus
            or TemplateValueGatherType.SmaSunnyIslandModbus
            or TemplateValueGatherType.SmaDataManagerModbus
            or TemplateValueGatherType.SmaWebBoxModbus => "SMA",
        TemplateValueGatherType.TeslaPowerwallFleetApi => "Tesla",
        TemplateValueGatherType.SolaxApi
            or TemplateValueGatherType.SolaxHybridInverterModbus => "Solax",
        TemplateValueGatherType.KostalHybridInverterModbus
            or TemplateValueGatherType.KostalKsemInverterModbus
            or TemplateValueGatherType.KostalPikoApi
            or TemplateValueGatherType.KostalPlenticoreGen2 => "Kostal",
        TemplateValueGatherType.SungrowHybridInverterModbus
            or TemplateValueGatherType.SungrowInverterModbus
            or TemplateValueGatherType.SungrowIhmModbus => "Sungrow",
        TemplateValueGatherType.HuaweiSun2000HybridInverterModbus
            or TemplateValueGatherType.HuaweiSun2000InverterModbus
            or TemplateValueGatherType.HuaweiSmartLoggerModbus
            or TemplateValueGatherType.HuaweiEmmaModbus => "Huawei",
        TemplateValueGatherType.GoodweHybridInverterModbus
            or TemplateValueGatherType.GoodweDtInverterModbus => "GoodWe",
        TemplateValueGatherType.GrowattSphHybridInverterModbus
            or TemplateValueGatherType.GrowattTlxhHybridInverterModbus => "Growatt",
        TemplateValueGatherType.DeyeHybridInverterModbus
            or TemplateValueGatherType.DeyeStorageModbus
            or TemplateValueGatherType.DeyeStringInverterModbus => "Deye",
        TemplateValueGatherType.FoxEssH3HybridInverterModbus
            or TemplateValueGatherType.FoxEssH1Modbus
            or TemplateValueGatherType.FoxEssH3SmartModbus
            or TemplateValueGatherType.FoxEssAvocadoModbus => "FoxESS",
        TemplateValueGatherType.AlphaEssSmileModbus => "Alpha ESS",
        TemplateValueGatherType.SajH2HybridInverterModbus => "SAJ",
        TemplateValueGatherType.MarstekVenusModbus => "Marstek",
        TemplateValueGatherType.FroniusSolarApiV1
            or TemplateValueGatherType.FroniusGen24 => "Fronius",
        TemplateValueGatherType.SofarSolarModbus
            or TemplateValueGatherType.SofarSolarG3HybridModbus => "Sofar Solar",
        TemplateValueGatherType.VartaModbus => "Varta",
        TemplateValueGatherType.SaxPowerModbus => "SAX",
        TemplateValueGatherType.MtecEbGen3Modbus => "M-TEC",
        TemplateValueGatherType.SolarmaxMaxStorageModbus
            or TemplateValueGatherType.SolarmaxSmtInverterModbus => "Solarmax",
        TemplateValueGatherType.VictronGxModbus => "Victron",
        TemplateValueGatherType.IntilionScaleblocModbus => "Intilion",
        TemplateValueGatherType.SiemensJunelightModbus => "Siemens",
        TemplateValueGatherType.StoraxeModbus => "Ads-tec",
        TemplateValueGatherType.SolintegModbus => "Solinteg",
        TemplateValueGatherType.AforeHybridModbus => "Afore",
        TemplateValueGatherType.AnkerSolixX1Modbus => "Anker",
        TemplateValueGatherType.IbcHomeOneModbus => "IBC",
        TemplateValueGatherType.EcoflowPowerOceanModbus => "EcoFlow",
        TemplateValueGatherType.SenergyInverterModbus => "Senergy",
        TemplateValueGatherType.SolarlogModbus => "Solar-Log",
        TemplateValueGatherType.PlexlogModbus => "Plexlog",
        TemplateValueGatherType.PowerdogModbus => "Powerdog",
        TemplateValueGatherType.SonnenBatterieApi => "Sonnen",
        TemplateValueGatherType.SessySmartBatteryApi => "Sessy",
        TemplateValueGatherType.BatterXApi => "batterX",
        TemplateValueGatherType.ApsystemsEz1Api => "APsystems",
        TemplateValueGatherType.HoymilesOpenDtuApi
            or TemplateValueGatherType.HoymilesAhoyDtuApi
            or TemplateValueGatherType.HoymilesDtuGatewayApi => "Hoymiles",
        TemplateValueGatherType.SmartfoxApi => "Smartfox",
        TemplateValueGatherType.SunSpecInverter
            or TemplateValueGatherType.SunSpecMeter => "SunSpec (generic)",
        TemplateValueGatherType.SolarEdgeInverter
            or TemplateValueGatherType.SolarEdgeHybrid => "SolarEdge",
        _ => "Other",
    };
}
