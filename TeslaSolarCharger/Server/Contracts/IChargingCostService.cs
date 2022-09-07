using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingCostService
{
    Task UpdateChargePrice(int? chargePriceId, DtoChargePrice dtoChargePrice);
    Task HandleAllCars();
    Task FinalizeHandledCharges();
    Task<DtoChargeSummary> GetChargeSummary(int carId);
}
