namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IChargingService
{
    Task SetNewChargingValues(bool onlyUpdateValues = false);
}