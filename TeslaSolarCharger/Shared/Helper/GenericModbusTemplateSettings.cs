using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public record GenericModbusTemplateDefaults(int Port, int UnitId, bool SupportsHomeBatteryControl,
    int DefaultMaxChargePowerW, int DefaultMaxDischargePowerW);

/// <summary>
/// Client and server side defaults for all vendors that are configured via
/// <see cref="Dtos.TemplateConfiguration.Generic.DtoGenericModbusTemplateValueConfiguration"/>. The server side
/// register maps are located in ModbusTemplateDefinitions.
/// </summary>
public static class GenericModbusTemplateSettings
{
    private static readonly Dictionary<TemplateValueGatherType, GenericModbusTemplateDefaults> Defaults = new()
    {
        { TemplateValueGatherType.SungrowHybridInverterModbus, new(502, 1, true, 5000, 5000) },
        { TemplateValueGatherType.SungrowInverterModbus, new(502, 1, false, 0, 0) },
        { TemplateValueGatherType.HuaweiSun2000HybridInverterModbus, new(502, 1, true, 5000, 5000) },
        { TemplateValueGatherType.HuaweiSun2000InverterModbus, new(502, 1, false, 0, 0) },
        { TemplateValueGatherType.GoodweHybridInverterModbus, new(502, 247, true, 10000, 10000) },
        { TemplateValueGatherType.GoodweDtInverterModbus, new(502, 247, false, 0, 0) },
        { TemplateValueGatherType.GrowattSphHybridInverterModbus, new(502, 1, true, 4200, 4200) },
        { TemplateValueGatherType.GrowattTlxhHybridInverterModbus, new(502, 1, true, 4200, 4200) },
        { TemplateValueGatherType.DeyeHybridInverterModbus, new(502, 1, false, 0, 0) },
        { TemplateValueGatherType.FoxEssH3HybridInverterModbus, new(502, 247, true, 4200, 4200) },
        { TemplateValueGatherType.SolaxHybridInverterModbus, new(502, 1, true, 4200, 4200) },
        { TemplateValueGatherType.AlphaEssSmileModbus, new(502, 85, true, 4200, 4200) },
        { TemplateValueGatherType.SajH2HybridInverterModbus, new(502, 1, true, 4200, 4200) },
        { TemplateValueGatherType.MarstekVenusModbus, new(502, 1, true, 2500, 2500) },
        { TemplateValueGatherType.SmaSunnyBoyStorageModbus, new(502, 3, true, 4200, 4200) },
    };

    public static bool IsGenericModbusType(TemplateValueGatherType gatherType) => Defaults.ContainsKey(gatherType);

    public static GenericModbusTemplateDefaults GetDefaults(TemplateValueGatherType gatherType)
    {
        if (!Defaults.TryGetValue(gatherType, out var defaults))
        {
            throw new ArgumentOutOfRangeException(nameof(gatherType), gatherType, "No generic modbus template defaults available");
        }
        return defaults;
    }

    public static IReadOnlyCollection<TemplateValueGatherType> GenericModbusTypes => Defaults.Keys;
}
