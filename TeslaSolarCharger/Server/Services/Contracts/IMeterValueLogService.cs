namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueLogService
{
    void AddPvValuesToBuffer();
    Task SaveBufferdMeterValuesToDatabase();
}
