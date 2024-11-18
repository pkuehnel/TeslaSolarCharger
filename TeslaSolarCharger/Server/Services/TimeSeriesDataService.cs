using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.TimeSeries;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Server.Services;

public class TimeSeriesDataService(ILogger<TimeSeriesDataService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IMapperConfigurationFactory mapperConfigurationFactory) : ITimeSeriesDataService
{
    public async Task<List<DtoTimeSeriesDatum>> GetTimeSeriesData(int carId, long startEpoch, long endEpoch, CarValueType carValueType)
    {
        logger.LogTrace("{method}({carId}, {startEpoch}, {endEpoch}, {carValueType})", nameof(GetTimeSeriesData), carId, startEpoch, endEpoch, carValueType);
        var startDate = DateTimeOffset.FromUnixTimeSeconds(startEpoch).DateTime;
        var endDate = DateTimeOffset.FromUnixTimeSeconds(endEpoch).DateTime;

        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<CarValueLog, DtoTimeSeriesDatum>()
                .ForMember(d => d.Timestamp, opt => opt.MapFrom(c => c.Timestamp.ToLocalTime()))
                .ForMember(d => d.Value, opt => opt.MapFrom(c => c.IntValue ?? c.DoubleValue))
                ;
        });

        var result = await teslaSolarChargerContext.CarValueLogs
            .Where(c => c.CarId == carId)
            .Where(c => c.Timestamp >= startDate && c.Timestamp <= endDate)
            .Where(c => c.Type == carValueType)
            .OrderBy(c => c.Timestamp)
            .ProjectTo<DtoTimeSeriesDatum>(mapper)
            .ToListAsync();

        return result;
    }
}
