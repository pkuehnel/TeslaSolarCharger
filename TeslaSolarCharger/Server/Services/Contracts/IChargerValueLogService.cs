namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChargerValueLogService
{
    Task SaveBufferedChargerValuesToDatabase();
}
