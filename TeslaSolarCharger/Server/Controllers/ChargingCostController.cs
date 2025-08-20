using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class ChargingCostController(
        IChargingCostService chargingCostService,
        ITscOnlyChargingCostService tscOnlyChargingCostService,
        ISettings settings)
        : ApiBaseController
    {
        [HttpGet]
        public async Task<IActionResult> GetChargeSummary(int? carId, int? chargingConnectorId)
        {
            var result = await tscOnlyChargingCostService.GetChargeSummary(carId, chargingConnectorId);
            return Ok(result);
        }

        [HttpGet]
        public Task<List<DtoHandledCharge>> GetHandledCharges(int? carId, int? chargingConnectorId, bool hideKnownCars, int minConsumedEnergyWh)
        {
            return tscOnlyChargingCostService.GetFinalizedChargingProcesses(carId, chargingConnectorId, hideKnownCars, minConsumedEnergyWh);
        }

        [HttpGet]
        public Task<List<DtoChargePrice>> GetChargePrices()
        {
            return chargingCostService.GetChargePrices();
        }

        [HttpGet]
        public Task<DtoChargePrice> GetChargePriceById(int id)
        {
            return chargingCostService.GetChargePriceById(id);
        }

        [HttpDelete]
        public Task DeleteChargePriceById(int id)
        {
            return chargingCostService.DeleteChargePriceById(id);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateChargePrice([FromBody] DtoChargePrice chargePrice)
        {
            await chargingCostService.UpdateChargePrice(chargePrice);
            return Ok();
        }

        [HttpGet]
        public IActionResult GetChargePriceUpdateProgress()
        {
            return Ok(settings.ChargePricesUpdateProgress);
        }
    }
}
