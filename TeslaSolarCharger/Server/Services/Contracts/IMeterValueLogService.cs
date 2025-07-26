namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueLogService
{
    Task AddPvValuesToBuffer();
    Task SaveBufferedMeterValuesToDatabase();
}
