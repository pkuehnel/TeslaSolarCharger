namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DtoLoadpoint
{
    public DtoCar? Car { get; set; }
    public int? OcppConnectorId { get; set; }
    public DtoOcppConnectorState? OcppConnectorState { get; set; }
    public int Priority { get; set; }

    public int? ActualChargingPower
    {
        get
        {
            if (OcppConnectorState != default)
            {
                return OcppConnectorState.ChargingPower.Value;
            }
            return Car?.ChargingPowerAtHome;
        }
    }

    public int? ActualVoltage
    {
        get
        {
            if (OcppConnectorState != default)
            {
                return (int)OcppConnectorState.ChargingVoltage.Value;
            }
            return Car?.ChargerVoltage;
        }
    }

    public decimal? ActualCurrent
    {
        get
        {
            if (OcppConnectorState != default)
            {
                return (int)OcppConnectorState.ChargingCurrent.Value;
            }
            return Car?.ChargerActualCurrent;
        }
    }

    public int? ActualPhases
    {
        get
        {
            if (OcppConnectorState != default)
            {
                return OcppConnectorState.PhaseCount.Value ?? Car?.ActualPhases;
            }
            return Car?.ActualPhases ?? 3;
        }
    }
}
