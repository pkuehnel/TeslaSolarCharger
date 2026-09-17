namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Where in a charging station's journey the user is. A charger is set up once and then reused by the cars that
/// charge on it.
/// </summary>
public enum SetupChargerStage
{
    /// <summary>Entering the connection address in the charger and waiting for it to report in.</summary>
    Connect = 0,

    /// <summary>Connector names, installation limits, which cars may charge here.</summary>
    Settings = 1,

    Review = 2,
}
