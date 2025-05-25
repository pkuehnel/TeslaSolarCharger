using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface ITscOnlyChargingCostService
{
    Task AddChargingDetailsForAllCars();
    Task FinalizeFinishedChargingProcesses();
    Task UpdateChargePricesOfAllChargingProcesses();
    Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId);
    Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries();
    Task<List<DtoHandledCharge>> GetFinalizedChargingProcesses(int? carId, int? chargingConnectorId);
}
