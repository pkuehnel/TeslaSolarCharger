namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IEnergyDataService
{
    Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted,
        bool useCache);
    Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted,
        bool useCache);
    Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted,
        bool useCache);
    Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted,
        bool useCache);
}
