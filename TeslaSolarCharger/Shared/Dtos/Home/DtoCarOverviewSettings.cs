using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarOverviewSettings
{
    public DtoCarOverviewSettings(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    [Postfix("%")]
    public int? MinSoc { get; set; }
    [Postfix("%")]
    public int? MaxSoc { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
    public CarType CarType { get; set; }
}

public class DtoCarOverviewState
{
    public bool? IsOnline { get; set; }
    public int? Soc { get; set; }
    public int? CarSideSocLimit { get; set; }
    public bool IsCharging { get; set; }
    public bool IsHome { get; set; }
    public bool IsPluggedIn { get; set; }
    /// <summary>
    /// BLE sleep window phase of the car, or null if the car is not collecting its data via BLE or the feature is off.
    /// </summary>
    public BleSleepPhase? BleSleepPhase { get; set; }
    /// <summary>Seconds until the next BLE sleep transition (see <see cref="BleSleepPhase"/>), or null.</summary>
    public int? BleSleepCountdownSeconds { get; set; }
    /// <summary>True while the user can cancel the current BLE sleep attempt (car awake and being silenced).</summary>
    public bool CanCancelBleSleep { get; set; }
}
