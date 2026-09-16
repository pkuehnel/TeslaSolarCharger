using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Helper.Contracts;

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

    /// <summary>
    /// The make and model as a person would look for it, for example "SMA Hybrid Inverter Modbus".
    /// </summary>
    public static string GetDisplayName(TemplateValueGatherType type, IStringHelper stringHelper)
    {
        var vendor = GetVendor(type);
        var model = stringHelper.GenerateFriendlyStringFromPascalString(type.ToString());
        //The vendor is usually the start of the model name too, so it is not repeated, but spelled the way the
        //manufacturer spells it ("SMA", not "Sma").
        return model.StartsWith(vendor, StringComparison.OrdinalIgnoreCase)
            ? vendor + model[vendor.Length..]
            : $"{vendor} {model}";
    }

    /// <summary>
    /// A name for a newly connected device, so nobody has to invent a label before they can connect anything. A second
    /// device of the same kind is numbered, because two identical entries in a list cannot be told apart.
    /// </summary>
    public static string SuggestName(TemplateValueGatherType type, IEnumerable<string?> existingNames, IStringHelper stringHelper)
    {
        var baseName = GetDisplayName(type, stringHelper);
        var takenNames = new HashSet<string>(
            existingNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var candidate = baseName;
        for (var number = 2; takenNames.Contains(candidate); number++)
        {
            candidate = $"{baseName} {number}";
        }

        return candidate;
    }
}
