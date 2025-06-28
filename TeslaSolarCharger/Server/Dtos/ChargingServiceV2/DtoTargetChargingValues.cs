using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoTargetChargingValues
{
    public DtoTargetChargingValues(DtoLoadPointOverview loadPoint)
    {
        LoadPoint = loadPoint;
    }

    public DtoLoadPointOverview LoadPoint { get; set; }
    public TargetValues? TargetValues { get; set; }
}

public class TargetValues
{
    public bool StartCharging { get; set; }
    public bool StopCharging { get; set; }
    public decimal? TargetCurrent { get; set; }
    public int? TargetPhases { get; set; }
}
