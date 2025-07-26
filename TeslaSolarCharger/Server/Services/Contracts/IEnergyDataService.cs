namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IEnergyDataService
{
    Task<Dictionary<DateTimeOffset, int>> GetPredictedSolarProductionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted);
    Task<Dictionary<DateTimeOffset, int>> GetPredictedHouseConsumptionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted);
    Task<Dictionary<DateTimeOffset, int>> GetActualSolarProductionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted);
    Task<Dictionary<DateTimeOffset, int>> GetActualHouseConsumptionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted);

    Task RefreshCachedValues(CancellationToken contextCancellationToken);
    Task<Dictionary<DateTimeOffset, int>> GetPredictedSurplusPerSlice(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken cancellationToken);
}
