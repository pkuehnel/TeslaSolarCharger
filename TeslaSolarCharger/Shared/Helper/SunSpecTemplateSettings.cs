using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public enum SunSpecControlConfigKind
{
    /// <summary>
    /// No user configurable control parameter.
    /// </summary>
    None,
    /// <summary>
    /// Charge rate in percent (SunSpec model 124 OutWRte).
    /// </summary>
    ChargeRatePercent,
    /// <summary>
    /// Charge power in watts (plain register charge setpoint, e.g. Kostal).
    /// </summary>
    ChargePowerW,
    /// <summary>
    /// Discharge power in watts (plain register discharge limit restored in normal mode, e.g. SolarEdge).
    /// </summary>
    DischargePowerW,
}

public record SunSpecTemplateDefaults(int Port, int UnitId, bool SupportsHomeBatteryControl, SunSpecControlConfigKind ControlConfigKind);

/// <summary>
/// Client and server side defaults for all vendors that are configured via
/// <see cref="Dtos.TemplateConfiguration.Generic.DtoSunSpecTemplateValueConfiguration"/>. The server side value maps
/// are located in SunSpecTemplateDefinitions.
/// </summary>
public static class SunSpecTemplateSettings
{
    private static readonly Dictionary<TemplateValueGatherType, SunSpecTemplateDefaults> Defaults = new()
    {
        { TemplateValueGatherType.SunSpecInverter, new(502, 1, true, SunSpecControlConfigKind.ChargeRatePercent) },
        { TemplateValueGatherType.SunSpecMeter, new(502, 1, false, SunSpecControlConfigKind.None) },
        { TemplateValueGatherType.FroniusGen24, new(502, 1, true, SunSpecControlConfigKind.ChargeRatePercent) },
        //Kostal uses port 1502, unit 71 and plain register battery control
        { TemplateValueGatherType.KostalPlenticoreGen2, new(1502, 71, true, SunSpecControlConfigKind.ChargePowerW) },
        //SolarEdge uses port 1502; the hybrid variant controls the battery via a discharge limit
        { TemplateValueGatherType.SolarEdgeInverter, new(1502, 1, false, SunSpecControlConfigKind.None) },
        { TemplateValueGatherType.SolarEdgeHybrid, new(1502, 1, true, SunSpecControlConfigKind.DischargePowerW) },
    };

    public static bool IsSunSpecType(TemplateValueGatherType gatherType) => Defaults.ContainsKey(gatherType);

    public static SunSpecTemplateDefaults GetDefaults(TemplateValueGatherType gatherType)
    {
        if (!Defaults.TryGetValue(gatherType, out var defaults))
        {
            throw new ArgumentOutOfRangeException(nameof(gatherType), gatherType, "No SunSpec template defaults available");
        }
        return defaults;
    }

    public static IReadOnlyCollection<TemplateValueGatherType> SunSpecTypes => Defaults.Keys;
}
