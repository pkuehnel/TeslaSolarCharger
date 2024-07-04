namespace TeslaSolarCharger.Server.Dtos.TscBackend;

public class DtoTeslaApiCallStatistic
{
    public DateOnly Date { get; set; }
    public Guid InstallationId { get; set; }
    public bool GetDataFromTesla { get; set; }
    public DateTime StartupTime { get; set; }
    public string Vin { get; set; }
    public bool UseBle { get; set; }
    public int ApiRefreshInterval { get; set; }
    public List<DateTime> WakeUpCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleDataCalls { get; set; } = new List<DateTime>();
    public List<DateTime> VehicleCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStartCalls { get; set; } = new List<DateTime>();
    public List<DateTime> ChargeStopCalls { get; set; } = new List<DateTime>();
    public List<DateTime> SetChargingAmpsCall { get; set; } = new List<DateTime>();
    public List<DateTime> OtherCommandCalls { get; set; } = new List<DateTime>();
}
