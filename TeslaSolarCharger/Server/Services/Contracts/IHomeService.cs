using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoCarChargingTarget>> GetCarChargingTargets(int carId);
    Task<Result<int>> SaveCarChargingTarget(int carId, DtoCarChargingTarget dto);
    Task UpdateCarMinSoc(int carId, int newMinSoc);
    Task<DtoCarChargingTarget> GetChargingTarget(int chargingTargetId);
    Task DeleteCarChargingTarget(int chargingTargetId);
}
