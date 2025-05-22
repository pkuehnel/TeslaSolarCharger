namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class OcppChargingStationConnector
{
    public OcppChargingStationConnector(string name)
    {
        Name = name;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public int ConnectorId { get; set; }
    public bool AutoSwitchBetween1And3PhasesEnabled { get; set; }
    public int? MinCurrent { get; set; }
    public int? MaxCurrent { get; set; }
    public int? ConnectedPhasesCount { get; set; }

    public int OcppChargingStationId { get; set; }

    public OcppChargingStation OcppChargingStation { get; set; } = null!;

    public List<OcppChargingStationConnectorValueLog> OcppChargingStationConnectorValueLogs { get; set; } = new();
    public List<ChargingProcess> ChargingProcesses { get; set; } = new();
}
