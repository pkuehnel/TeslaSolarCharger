namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class OcppChargingStation
{
    public OcppChargingStation(string chargepointId)
    {
        ChargepointId = chargepointId;
    }

    public int Id { get; set; }
    public string ChargepointId { get; set; }
    public int ConfigurationVersion { get; set; }
    public bool? CanSwitchBetween1And3Phases { get; set; }
    public int? MaxCurrent { get; set; }

    public List<OcppChargingStationConnector> Connectors { get; set; } = new();
}
