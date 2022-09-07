using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class ChargingCostService : IChargingCostService
{
    private readonly ILogger<ChargingCostService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;

    public ChargingCostService(ILogger<ChargingCostService> logger,
        ITeslaSolarChargerContext teslaSolarChargerContext, ITeslamateContext teslamateContext,
        IDateTimeProvider dateTimeProvider, ISettings settings)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
    }

    public async Task UpdateChargePrice(int? chargePriceId, DtoChargePrice dtoChargePrice)
    {
        _logger.LogTrace("{method}({chargePriceId}, @{dtoChargePrice})",
            nameof(UpdateChargePrice), chargePriceId, dtoChargePrice);
        ChargePrice chargePrice;
        if (chargePriceId == null)
        {
            chargePrice = new ChargePrice();
            _teslaSolarChargerContext.ChargePrices.Add(chargePrice);
        }
        else
        {
            chargePrice = await _teslaSolarChargerContext.ChargePrices.FirstAsync(c => c.Id == chargePriceId).ConfigureAwait(false);
        }

        chargePrice.GridPrice = dtoChargePrice.GridPrice;
        chargePrice.SolarPrice = dtoChargePrice.SolarPrice;
        chargePrice.ValidSince = dtoChargePrice.ValidSince;
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task HandleAllCars()
    {
        _logger.LogTrace("{method}()", nameof(HandleAllCars));
        //ToDO: remove before release
        var chargePrice = await _teslaSolarChargerContext.ChargePrices
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (chargePrice == default)
        {
            chargePrice = new ChargePrice()
            {
                GridPrice = new decimal(0.28),
                SolarPrice = new decimal(0.10),
                ValidSince = new DateTime(2022, 9, 7),
            };
            _teslaSolarChargerContext.ChargePrices.Add(chargePrice);
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        foreach (var car in _settings.Cars)
        {
            if (car.CarState.ChargingPowerAtHome > 0)
            {
                await AddPowerDistribution(car.Id, car.CarState.ChargingPowerAtHome, -_settings.Overage).ConfigureAwait(false);
            }
        }
    }

    private async Task AddPowerDistribution(int carId, int? chargingPower, int? powerFromGrid)
    {
        _logger.LogTrace("{method}({carId}, {chargingPower}, {powerFromGrid})",
            nameof(AddPowerDistribution), carId, chargingPower, powerFromGrid);
        if (chargingPower == null || powerFromGrid == null)
        {
            _logger.LogWarning("Can not handle as at least one parameter is null");
            return;
        }
        var powerDistribution = new PowerDistribution()
        {
            ChargingPower = (int)chargingPower,
            PowerFromGrid = (int)powerFromGrid,
            TimeStamp = _dateTimeProvider.UtcNow(),
        };
        var latestOpenHandledCharge = await _teslaSolarChargerContext.HandledCharges
            .FirstOrDefaultAsync(h => h.Id == carId && h.UsedGridEnergy == null).ConfigureAwait(false);
        var latestOpenChargingProcessId = await _teslamateContext.ChargingProcesses
            .OrderByDescending(cp => cp.StartDate)
            .Where(cp => cp.CarId == carId && cp.EndDate == null)
            .Select(cp => cp.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (latestOpenHandledCharge == default
            || latestOpenHandledCharge.ChargingProcessId != latestOpenChargingProcessId)
        {
            if (latestOpenChargingProcessId == default)
            {
                _logger.LogWarning("Seems like car {carId} is charging but there is no open charging process found in TeslaMate0", carId);
                return;
            }

            var relevantDateTime = _dateTimeProvider.UtcNow();
            var currentChargePrice = await CurrentChargePrice(relevantDateTime).ConfigureAwait(false);

            if (currentChargePrice == default)
            {
                _logger.LogWarning("No valid chargeprice is defined");
                return;
            }

            latestOpenHandledCharge = new HandledCharge()
            {
                CarId = carId,
                ChargingProcessId = latestOpenChargingProcessId,
                ChargePriceId = currentChargePrice.Id,
            };
        }

        powerDistribution.HandledCharge = latestOpenHandledCharge;
        powerDistribution.GridProportion = (float)(powerFromGrid / (float) chargingPower);
        _logger.LogTrace("Calculated grod proportion: {proportion}", powerDistribution.GridProportion);
        if (powerDistribution.GridProportion < 0)
        {
            powerDistribution.GridProportion = 0;
        }
        if (powerDistribution.GridProportion > 1)
        {
            powerDistribution.GridProportion = 1;
        }
        _teslaSolarChargerContext.PowerDistributions.Add(powerDistribution);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<ChargePrice?> CurrentChargePrice(DateTime relevantDateTime)
    {
        var currentChargePrice = await _teslaSolarChargerContext.ChargePrices
            .Where(cp => cp.ValidSince < relevantDateTime)
            .OrderByDescending(cp => cp.ValidSince)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        return currentChargePrice;
    }

    public async Task FinalizeHandledCharges()
    {
        _logger.LogTrace("{method}()", nameof(FinalizeHandledCharges));
        var openHandledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CalculatedPrice == null)
            .ToListAsync().ConfigureAwait(false);

        var chargingProcessesOfOpenHandledCharges = await _teslamateContext.ChargingProcesses
            .Where(c => openHandledCharges.Select(h => h.ChargingProcessId).Contains(c.Id)
                        && c.EndDate != null)
            .ToListAsync().ConfigureAwait(false);

        foreach (var openHandledCharge in openHandledCharges)
        {
            var chargingProcess = chargingProcessesOfOpenHandledCharges.FirstOrDefault(c => c.Id == openHandledCharge.ChargingProcessId);
            if (chargingProcess == default)
            {
                continue;
            }
            //ToDo: maybe calculate based on time differences in the future
            var gridProportionAverage = await _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledChargeId == openHandledCharge.Id)
                .Select(p => p.GridProportion)
                .AverageAsync().ConfigureAwait(false);
            openHandledCharge.UsedGridEnergy = chargingProcess.ChargeEnergyUsed * (decimal?)gridProportionAverage;
            openHandledCharge.UsedSolarEnergy = chargingProcess.ChargeEnergyUsed * ( 1 - (decimal?)gridProportionAverage);
            var price = await _teslaSolarChargerContext.ChargePrices
                .FirstOrDefaultAsync(p => p.Id == openHandledCharge.ChargePriceId)
                .ConfigureAwait(false);
            if (price != default)
            {
                openHandledCharge.CalculatedPrice = price.GridPrice * openHandledCharge.UsedGridEnergy +
                                                    price.SolarPrice * openHandledCharge.UsedSolarEnergy;
                await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }
}
