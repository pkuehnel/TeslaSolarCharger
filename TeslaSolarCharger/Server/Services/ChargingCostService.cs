using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.MappingExtensions;
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
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;

    public ChargingCostService(ILogger<ChargingCostService> logger,
        ITeslaSolarChargerContext teslaSolarChargerContext, ITeslamateContext teslamateContext,
        IDateTimeProvider dateTimeProvider, ISettings settings,
        IMapperConfigurationFactory mapperConfigurationFactory)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _mapperConfigurationFactory = mapperConfigurationFactory;
    }

    public async Task UpdateChargePrice(DtoChargePrice dtoChargePrice)
    {
        _logger.LogTrace("{method}({@dtoChargePrice})",
            nameof(UpdateChargePrice), dtoChargePrice);
        ChargePrice chargePrice;
        if (dtoChargePrice.Id == null)
        {
            chargePrice = new ChargePrice();
            _teslaSolarChargerContext.ChargePrices.Add(chargePrice);
        }
        else
        {
            chargePrice = await _teslaSolarChargerContext.ChargePrices.FirstAsync(c => c.Id == dtoChargePrice.Id).ConfigureAwait(false);
        }

        //Can not be null as declared as Required in DTO
        chargePrice.GridPrice = (decimal)dtoChargePrice.GridPrice!;
        chargePrice.SolarPrice = (decimal)dtoChargePrice.SolarPrice!;
        chargePrice.ValidSince = dtoChargePrice.ValidSince;
        chargePrice.AddSpotPriceToGridPrice = dtoChargePrice.AddSpotPriceToGridPrice;
        chargePrice.SpotPriceCorrectionFactor = (dtoChargePrice.SpotPriceSurcharge ?? 0) / 100;
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await UpdateHandledChargesPriceCalculation().ConfigureAwait(false);
    }

    private async Task UpdateHandledChargesPriceCalculation()
    {
        var handledCharges = await _teslaSolarChargerContext.HandledCharges.ToListAsync().ConfigureAwait(false);
        foreach (var handledCharge in handledCharges)
        {

            var chargingProcess = await _teslamateContext.ChargingProcesses
                .FirstOrDefaultAsync(c => c.Id == handledCharge.ChargingProcessId).ConfigureAwait(false);
            if (chargingProcess == default)
            {
                _logger.LogWarning("No charging process with id {chargingPricessId} found",
                    handledCharge.ChargingProcessId);
                var powerDistributions = await _teslaSolarChargerContext.PowerDistributions
                    .Where(p => p.HandledChargeId == handledCharge.Id)
                    .ToListAsync().ConfigureAwait(false);
                foreach (var powerDistribution in powerDistributions)
                {
                    _teslaSolarChargerContext.PowerDistributions.Remove(powerDistribution);
                }

                _teslaSolarChargerContext.HandledCharges.Remove(handledCharge);
            }
        }

        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await FinalizeHandledCharges(handledCharges).ConfigureAwait(false);
    }

    public async Task<List<DtoChargePrice>> GetChargePrices()
    {
        _logger.LogTrace("{method}()", nameof(GetChargePrices));
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.Id, opt => opt.MapFrom(c => c.Id));
        });
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .ProjectTo<DtoChargePrice>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return chargePrices;
    }
    // TODO: here is code that can be deleted: 2023-02-24 Freundorfer, Josef
    // auf einmal ist es, mir zu blöd: es ist gerade noch komisch zu schreiben, aber es wird besser

    public async Task AddPowerDistributionForAllChargingCars()
    {
        _logger.LogTrace("{method}()", nameof(AddPowerDistributionForAllChargingCars));
        await CreateDefaultChargePrice().ConfigureAwait(false);
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
        if (chargingPower == null)
        {
            _logger.LogWarning("Can not handle as at least one parameter is null");
            return;
        }
        if (powerFromGrid == null)
        {
            _logger.LogDebug("As no grid power is available assuming 100% power is coming from grid.");
            powerFromGrid = chargingPower;
        }

        var powerDistribution = new PowerDistribution()
        {
            ChargingPower = (int)chargingPower,
            PowerFromGrid = (int)powerFromGrid,
            TimeStamp = _dateTimeProvider.UtcNow(),
        };
        var latestOpenHandledCharge = await _teslaSolarChargerContext.HandledCharges
            .OrderByDescending(h => h.ChargingProcessId)
            .FirstOrDefaultAsync(h => h.CarId == carId && h.CalculatedPrice == null).ConfigureAwait(false);
        var latestOpenChargingProcessId = await _teslamateContext.ChargingProcesses
            .OrderByDescending(cp => cp.StartDate)
            .Where(cp => cp.CarId == carId && cp.EndDate == null)
            .Select(cp => cp.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        _logger.LogDebug("latest open handled charge: {@latestOpenHandledCharge}, latest open charging process id: {id}",
            latestOpenHandledCharge, latestOpenChargingProcessId);
        //if new charging process
        if (latestOpenHandledCharge == default
            || latestOpenHandledCharge.ChargingProcessId != latestOpenChargingProcessId)
        {

            if (latestOpenChargingProcessId == default)
            {
                _logger.LogWarning("Seems like car {carId} is charging but there is no open charging process found in TeslaMate", carId);
                return;
            }

            var relevantDateTime = _dateTimeProvider.UtcNow();
            var currentChargePrice = await GetRelevantChargePrice(relevantDateTime).ConfigureAwait(false);

            if (currentChargePrice == default)
            {
                _logger.LogWarning("No valid chargeprice is defined");
                return;
            }

            _logger.LogDebug("Creating new HandledCharge");
            latestOpenHandledCharge = new HandledCharge()
            {
                CarId = carId,
                ChargingProcessId = latestOpenChargingProcessId,
            };
        }
        else
        {
            var lastPowerDistributionTimeStamp = _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledCharge == latestOpenHandledCharge)
                .OrderByDescending(p => p.TimeStamp)
                .Select(p => p.TimeStamp)
                .FirstOrDefault();
            if (lastPowerDistributionTimeStamp != default)
            {
                var timespanSinceLastPowerDistribution = powerDistribution.TimeStamp - lastPowerDistributionTimeStamp;
                powerDistribution.UsedWattHours = (float)(chargingPower * timespanSinceLastPowerDistribution.TotalHours);
            }

        }

        powerDistribution.HandledCharge = latestOpenHandledCharge;
        powerDistribution.GridProportion = (float)(powerFromGrid / (float)chargingPower);
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

    private async Task<ChargePrice?> GetRelevantChargePrice(DateTime relevantDateTime)
    {
        _logger.LogTrace("{method}({dateTime})", nameof(GetRelevantChargePrice), relevantDateTime);
        var currentChargePrice = await _teslaSolarChargerContext.ChargePrices
            .Where(cp => cp.ValidSince < relevantDateTime)
            .OrderByDescending(cp => cp.ValidSince)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        return currentChargePrice;
    }

    private async Task CreateDefaultChargePrice()
    {
        if (!await _teslaSolarChargerContext.ChargePrices
                .AnyAsync().ConfigureAwait(false))
        {
            _logger.LogDebug("Add new charge price");
            var chargePrice = new ChargePrice()
            {
                GridPrice = new decimal(0.28),
                SolarPrice = new decimal(0.10),
                ValidSince = new DateTime(2022, 9, 7),
            };
            _teslaSolarChargerContext.ChargePrices.Add(chargePrice);
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task DeleteDuplicatedHandleCharges()
    {
        var handledChargeChargingProcessIDs = await _teslaSolarChargerContext.HandledCharges
            .Select(h => h.ChargingProcessId)
            .ToListAsync().ConfigureAwait(false);

        if (handledChargeChargingProcessIDs.Count == handledChargeChargingProcessIDs.Distinct().Count())
        {
            return;
        }

        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .ToListAsync().ConfigureAwait(false);

        var duplicates = handledCharges
            .GroupBy(t => new { t.ChargingProcessId })
            .Where(t => t.Count() > 1)
            .SelectMany(x => x)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            var chargeDistributions = await _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledChargeId == duplicate.Id)
                .ToListAsync().ConfigureAwait(false);
            _teslaSolarChargerContext.PowerDistributions.RemoveRange(chargeDistributions);
            _teslaSolarChargerContext.HandledCharges.Remove(duplicate);
        }

        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<SpotPrice>> GetSpotPrices()
    {
        return await _teslaSolarChargerContext.SpotPrices.ToListAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoHandledCharge>> GetHandledCharges(int carId)
    {
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            //ToDo: Maybe possible null exceptions as not all members that are nullable in database are also nullable in dto
            cfg.CreateMap<HandledCharge, DtoHandledCharge>()
                .ForMember(d => d.ChargingProcessId, opt => opt.MapFrom(h => h.ChargingProcessId))
                .ForMember(d => d.CalculatedPrice, opt => opt.MapFrom(h => h.CalculatedPrice))
                .ForMember(d => d.UsedGridEnergy, opt => opt.MapFrom(h => h.UsedGridEnergy))
                .ForMember(d => d.UsedSolarEnergy, opt => opt.MapFrom(h => h.UsedSolarEnergy))
                .ForMember(d => d.AverageSpotPrice, opt => opt.MapFrom(h => h.AverageSpotPrice))
                ;
        });
        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CarId == carId && h.CalculatedPrice != null)
            .ProjectTo<DtoHandledCharge>(mapper)
            .ToListAsync().ConfigureAwait(false);

        handledCharges.RemoveAll(c => (c.UsedGridEnergy + c.UsedSolarEnergy) < (decimal)0.1);

        var chargingProcesses = await _teslamateContext.ChargingProcesses
            .Where(c => handledCharges.Select(h => h.ChargingProcessId).Contains(c.Id))
            .Select(c => new { c.StartDate, ChargingProcessId = c.Id })
            .ToListAsync().ConfigureAwait(false);

        foreach (var dtoHandledCharge in handledCharges)
        {
            var chargingProcess = chargingProcesses
                .FirstOrDefault(c => c.ChargingProcessId == dtoHandledCharge.ChargingProcessId);
            dtoHandledCharge.StartTime = chargingProcess?.StartDate.ToLocalTime();
            dtoHandledCharge.PricePerKwh =
                dtoHandledCharge.CalculatedPrice / (dtoHandledCharge.UsedGridEnergy + dtoHandledCharge.UsedSolarEnergy);
        }
        return handledCharges.OrderByDescending(d => d.StartTime).ToList();
    }

    public async Task FinalizeHandledCharges()
    {
        _logger.LogTrace("{method}()", nameof(FinalizeHandledCharges));
        var openHandledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CalculatedPrice == null)
            .ToListAsync().ConfigureAwait(false);

        await FinalizeHandledCharges(openHandledCharges).ConfigureAwait(false);
    }

    private async Task FinalizeHandledCharges(List<HandledCharge> handledCharges)
    {
        _logger.LogTrace("{method}({@handledCharges})",
            nameof(FinalizeHandledCharges), handledCharges);
        foreach (var openHandledCharge in handledCharges)
        {
            var chargingProcess = _teslamateContext.ChargingProcesses.FirstOrDefault(c =>
                c.Id == openHandledCharge.ChargingProcessId && c.EndDate != null);
            if (chargingProcess == default)
            {
                _logger.LogWarning(
                    "Could not find ended charging process with {id} for handled charge {openhandledChargeId}",
                    openHandledCharge.ChargingProcessId, openHandledCharge.Id);
                continue;
            }
            _logger.LogDebug("Charging process found. Handled charge {id} can be finalized", openHandledCharge.Id);
            //ToDo: maybe calculate based on time differences in the future
            var gridProportionAverage = await _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledChargeId == openHandledCharge.Id)
                .Select(p => p.GridProportion)
                .AverageAsync().ConfigureAwait(false);
            _logger.LogDebug("Average grid proportion is {proportion}", gridProportionAverage);

            var usedEnergy = (chargingProcess.ChargeEnergyUsed ?? chargingProcess.ChargeEnergyAdded) ?? 0;
            openHandledCharge.UsedGridEnergy = usedEnergy * (decimal?)gridProportionAverage;
            openHandledCharge.UsedSolarEnergy = usedEnergy * (1 - (decimal?)gridProportionAverage);
            var relevantPowerDistributions = await _teslaSolarChargerContext.PowerDistributions
                .Where(p => p.HandledCharge == openHandledCharge)
                .OrderBy(p => p.TimeStamp)
                .ToListAsync().ConfigureAwait(false);
            var price = await _teslaSolarChargerContext.ChargePrices
                .OrderByDescending(p => p.ValidSince)
                .FirstOrDefaultAsync(p => p.ValidSince < chargingProcess.StartDate)
                .ConfigureAwait(false);
            if (relevantPowerDistributions.Count > 0)
            {
                openHandledCharge.AverageSpotPrice = await CalculateAverageSpotPrice(relevantPowerDistributions, price).ConfigureAwait(false);
            }
            if (price != default)
            {
                //ToDo: add spotPrice if useSpotPrice is enabled
                openHandledCharge.CalculatedPrice = price.GridPrice * openHandledCharge.UsedGridEnergy +
                                                    price.SolarPrice * openHandledCharge.UsedSolarEnergy;
                if (price.AddSpotPriceToGridPrice)
                {
                    openHandledCharge.CalculatedPrice += openHandledCharge.AverageSpotPrice * openHandledCharge.UsedGridEnergy;
                }
                await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                chargingProcess.Cost = openHandledCharge.CalculatedPrice;
                await _teslamateContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }

    internal async Task<decimal?> CalculateAverageSpotPrice(List<PowerDistribution> relevantPowerDistributions, ChargePrice? chargePrice)
    {
        var startTime = relevantPowerDistributions.First().TimeStamp;
        var endTime = relevantPowerDistributions.Last().TimeStamp;
        var spotPrices = await GetSpotPricesInTimeSpan(startTime, endTime).ConfigureAwait(false);

        if (IsAnyPowerTimeStampWithoutSpotPrice(relevantPowerDistributions, spotPrices))
        {
            _logger.LogWarning("At least one powerdistribution has no related spot price. Do not use spotprices.");
            return null;
        }

        var usedGridWattHoursHourGroups = CalculateGridWattHours(relevantPowerDistributions);

        float averagePrice = 0;
        foreach (var usedGridWattHour in usedGridWattHoursHourGroups)
        {
            var relavantPrice = spotPrices.First(s => s.StartDate == usedGridWattHour.Key);
            var costsInThisHour = usedGridWattHour.Value * (float)relavantPrice.Price + usedGridWattHour.Value * (float)(relavantPrice.Price * chargePrice?.SpotPriceCorrectionFactor ?? 0);
            averagePrice += costsInThisHour;
        }

        var usedGridWattHourSum = usedGridWattHoursHourGroups.Values.Sum();
        if (usedGridWattHourSum <= 0)
        {
            return null;
        }
        averagePrice /= usedGridWattHourSum;
        return Convert.ToDecimal(averagePrice);
    }

    private Dictionary<DateTime, float> CalculateGridWattHours(List<PowerDistribution> relevantPowerDistributions)
    {
        var usedGridWattHours = new Dictionary<DateTime, float>();

        var hourGroups = relevantPowerDistributions
            .GroupBy(x => new { x.TimeStamp.Date, x.TimeStamp.Hour })
            .ToList();

        foreach (var hourGroup in hourGroups)
        {
            var usedPowerWhileSpotPriceIsValid = hourGroup
                .Select(p => p.UsedWattHours * p.GridProportion ?? 0)
                .Sum();
            usedGridWattHours.Add(hourGroup.Key.Date.AddHours(hourGroup.Key.Hour), usedPowerWhileSpotPriceIsValid);
        }

        return usedGridWattHours;
    }

    internal async Task<List<SpotPrice>> GetSpotPricesInTimeSpan(DateTime startTime, DateTime endTime)
    {
        var spotPrices = await _teslaSolarChargerContext.SpotPrices.AsNoTracking()
            .Where(s => s.EndDate > startTime && s.StartDate < endTime)
            .OrderBy(s => s.StartDate)
            .ToListAsync().ConfigureAwait(false);
        return spotPrices;
    }

    private bool IsAnyPowerTimeStampWithoutSpotPrice(List<PowerDistribution> relevantPowerDistributions, List<SpotPrice> spotPrices)
    {
        foreach (var timeStamp in relevantPowerDistributions.Select(p => p.TimeStamp))
        {
            if (!spotPrices.Any(s => s.StartDate <= timeStamp && s.EndDate > timeStamp))
            {
                _logger.LogWarning("No spotprice found at {timestamp}", timeStamp);
                return true;
            }
        }

        return false;
    }

    public async Task<DtoChargeSummary> GetChargeSummary(int carId)
    {
        var handledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(h => h.CalculatedPrice != null && h.CarId == carId)
            .ToListAsync().ConfigureAwait(false);

        return GetChargeSummary(handledCharges);
    }

    private DtoChargeSummary GetChargeSummary(List<HandledCharge> handledCharges)
    {
        var dtoChargeSummary = new DtoChargeSummary()
        {
            ChargeCost = handledCharges.Sum(h => h.CalculatedPrice ?? 0),
            ChargedGridEnergy = handledCharges.Sum(h => h.UsedGridEnergy ?? 0),
            ChargedSolarEnergy = handledCharges.Sum(h => h.UsedSolarEnergy ?? 0),
        };
        return dtoChargeSummary;
    }

    public async Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
    {
        var handledChargeGroups = (await _teslaSolarChargerContext.HandledCharges
                .Where(h => h.CalculatedPrice != null)
                .ToListAsync().ConfigureAwait(false))
            .GroupBy(h => h.CarId).ToList();

        var chargeSummaries = new Dictionary<int, DtoChargeSummary>();

        foreach (var handledChargeGroup in handledChargeGroups)
        {
            var list = handledChargeGroup.ToList();
            chargeSummaries.Add(handledChargeGroup.Key, GetChargeSummary(list));
        }

        return chargeSummaries;
    }

    public async Task<DtoChargePrice> GetChargePriceById(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(GetChargePriceById), id);
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<ChargePrice, DtoChargePrice>()
                .ForMember(d => d.Id, opt => opt.MapFrom(c => c.Id))
                .ForMember(d => d.SpotPriceSurcharge, opt => opt.MapFrom(c => c.SpotPriceCorrectionFactor * 100))
                ;
        });
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .Where(c => c.Id == id)
            .ProjectTo<DtoChargePrice>(mapper)
            .FirstAsync().ConfigureAwait(false);
        return chargePrices;
    }

    public async Task DeleteChargePriceById(int id)
    {
        var chargePrice = await _teslaSolarChargerContext.ChargePrices
            .FirstAsync(c => c.Id == id).ConfigureAwait(false);
        _teslaSolarChargerContext.ChargePrices.Remove(chargePrice);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await UpdateHandledChargesPriceCalculation().ConfigureAwait(false);
    }
}
