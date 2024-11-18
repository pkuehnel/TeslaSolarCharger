using TeslaSolarCharger.Shared.Dtos.TimeSeries;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ITimeSeriesDataService
{
    Task<List<DtoTimeSeriesDatum>> GetTimeSeriesData(int carId, long startEpoch, long endEpoch, CarValueType carValueType);
}
