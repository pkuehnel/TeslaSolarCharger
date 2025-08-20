using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IChargePriceService
{
    Task<DtoChargePrice?> GetDtoChargePrice(int id);
    Task UpdateChargePrice(DtoChargePrice chargePrice);
    Task<DtoProgress?> GetChargePriceUpdateProgress();
}
