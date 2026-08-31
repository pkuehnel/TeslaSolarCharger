using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Helper;

public record GenericRestTemplateDefaults(int Port, bool RequiresCredentials, bool UsesApiToken, bool ShowDeviceId,
    bool SupportsHomeBatteryControl, int DefaultMaxChargePowerW);

/// <summary>
/// Client and server side defaults for all vendors that are configured via
/// <see cref="Dtos.TemplateConfiguration.Generic.DtoGenericRestTemplateValueConfiguration"/>. The server side
/// value maps are located in JsonRestTemplateDefinitions.
/// </summary>
public static class GenericRestTemplateSettings
{
    private static readonly Dictionary<TemplateValueGatherType, GenericRestTemplateDefaults> Defaults = new()
    {
        { TemplateValueGatherType.SonnenBatterieApi, new(8080, false, true, false, true, 3300) },
        { TemplateValueGatherType.SessySmartBatteryApi, new(80, true, false, false, true, 2200) },
        { TemplateValueGatherType.BatterXApi, new(80, false, false, false, true, 3300) },
        { TemplateValueGatherType.ApsystemsEz1Api, new(8050, false, false, false, false, 0) },
        { TemplateValueGatherType.HoymilesOpenDtuApi, new(80, false, false, false, false, 0) },
        { TemplateValueGatherType.HoymilesAhoyDtuApi, new(80, false, false, true, false, 0) },
        { TemplateValueGatherType.HoymilesDtuGatewayApi, new(80, false, false, false, false, 0) },
        { TemplateValueGatherType.KostalPikoApi, new(80, false, false, false, false, 0) },
        { TemplateValueGatherType.SmartfoxApi, new(80, false, false, false, false, 0) },
    };

    public static bool IsGenericRestType(TemplateValueGatherType gatherType) => Defaults.ContainsKey(gatherType);

    public static GenericRestTemplateDefaults GetDefaults(TemplateValueGatherType gatherType)
    {
        if (!Defaults.TryGetValue(gatherType, out var defaults))
        {
            throw new ArgumentOutOfRangeException(nameof(gatherType), gatherType, "No generic rest template defaults available");
        }
        return defaults;
    }

    public static IReadOnlyCollection<TemplateValueGatherType> GenericRestTypes => Defaults.Keys;
}
