using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using ChargingProcess = TeslaSolarCharger.Model.Entities.TeslaMate.ChargingProcess;

namespace TeslaSolarCharger.Server.Services;

public class ChargingCostService : IChargingCostService
{
    private readonly ILogger<ChargingCostService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IFixedPriceService _fixedPriceService;

    public ChargingCostService(ILogger<ChargingCostService> logger,
        ITeslaSolarChargerContext teslaSolarChargerContext, ITeslamateContext teslamateContext,
        IDateTimeProvider dateTimeProvider, ISettings settings,
        IMapperConfigurationFactory mapperConfigurationFactory, IConfigurationWrapper configurationWrapper,
        IFixedPriceService fixedPriceService)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _configurationWrapper = configurationWrapper;
        _fixedPriceService = fixedPriceService;
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
        chargePrice.EnergyProvider = dtoChargePrice.EnergyProvider;
        chargePrice.AddSpotPriceToGridPrice = dtoChargePrice.AddSpotPriceToGridPrice;
        chargePrice.SpotPriceCorrectionFactor = (dtoChargePrice.SpotPriceSurcharge ?? 0) / 100;
        chargePrice.EnergyProviderConfiguration = dtoChargePrice.EnergyProviderConfiguration;
        switch (dtoChargePrice.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                break;
            case EnergyProvider.Awattar:
                break;
            case EnergyProvider.Energinet:
                break;
            case EnergyProvider.HomeAssistant:
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        await UpdateHandledChargesPriceCalculation().ConfigureAwait(false);
    }

