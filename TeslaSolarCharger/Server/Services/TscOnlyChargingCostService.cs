using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.MappingExtensions;

namespace TeslaSolarCharger.Server.Services;

public class TscOnlyChargingCostService(ILogger<TscOnlyChargingCostService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IServiceProvider serviceProvider,
    IMapperConfigurationFactory mapperConfigurationFactory,
    IConstants constants) : ITscOnlyChargingCostService
{
    public async Task FinalizeFinishedChargingProcesses()
    {
        logger.LogTrace("{method}()", nameof(FinalizeFinishedChargingProcesses));
        var openChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate == null)
            .ToListAsync().ConfigureAwait(false);
        var timeSpanToHandleChargingProcessAsCompleted = TimeSpan.FromMinutes(2);
        foreach (var chargingProcess in openChargingProcesses)
        {
            var latestChargingDetail = await context.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .OrderByDescending(cd => cd.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (latestChargingDetail == default)
            {
                logger.LogWarning("No charging detail found for charging process with ID {chargingProcessId}.", chargingProcess.Id);
                continue;
            }

            if (latestChargingDetail.TimeStamp.Add(timeSpanToHandleChargingProcessAsCompleted) < dateTimeProvider.UtcNow())
            {
                try
                {
                    await FinalizeChargingProcess(chargingProcess);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while finalizing charging process with ID {chargingProcessId}.", chargingProcess.Id);
                }
            }
        }
    }

    public async Task UpdateChargePricesOfAllChargingProcesses()
    {
        logger.LogTrace("{method}()", nameof(UpdateChargePricesOfAllChargingProcesses));
        var finalizedChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate != null)
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingProcess in finalizedChargingProcesses)
        {
            settings.ChargePricesUpdateText = $"Updating charging processes {finalizedChargingProcesses.IndexOf(chargingProcess)}/{finalizedChargingProcesses.Count}";
            try
            {
                await FinalizeChargingProcess(chargingProcess);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while updating charge prices of charging process with ID {chargingProcessId}.", chargingProcess.Id);
            }
        }

        settings.ChargePricesUpdateText = null;
    }

    public async Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
    {
        var chargingProcessGroups = (await context.ChargingProcesses
                .Where(h => h.Cost != null)
                .ToListAsync().ConfigureAwait(false))
            .GroupBy(h => h.CarId).ToList();
        var chargeSummaries = new Dictionary<int, DtoChargeSummary>();
        foreach (var chargingProcessGroup in chargingProcessGroups)
        {
            var list = chargingProcessGroup.ToList();
            chargeSummaries.Add(chargingProcessGroup.Key, GetChargeSummaryByChargingProcesses(list));
        }

        return chargeSummaries;
    }

    public async Task<DtoChargeSummary> GetChargeSummary(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(GetChargeSummary), carId);
        var chargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.CarId == carId)
            .AsNoTracking()
            .ToListAsync().ConfigureAwait(false);
        var chargeSummary = GetChargeSummaryByChargingProcesses(chargingProcesses);
        return chargeSummary;
    }

    public async Task<List<DtoHandledCharge>> GetFinalizedChargingProcesses(int carId)
    {
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            //ToDo: Maybe possible null exceptions as not all members that are nullable in database are also nullable in dto
            cfg.CreateMap<ChargingProcess, DtoHandledCharge>()
                .ForMember(d => d.StartTime, opt => opt.MapFrom(h => h.StartDate.ToLocalTime()))
                .ForMember(d => d.CalculatedPrice, opt => opt.MapFrom(h => h.Cost == null ? 0m : Math.Round(h.Cost.Value, 2)))
                .ForMember(d => d.UsedGridEnergy, opt => opt.MapFrom(h => h.UsedGridEnergyKwh == null ? 0m : Math.Round(h.UsedGridEnergyKwh.Value, 2)))
                .ForMember(d => d.UsedHomeBatteryEnergy, opt => opt.MapFrom(h => h.UsedHomeBatteryEnergyKwh == null ? 0m : Math.Round(h.UsedHomeBatteryEnergyKwh.Value, 2)))
                .ForMember(d => d.UsedSolarEnergy, opt => opt.MapFrom(h => h.UsedSolarEnergyKwh == null ? 0m : Math.Round(h.UsedSolarEnergyKwh.Value, 2)))
                ;
        });

        var handledCharges = await context.ChargingProcesses
            .Where(h => h.CarId == carId && h.Cost != null)
            .OrderByDescending(h => h.StartDate)
            .ProjectTo<DtoHandledCharge>(mapper)
            .ToListAsync().ConfigureAwait(false);

        handledCharges.RemoveAll(c => (c.UsedGridEnergy + c.UsedSolarEnergy + c.UsedHomeBatteryEnergy) < 0.1m);
        foreach (var dtoHandledCharge in handledCharges)
        {
            dtoHandledCharge.PricePerKwh = Math.Round(dtoHandledCharge.CalculatedPrice / (dtoHandledCharge.UsedGridEnergy + dtoHandledCharge.UsedSolarEnergy + dtoHandledCharge.UsedHomeBatteryEnergy), 3);
        }
        return handledCharges;
    }

    private static DtoChargeSummary GetChargeSummaryByChargingProcesses(List<ChargingProcess> chargingProcesses)
    {
        var chargeSummary = new DtoChargeSummary();
        foreach (var chargingProcess in chargingProcesses)
        {
            chargeSummary.ChargeCost += chargingProcess.Cost ?? 0;
            chargeSummary.ChargedGridEnergy += chargingProcess.UsedGridEnergyKwh ?? 0;
            chargeSummary.ChargedHomeBatteryEnergy += chargingProcess.UsedHomeBatteryEnergyKwh ?? 0;
            chargeSummary.ChargedSolarEnergy += chargingProcess.UsedSolarEnergyKwh ?? 0;
        }

        return chargeSummary;
    }

    private async Task FinalizeChargingProcess(ChargingProcess chargingProcess)
    {
        logger.LogTrace("{method}({chargingProcessId})", nameof(FinalizeChargingProcess), chargingProcess.Id);
        
        var chargingDetails = await context.ChargingDetails
            .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
            .OrderBy(cd => cd.TimeStamp)
            .ToListAsync().ConfigureAwait(false);
        decimal usedSolarEnergyWh = 0;
        decimal usedHomeBatteryEnergyWh = 0;
        decimal usedGridEnergyWh = 0;
        decimal cost = 0;
        chargingProcess.EndDate = chargingDetails.Last().TimeStamp;
        var prices = await GetPricesInTimeSpan(chargingDetails.First().TimeStamp, chargingProcess.EndDate.Value);        //When a charging process is stopped and resumed later, the last charging detail is too old and should not be used because it would use the last value dring the whole time althoug the car was not charging
        var maxChargingDetailsDuration = TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds).Add(TimeSpan.FromSeconds(10));
        for (var index = 1; index < chargingDetails.Count; index++)
        {
            var price = GetPriceByTimeStamp(prices, chargingDetails[index].TimeStamp);
            logger.LogTrace("Price for timestamp {timeStamp}: {@price}", chargingDetails[index].TimeStamp, price);
            var chargingDetail = chargingDetails[index];
            var timeSpanSinceLastDetail = chargingDetail.TimeStamp - chargingDetails[index - 1].TimeStamp;
            
            if (timeSpanSinceLastDetail > maxChargingDetailsDuration)
            {
                logger.LogWarning("Do not use charging detail as last charging detail ist too old");
                continue;
            }
            var usedSolarWhSinceLastChargingDetail = (decimal)(chargingDetail.SolarPower * timeSpanSinceLastDetail.TotalHours);
            usedSolarEnergyWh += usedSolarWhSinceLastChargingDetail;
            var usedHomeBatteryWhSinceLastChargingDetail = (decimal)(chargingDetail.HomeBatteryPower * timeSpanSinceLastDetail.TotalHours);
            usedHomeBatteryEnergyWh += usedHomeBatteryWhSinceLastChargingDetail;
            var usedGridPowerSinceLastChargingDetail = (decimal)(chargingDetail.GridPower * timeSpanSinceLastDetail.TotalHours);
            usedGridEnergyWh += usedGridPowerSinceLastChargingDetail;
            cost += usedGridPowerSinceLastChargingDetail * price.Value;
            cost += usedSolarWhSinceLastChargingDetail * price.SolarPrice;
            cost += usedHomeBatteryWhSinceLastChargingDetail * price.SolarPrice;
        }
        chargingProcess.UsedSolarEnergyKwh = usedSolarEnergyWh / 1000m;
        chargingProcess.UsedHomeBatteryEnergyKwh = usedHomeBatteryEnergyWh / 1000m;
        chargingProcess.UsedGridEnergyKwh = usedGridEnergyWh / 1000m;
        chargingProcess.Cost = cost / 1000m;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private Price GetPriceByTimeStamp(List<Price> prices, DateTime timeStamp)
    {
        return prices.First(p => p.ValidFrom <= timeStamp && p.ValidTo > timeStamp);
    }

    private async Task<List<Price>> GetPricesInTimeSpan(DateTime from, DateTime to)
    {
        logger.LogTrace("{method}({from}, {to})", nameof(GetPricesInTimeSpan), from, to);
        var chargePrice = await context.ChargePrices
            .Where(c => c.ValidSince < from)
            .OrderByDescending(c => c.ValidSince)
            .FirstAsync();
        var fromDateTimeOffset = new DateTimeOffset(from, TimeSpan.Zero);
        var toDateTimeOffset = new DateTimeOffset(to.AddMilliseconds(1), TimeSpan.Zero);
        IPriceDataService priceDataService;
        List<Price> prices;
        switch (chargePrice.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                priceDataService = serviceProvider.GetRequiredService<IFixedPriceService>();
                prices = (await priceDataService.GetPriceData(fromDateTimeOffset, toDateTimeOffset, chargePrice.EnergyProviderConfiguration).ConfigureAwait(false)).ToList();
                prices = AddDefaultChargePrices(prices, fromDateTimeOffset, toDateTimeOffset, chargePrice.GridPrice, chargePrice.SolarPrice);

                return prices;
            case EnergyProvider.Awattar:
                break;
            case EnergyProvider.Energinet:
                break;
            case EnergyProvider.HomeAssistant:
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                priceDataService = serviceProvider.GetRequiredService<IOldTscConfigPriceService>();
                prices = (await priceDataService.GetPriceData(fromDateTimeOffset, toDateTimeOffset, chargePrice.Id.ToString()).ConfigureAwait(false)).ToList();
                return prices;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        throw new NotImplementedException($"Energyprovider {chargePrice.EnergyProvider} is not implemented.");
    }

    private List<Price> AddDefaultChargePrices(List<Price> prices, DateTimeOffset from, DateTimeOffset to, decimal defaultValue, decimal defaultSolarPrice)
    {
        var updatedPrices = new List<Price>();

        // Sort the list by ValidFrom
        prices = prices.OrderBy(p => p.ValidFrom).ToList();

        // Initialize the start of the uncovered period
        var currentStart = from;

        foreach (var price in prices)
        {
            // If there's a gap between currentStart and the next price.ValidFrom
            if (currentStart < price.ValidFrom)
            {
                updatedPrices.Add(new Price
                {
                    Value = defaultValue,
                    SolarPrice = defaultSolarPrice,
                    ValidFrom = currentStart,
                    ValidTo = price.ValidFrom,
                });
            }

            // Update currentStart to the end of the current price's ValidTo
            currentStart = price.ValidTo;
        }

        // Check for a gap after the last price.ValidTo to the 'to' date
        if (currentStart < to)
        {
            updatedPrices.Add(new Price
            {
                Value = defaultValue,
                SolarPrice = defaultSolarPrice,
                ValidFrom = currentStart,
                ValidTo = to,
            });
        }

        // Add all original prices to the updated list
        updatedPrices.AddRange(prices);

        return updatedPrices.OrderBy(p => p.ValidFrom).ToList();
    }

    public async Task AddChargingDetailsForAllCars()
    {
        logger.LogTrace("{method}()", nameof(AddChargingDetailsForAllCars));
        var powerBuffer = configurationWrapper.PowerBuffer(true);
        var overage = settings.Overage ?? (settings.InverterPower - (powerBuffer < 0 ? 0 : powerBuffer));
        var homeBatteryDischargingPower =  (- settings.HomeBatteryPower) ?? 0;
        if (homeBatteryDischargingPower < 0)
        {
            homeBatteryDischargingPower = 0;
        }
        var gridPower = (overage < 0) ? (-overage.Value) : 0;
        var solarPower = overage ?? 0;
        logger.LogTrace("SolarPower: {solarPower}", solarPower);
        logger.LogTrace("HomeBatteryDischargingPower: {homeBatteryDischargingPower}", homeBatteryDischargingPower);
        var combinedChargingPowerAtHome = settings.CarsToManage.Sum(c => c.ChargingPowerAtHome ?? 0);
        var usedGridPower = 0;
        var usedHomeBatteryPower = 0;
        var usedSolarPower = 0;
        if (combinedChargingPowerAtHome == 0)
        {
            logger.LogTrace("No car is charging at home so no charging detail to create.");
            return;
        }
        logger.LogTrace("Combined charging power at home: {combinedChargingPowerAtHome}", combinedChargingPowerAtHome);
        if (gridPower > combinedChargingPowerAtHome)
        {
            logger.LogTrace("Grid power is enough for all cars.");
            usedGridPower = combinedChargingPowerAtHome;
        }
        else
        {
            usedGridPower = gridPower;
            combinedChargingPowerAtHome -= gridPower;
            logger.LogTrace("Using {usedGridPower} W from grid", usedGridPower);
            if (homeBatteryDischargingPower > combinedChargingPowerAtHome)
            {
                logger.LogTrace("Home battery power is enough for all cars.");
                usedHomeBatteryPower = combinedChargingPowerAtHome;
            }
            else
            {
                usedHomeBatteryPower = homeBatteryDischargingPower;
                logger.LogTrace("Using {usedHomeBatteryPower} W from home battery", usedHomeBatteryPower);
                combinedChargingPowerAtHome -= homeBatteryDischargingPower;
                usedSolarPower = combinedChargingPowerAtHome;
                logger.LogTrace("Using {usedSolarPower} W from solar", usedSolarPower);
            }
        }
        foreach (var car in settings.CarsToManage.OrderByDescending(c => c.ChargingPriority))
        {
            var chargingPowerAtHome = car.ChargingPowerAtHome ?? 0;
            if (chargingPowerAtHome < 1)
            {
                logger.LogTrace("Car with ID {carId} currently not charging at home so no charging detail to create.", car.Id);
                continue;
            }
            var chargingDetail = await GetAttachedChargingDetail(car.Id);
            if (chargingPowerAtHome < usedGridPower)
            {
                logger.LogTrace("Grid power is enough for car with ID {carId}.", car.Id);
                chargingDetail.GridPower = chargingPowerAtHome;
                usedGridPower -= chargingPowerAtHome;
                logger.LogTrace("Grid power after charging detail: {gridPower}", usedGridPower);
            }
            else
            {
                chargingDetail.GridPower = usedGridPower;
                usedGridPower = 0;
                chargingPowerAtHome -= chargingDetail.GridPower;
                logger.LogTrace("Remaining charging power after using gridPower for car {carId}: {chargingPowerAtHome}", car.Id, chargingPowerAtHome);
                if (chargingPowerAtHome < usedHomeBatteryPower)
                {
                    logger.LogTrace("Home battery power is enough for car with ID {carId}.", car.Id);
                    chargingDetail.HomeBatteryPower = chargingPowerAtHome;
                    usedHomeBatteryPower -= chargingPowerAtHome;
                    logger.LogTrace("Home battery power after charging detail: {homeBatteryDischargingPower}", usedHomeBatteryPower);
                }
                else
                {
                    chargingDetail.HomeBatteryPower = usedHomeBatteryPower;
                    usedHomeBatteryPower = 0;
                    chargingPowerAtHome -= chargingDetail.HomeBatteryPower;
                    logger.LogTrace("Remaining charging power after using home battery power for car {carId}: {chargingPowerAtHome}", car.Id, chargingPowerAtHome);
                    chargingDetail.SolarPower = chargingPowerAtHome;
                }
            }
            logger.LogTrace("Solar power after charging detail: {solarPower}", usedSolarPower);
            logger.LogTrace("Home battery power after charging detail: {homeBatteryDischargingPower}", usedHomeBatteryPower);
            logger.LogTrace("Grid power after charging detail: {gridPower}", usedGridPower);
            logger.LogTrace("Created charging detail: {@chargingDetail}", chargingDetail);
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<ChargingDetail> GetAttachedChargingDetail(int carId)
    {
        var latestOpenChargingProcessId = await context.ChargingProcesses
            .Where(cp => cp.CarId == carId && cp.EndDate == null)
            .OrderByDescending(cp => cp.StartDate)
            .Select(cp => cp.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        var chargingDetail = new ChargingDetail
        {
            TimeStamp = dateTimeProvider.UtcNow(),
        };
        if (latestOpenChargingProcessId == default)
        {
            var chargingProcess = new ChargingProcess
            {
                StartDate = chargingDetail.TimeStamp,
                CarId = carId,
            };
            context.ChargingProcesses.Add(chargingProcess);
            chargingProcess.ChargingDetails.Add(chargingDetail);
        }
        else
        {
            chargingDetail.ChargingProcessId = latestOpenChargingProcessId;
            context.ChargingDetails.Add(chargingDetail);
        }
        return chargingDetail;
    }
}
