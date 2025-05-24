using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints();
    Task<List<DtoCarChargingSchedule>?> GetCarChargingSchedules(int carId);
    Task<Result<Result<int>>> SaveCarChargingSchedule(int carId, DtoCarChargingSchedule dto);
}
