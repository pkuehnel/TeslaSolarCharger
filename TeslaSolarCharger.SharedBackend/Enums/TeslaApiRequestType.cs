namespace TeslaSolarCharger.SharedBackend.Enums;

public enum TeslaApiRequestType
{
    Vehicle,
    VehicleData,
    Command,
    WakeUp,
    Charging,
    Other,
    /// <summary>
    /// Fleet API access test of a car, see the Solar4Car backend's FleetApiRequests/TestFleetApiAccess.
    /// </summary>
    FleetApiTest,
}
