using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ChargingCostController : ControllerBase
    {
        private readonly IChargingCostService _chargingCostService;

        public ChargingCostController(IChargingCostService chargingCostService)
        {
            _chargingCostService = chargingCostService;
        }

        [HttpGet]
        public Task<DtoChargeSummary> GetChargeSummary(int carId)
        {
            return _chargingCostService.GetChargeSummary(carId);
        }

        [HttpGet]
        public Task<List<DtoHandledCharge>> GetHandledCharges(int carId)
        {
            return _chargingCostService.GetHandledCharges(carId);
        }

        [HttpGet]
        public Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
        {
            return _chargingCostService.GetChargeSummaries();
        }

        [HttpGet]
        public Task<List<DtoChargePrice>> GetChargePrices()
        {
            return _chargingCostService.GetChargePrices();
        }

        [HttpGet]
        public Task<List<SpotPrice>> GetSpotPrices()
        {
            return _chargingCostService.GetSpotPrices();
        }

        [HttpGet]
        public Task<DtoChargePrice> GetChargePriceById(int id)
        {
            return _chargingCostService.GetChargePriceById(id);
        }

        [HttpDelete]
        public Task DeleteChargePriceById(int id)
        {
            return _chargingCostService.DeleteChargePriceById(id);
        }

        [HttpPost]
        public Task UpdateChargePrice([FromBody] DtoChargePrice chargePrice)
        {
            return _chargingCostService.UpdateChargePrice(chargePrice);
        }
    }
}
