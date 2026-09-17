using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface IChargePriceService
{
    Task<DtoChargePrice?> GetDtoChargePrice(int id);
    /// <summary>
    /// Saves the price and reports whether that worked. The caller needs the outcome: a setup step that finished
    /// with a failed price save must not show success.
    /// </summary>
    Task<Result<object?>> UpdateChargePrice(DtoChargePrice chargePrice);
    Task<DtoProgress?> GetChargePriceUpdateProgress();
}
