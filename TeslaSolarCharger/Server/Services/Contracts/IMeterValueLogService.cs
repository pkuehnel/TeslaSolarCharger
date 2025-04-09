namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueLogService
{
    void AdPvValuesToBuffer();
    Task SaveBufferdMeterValuesToDatabase();
}
