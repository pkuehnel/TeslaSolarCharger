using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.TimeSeries;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class TimeSeriesDataController(ITimeSeriesDataService service) : ApiBaseController
{
    [HttpGet]
    public Task<List<DtoTimeSeriesDatum>> GetTimeSeriesData(int carId, long startEpoch, long endEpoch, CarValueType carValueType) =>
        service.GetTimeSeriesData(carId, startEpoch, endEpoch, carValueType);
}
