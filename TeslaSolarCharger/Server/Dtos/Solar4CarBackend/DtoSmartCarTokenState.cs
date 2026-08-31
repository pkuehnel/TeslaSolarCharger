namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public class DtoSmartCarTokenState
{
    public int Id { get; set; }
    public HashSet<string> Vins { get; set; } = new();
    public bool HasPendingConnections { get; set; }

    /// <summary>
    /// All licensed connections for this token, keyed by the always-known SmartCar vehicle id. The VIN
    /// is null until SmartCar delivers it via webhook, so consumers match on the vehicle id and treat
    /// the VIN as a value that backfills later.
    /// </summary>
    public List<DtoSmartCarConnectionState> Connections { get; set; } = new();
}

public class DtoSmartCarConnectionState
{
    public string SmartCarVehicleId { get; set; } = string.Empty;
    public string? Vin { get; set; }

    /// <summary>
    /// Vehicle make and model as reported by SmartCar. Known before the VIN, so a freshly connected car can be
    /// named immediately (e.g. "Tesla Model S") instead of with a placeholder. May be null if the backend's
    /// live SmartCar fetch did not return them.
    /// </summary>
    public string? Make { get; set; }
    public string? Model { get; set; }
}
