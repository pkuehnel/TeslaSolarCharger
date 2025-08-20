using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface ITscOnlyChargingCostService
{
    Task AddChargingDetailsForAllCars();
    Task FinalizeFinishedChargingProcesses();
    Task UpdateChargePricesOfAllChargingProcesses();
    Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId);
    Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries();
    Task<List<DtoHandledCharge>> GetFinalizedChargingProcesses(int? carId, int? chargingConnectorId, bool hideKnownCars,
        int minConsumedEnergyWh);
    Task<List<Price>> GetPricesInTimeSpan(DateTimeOffset from, DateTimeOffset to);
    Task AddNonZeroMeterValuesCarsAndChargingStationsToSettings();
    MeterValue GenerateDefaultMeterValue(int? carId, int? chargingConnectorId, DateTimeOffset timestamp);
}
