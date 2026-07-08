using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public record SunSpecTemplateDefaults(int Port, int UnitId, bool SupportsHomeBatteryControl, bool UsesPlainRegisterControl);

/// <summary>
/// Client and server side defaults for all vendors that are configured via
/// <see cref="Dtos.TemplateConfiguration.Generic.DtoSunSpecTemplateValueConfiguration"/>. The server side value maps
/// are located in SunSpecTemplateDefinitions.
/// </summary>
public static class SunSpecTemplateSettings
{
    private static readonly Dictionary<TemplateValueGatherType, SunSpecTemplateDefaults> Defaults = new()
    {
        { TemplateValueGatherType.SunSpecInverter, new(502, 1, true, false) },
        { TemplateValueGatherType.SunSpecMeter, new(502, 1, false, false) },
        { TemplateValueGatherType.FroniusGen24, new(502, 1, true, false) },
        //Kostal uses port 1502, unit 71 and plain register battery control
        { TemplateValueGatherType.KostalPlenticoreGen2, new(1502, 71, true, true) },
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
