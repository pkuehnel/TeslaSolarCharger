using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingCostService
{
    Task UpdateChargePrice(DtoChargePrice dtoChargePrice);
    Task<List<DtoChargePrice>> GetChargePrices();
    Task<DtoChargePrice> GetChargePriceById(int id);
    Task DeleteChargePriceById(int id);
    Task DeleteDuplicatedHandleCharges();
    Task<List<SpotPrice>> GetSpotPrices();
    Task ConvertToNewChargingProcessStructure();
    Task AddFirstChargePrice();
}
