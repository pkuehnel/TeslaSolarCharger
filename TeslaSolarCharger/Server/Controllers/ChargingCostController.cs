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
        public Task<DtoChargeSummary> GetChargeSummary(int carId)
        {
            return tscOnlyChargingCostService.GetChargeSummary(carId);
        }

        [HttpGet]
        public Task<List<DtoHandledCharge>> GetHandledCharges(int carId)
        {
            return tscOnlyChargingCostService.GetFinalizedChargingProcesses(carId);
        }

        [HttpGet]
        public Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
        {
            return tscOnlyChargingCostService.GetChargeSummaries();
        }

        [HttpGet]
        public Task<List<DtoChargePrice>> GetChargePrices()
        {
            return chargingCostService.GetChargePrices();
        }

        [HttpGet]
        public Task<List<SpotPrice>> GetSpotPrices()
        {
            return chargingCostService.GetSpotPrices();
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
        public Task UpdateChargePrice([FromBody] DtoChargePrice chargePrice)
        {
            return chargingCostService.UpdateChargePrice(chargePrice);
        }

        [HttpGet]
        public DtoValue<string?> GetChargePricesUpdateText()
        {
            return new DtoValue<string?>(settings.ChargePricesUpdateText);
        }
    }
}
