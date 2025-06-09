using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoCarChargingTarget>> GetCarChargingTargets(int carId);
    Task<Result<int>> SaveCarChargingTarget(int carId, DtoCarChargingTarget dto);
    Task UpdateCarMinSoc(int carId, int newMinSoc);
    Task<DtoCarChargingTarget> GetChargingTarget(int chargingTargetId);
    Task DeleteCarChargingTarget(int chargingTargetId);
    Task<DtoCarOverview> GetCarOverview(int carId);
    Task<DtoChargingConnectorOverview> GetChargingConnectorOverview(int chargingConnectorId);
    List<DtoChargingSchedule> GetChargingSchedules(int? carId, int? chargingConnectorId);
    Task UpdateCarChargeMode(int carId, ChargeModeV2 chargeMode);
    Task UpdateChargingConnectorChargeMode(int chargingConnectorId, ChargeModeV2 chargeMode);
    Task StartChargingConnectorCharging(int chargingConnectorId, int currentToSet, int? numberOfPhases, CancellationToken cancellationToken);
    Task SetChargingConnectorCurrent(int chargingConnectorId, int currentToSet, int? numberOfPhases, CancellationToken cancellationToken);
    Task StopChargingConnectorCharging(int chargingConnectorId, CancellationToken cancellationToken);
    Task UpdateCarMaxSoc(int carId, int newSoc);
}
