using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.TimeSeries;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TimeSeriesDataService(ILogger<TimeSeriesDataService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext) : ITimeSeriesDataService
{
    public async Task<List<DtoTimeSeriesDatum>> GetTimeSeriesData(int carId, long startEpoch, long endEpoch, CarValueType carValueType)
    {
        logger.LogTrace("{method}({carId}, {startEpoch}, {endEpoch}, {carValueType})", nameof(GetTimeSeriesData), carId, startEpoch, endEpoch, carValueType);
        var startDate = DateTimeOffset.FromUnixTimeSeconds(startEpoch).DateTime;
        var endDate = DateTimeOffset.FromUnixTimeSeconds(endEpoch).DateTime;

        var result = await teslaSolarChargerContext.CarValueLogs
            .Where(c => c.CarId == carId)
            .Where(c => c.Timestamp >= startDate && c.Timestamp <= endDate)
            .Where(c => c.Type == carValueType)
            .OrderBy(c => c.Timestamp)
            .Select(c => new DtoTimeSeriesDatum()
            {
                Timestamp = c.Timestamp.ToLocalTime(),
                Value = c.IntValue ?? c.DoubleValue
            })
            .ToListAsync();

        return result;
    }
}
