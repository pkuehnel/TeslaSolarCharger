namespace TeslaSolarCharger.Shared.Dtos.ChargingStation;

public class DtoChargingStation
{
    public DtoChargingStation(string chargepointId)
    {
        ChargepointId = chargepointId;
    }

    public int Id { get; set; }
    public string ChargepointId { get; set; }
    public bool IsConnected { get; set; }
    public bool? CanSwitchBetween1And3Phases { get; set; }
}
