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
}
