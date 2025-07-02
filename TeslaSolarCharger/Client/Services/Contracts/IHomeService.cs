using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IHomeService
{
    Task<List<DtoLoadPointOverview>?> GetLoadPointsToManage();
    Task<List<DtoCarChargingTarget>?> GetCarChargingTargets(int carId);
    Task<Result<object?>> UpdateCarMinSoc(int carId, int minSoc);
    Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId);
    Task<Result<object>> DeleteCarChargingTarget(int chargingTargetId);
    Task<DtoCarOverview?> GetCarOverview(int carId);
    Task<DtoChargingConnectorOverview?> GetChargingConnectorOverview(int chargingConnectorId);
    Task<List<DtoChargingSchedule>?> GetChargingSchedules(int? carId, int? chargingConnectorId);
    Task<Result<object?>> UpdateCarChargeMode(int carId, ChargeModeV2 chargeMode);
    Task<Result<object?>> UpdateChargingConnectorChargeMode(int chargingConnectorId, ChargeModeV2 chargeMode);
    Task<Result<object?>> StartChargingConnectorCharging(int chargingConnectorId, int currentToSet, int? numberOfPhases);
    Task<Result<object?>> SetChargingConnectorCurrent(int chargingConnectorId, int currentToSet, int? numberOfPhases);
    Task<Result<object?>> StopChargingConnectorCharging(int chargingConnectorId);
    Task<Result<object?>> SetCarChargingCurrent(int carId, int currentToSet);
    Task<Result<object?>> UpdateCarMaxSoc(int carId, int soc);
    Task<List<DtoNotChargingWithExpectedPowerReason>?> GetNotChargingWithExpectedPowerReasons(int? carId, int? connectorId);
    Task<Dictionary<int, string>?> GetLoadPointCarOptions();
    Task UpdateCarForLoadpoint(int chargingConnectorId, int? carId);
    Task<Dictionary<DateTimeOffset, decimal>> GetGridPrices(DateTimeOffset from, DateTimeOffset to);
}
