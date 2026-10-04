namespace TeslaSolarCharger.Server.Enums;

/// <summary>
/// The parts of the Solar4Car backend's Fleet API command budget.
/// </summary>
public enum FleetApiBudgetKind
{
    /// <summary>
    /// Hourly command budget of cars without Fleet API license.
    /// </summary>
    Commands,
    WakeUp,
    FleetApiTest,
}
