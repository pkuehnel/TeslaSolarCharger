using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints();
    Task<List<DtoCarChargingSchedule>?> GetCarChargingSchedules(int carId);
    Task<Result<Result<int>>> SaveCarChargingSchedule(int carId, DtoCarChargingSchedule dto);
    Task UpdateCarMinSoc(int carId, int minSoc);
    Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId);
    Task<DtoCarChargingSchedule?> GetChargingSchedule(int chargingScheduleId);
}
