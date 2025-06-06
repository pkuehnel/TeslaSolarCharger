using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints();
    Task<List<DtoCarChargingTarget>?> GetCarChargingTargets(int carId);
    Task UpdateCarMinSoc(int carId, int minSoc);
    Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId);
    Task<Result<object>> DeleteCarChargingTarget(int chargingTargetId);
}
