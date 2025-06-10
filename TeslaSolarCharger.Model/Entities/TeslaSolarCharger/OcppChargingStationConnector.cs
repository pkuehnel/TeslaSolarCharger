using TeslaSolarCharger.Shared.Enums;

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
    public bool ShouldBeManaged { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
    public int? MinCurrent { get; set; }
    public int? SwitchOnAtCurrent { get; set; }
    public int? SwitchOffAtCurrent { get; set; }
    public int? MaxCurrent { get; set; }
    public int? ConnectedPhasesCount { get; set; }
    public int ChargingPriority { get; set; }
    public TimeSpan? PhaseSwitchCoolDownTime { get; set; }

    public int OcppChargingStationId { get; set; }

    public OcppChargingStation OcppChargingStation { get; set; } = null!;

    public List<OcppChargingStationConnectorValueLog> OcppChargingStationConnectorValueLogs { get; set; } = new();
    public List<ChargingProcess> ChargingProcesses { get; set; } = new();
}
