using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingCostService
{
    Task HandleAllCars();
    Task FinalizeHandledCharges();
    Task<DtoChargeSummary> GetChargeSummary(int carId);
    Task UpdateChargePrice(DtoChargePrice dtoChargePrice);
    Task<List<DtoChargePrice>> GetChargePrices();
    Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries();
    Task<DtoChargePrice> GetChargePriceById(int id);
}
