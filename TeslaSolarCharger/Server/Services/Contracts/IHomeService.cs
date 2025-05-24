using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>> GetLoadPointOverviews();
    Task<List<DtoCarChargingSchedule>> GetCarChargingSchedules(int carId);
    Task<Result<int>> SaveCarChargingSchedule(int carId, DtoCarChargingSchedule dto);
    Task UpdateCarMinSoc(int carId, int newMinSoc);
}
