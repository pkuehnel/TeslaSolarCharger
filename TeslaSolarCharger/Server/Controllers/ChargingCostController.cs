using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public Task<DtoChargePrice> GetChargePriceById(int id)
        {
            return _chargingCostService.GetChargePriceById(id);
        }

        [HttpPost]
        public Task UpdateChargePrice([FromBody] DtoChargePrice chargePrice)
        {
            return _chargingCostService.UpdateChargePrice(chargePrice);
        }
    }
}