    private async Task UpdateHandledChargesPriceCalculation()
    {
        var handledCharges = await _teslaSolarChargerContext.HandledCharges.ToListAsync().ConfigureAwait(false);
        var chargingProcesses = await _teslamateContext.ChargingProcesses.ToListAsync().ConfigureAwait(false);
        var chargePrices = await _teslaSolarChargerContext.ChargePrices.OrderByDescending(c => c.ValidSince).ToListAsync().ConfigureAwait(false);
        foreach (var handledCharge in handledCharges)
        {
            var chargingProcess = chargingProcesses.FirstOrDefault(c => c.Id == handledCharge.ChargingProcessId);
            if (chargingProcess == default)
            {
                _logger.LogWarning("Could not update charge costs for as chargingProcessId {chargingProcessId} was not found", handledCharge.ChargingProcessId);
                continue;
            }
            var chargePrice = chargePrices.FirstOrDefault(p => p.ValidSince < chargingProcess.StartDate);
            if (chargePrice == default)
            {
                _logger.LogWarning("Could not update charge costs for as no chargeprice for {startDate} was found.", chargingProcess.StartDate);
                continue;
            }

            await UpdateChargingProcessCosts(handledCharge, chargePrice, chargingProcess).ConfigureAwait(false);
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await _teslamateContext.SaveChangesAsync().ConfigureAwait(false);
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
        return chargePrices.OrderBy(p => p.ValidSince).ToList();
    }

    public async Task AddPowerDistributionForAllChargingCars()
    {
        _logger.LogTrace("{method}()", nameof(AddPowerDistributionForAllChargingCars));
        await CreateDefaultChargePrice().ConfigureAwait(false);
        await CheckForToHighChargingProcessIds().ConfigureAwait(false);

        foreach (var car in _settings.CarsToManage)
        {
            if (car.ChargingPowerAtHome > 0)
            {
                var powerFromGrid = -_settings.Overage;
                if (_configurationWrapper.FrontendConfiguration()?.GridValueSource == SolarValueSource.None
                    && _configurationWrapper.FrontendConfiguration()?.InverterValueSource != SolarValueSource.None
                    && _settings.InverterPower != null)
                {
                    var powerBuffer = _configurationWrapper.PowerBuffer(true);
                    powerFromGrid = - _settings.InverterPower
                                    + (powerBuffer > 0 ? powerBuffer : 0)
                                    + _settings.CarsToManage.Select(c => c.ChargingPowerAtHome).Sum();
                }
                await AddPowerDistribution(car.Id, car.ChargingPowerAtHome, powerFromGrid).ConfigureAwait(false);
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
        _logger.LogTrace("Calculated grid proportion: {proportion}", powerDistribution.GridProportion);
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

    private async Task CheckForToHighChargingProcessIds()
    {
        _logger.LogTrace("{method}()", nameof(CheckForToHighChargingProcessIds));
        var highestTeslaMateChargingProcessId = await _teslamateContext.ChargingProcesses
            .OrderByDescending(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync().ConfigureAwait(false);

        var toHighHandledCharges = await _teslaSolarChargerContext.HandledCharges
            .Where(hc => hc.ChargingProcessId > highestTeslaMateChargingProcessId)
            .ToListAsync().ConfigureAwait(false);

        foreach (var highHandledCharge in toHighHandledCharges)
        {
            _logger.LogWarning(
                "The handled charge with ID {handledChargeId} has a chargingprocess ID of {chargingProcessId}, which is higher than the highes charging process ID in TeslaMate {maxChargingProcessId}.",
                highHandledCharge.Id, highHandledCharge.ChargingProcessId, highestTeslaMateChargingProcessId);
            if (highHandledCharge.ChargingProcessId > 0)
            {
                highHandledCharge.ChargingProcessId = -highHandledCharge.ChargingProcessId;
                _logger.LogDebug("Charging process Id was set to {newChargingProcessId}", highHandledCharge.ChargingProcessId);
            }
        }

        await _teslamateContext.SaveChangesAsync().ConfigureAwait(false);
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
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .OrderByDescending(p => p.ValidSince).ToListAsync().ConfigureAwait(false);
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
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
            var price = chargePrices
                .FirstOrDefault(p => p.ValidSince < chargingProcess.StartDate);
            if (relevantPowerDistributions.Count > 0)
            {
                openHandledCharge.AverageSpotPrice = await CalculateAverageSpotPrice(relevantPowerDistributions, price).ConfigureAwait(false);
            }
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

            if (price != default)
            {
                await UpdateChargingProcessCosts(openHandledCharge, price, chargingProcess).ConfigureAwait(false);
            }
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await _teslamateContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task UpdateChargingProcessCosts(HandledCharge openHandledCharge, ChargePrice price,
        ChargingProcess chargingProcess)
    {
        if (chargingProcess.EndDate == null)
        {
            _logger.LogWarning("Charging process {id} has no end date. Can not calculate costs.", chargingProcess.Id);
            return;
        }

        var relevantPowerDistributions = await _teslaSolarChargerContext.PowerDistributions
            .Where(p => p.HandledCharge == openHandledCharge)
            .OrderBy(p => p.TimeStamp)
            .AsNoTracking()
            .ToListAsync().ConfigureAwait(false);

        List<Price> prices;
        decimal? gridCost = null;

        switch (price.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                throw new NotImplementedException();
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                prices = (await _fixedPriceService.GetPriceData(chargingProcess.StartDate, chargingProcess.EndDate.Value, price.EnergyProviderConfiguration).ConfigureAwait(false)).ToList();
                gridCost = GetGridChargeCosts(relevantPowerDistributions, prices, price.GridPrice);
                break;
            case EnergyProvider.Awattar:
                throw new NotImplementedException();
                break;
            case EnergyProvider.Energinet:
                throw new NotImplementedException();
                break;
            case EnergyProvider.HomeAssistant:
                throw new NotImplementedException();
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                gridCost = price.GridPrice * openHandledCharge.UsedGridEnergy;
                if (price.AddSpotPriceToGridPrice)
                {
                    gridCost += openHandledCharge.AverageSpotPrice * openHandledCharge.UsedGridEnergy;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        openHandledCharge.CalculatedPrice = gridCost + price.SolarPrice * openHandledCharge.UsedSolarEnergy;
        chargingProcess.Cost = openHandledCharge.CalculatedPrice;
    }

    internal decimal? GetGridChargeCosts(List<PowerDistribution> relevantPowerDistributions, List<Price> prices, decimal priceGridPrice)
    {
        try
        {
            var priceGroups = GroupDistributionsByPrice(prices, relevantPowerDistributions, priceGridPrice);
            decimal totalCost = 0;
            foreach (var priceGroup in priceGroups)
            {
                var usedEnergyWhilePriceGroupsWasActive = priceGroup.Value
                    .Select(p => p.UsedWattHours * p.GridProportion ?? 0)
                    .Sum();
                var usedkWh = usedEnergyWhilePriceGroupsWasActive / 1000;
                totalCost += (decimal)(usedkWh * (float)priceGroup.Key.Value);
            }

            return totalCost;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while calculating chargeCosts for HandledCharge {handledChargeId}", relevantPowerDistributions.FirstOrDefault()?.HandledChargeId);
            return null;
        }
        
    }

    private Dictionary<Price, List<PowerDistribution>> GroupDistributionsByPrice(List<Price> prices, List<PowerDistribution> distributions,
        decimal priceGridPrice)
    {
        var groupedByPrice = new Dictionary<Price, List<PowerDistribution>>();

        foreach (var price in prices)
        {
            var relevantDistributions = distributions
                .Where(d => d.TimeStamp >= price.ValidFrom.UtcDateTime &&
                            d.TimeStamp <= price.ValidTo.UtcDateTime)
                .ToList();

            groupedByPrice.Add(price, relevantDistributions);
            distributions.RemoveAll(relevantDistributions.Contains);
        }

        if (distributions.Any())
        {
            var oldestDistribution = distributions.OrderBy(d => d.TimeStamp).First().TimeStamp;
            var newestDistribution = distributions.OrderByDescending(d => d.TimeStamp).First().TimeStamp;
            groupedByPrice.Add(
                new Price()
                {
                    ValidFrom = new DateTimeOffset(oldestDistribution, TimeSpan.Zero),
                    ValidTo = new DateTimeOffset(newestDistribution, TimeSpan.Zero),
                    Value = priceGridPrice,
                },
                distributions.ToList());
        }

        return groupedByPrice;
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
                .ForMember(d => d.SpotPriceSurcharge, opt => opt.MapFrom(c => c.SpotPriceCorrectionFactor * 100))
                ;
        });
        var chargePrices = await _teslaSolarChargerContext.ChargePrices
            .Where(c => c.Id == id)
            .ProjectTo<DtoChargePrice>(mapper)
            .FirstAsync().ConfigureAwait(false);
        switch (chargePrices.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                break;
            case EnergyProvider.Awattar:
                break;
            case EnergyProvider.Energinet:
                break;
            case EnergyProvider.HomeAssistant:
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }


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
