using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Helper;

public static class LoadPointOverviewExtensions
{
    /// <summary>
    /// Chargers report a few watts of standby power while idle, which must not count as drawing power.
    /// </summary>
    public const int MinPowerToCountAsDrawingPower = 100;

    /// <summary>
    /// True when the loadpoint draws real power, which includes preconditioning, as the charger is switched on then as well.
    /// </summary>
    public static bool IsDrawingPower(this DtoLoadPointOverview loadPoint)
    {
        return loadPoint.ChargingPower >= MinPowerToCountAsDrawingPower;
    }
}
