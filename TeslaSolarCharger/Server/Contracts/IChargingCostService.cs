﻿using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingCostService
{
    Task AddPowerDistributionForAllChargingCars();
    Task FinalizeHandledCharges();
    Task<DtoChargeSummary> GetChargeSummary(int carId);
    Task UpdateChargePrice(DtoChargePrice dtoChargePrice);
    Task<List<DtoChargePrice>> GetChargePrices();
    Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries();
    Task<DtoChargePrice> GetChargePriceById(int id);
    Task DeleteChargePriceById(int id);
    Task DeleteDuplicatedHandleCharges();
    Task<List<SpotPrice>> GetSpotPrices();
    Task<List<DtoHandledCharge>> GetHandledCharges(int carId);
}
