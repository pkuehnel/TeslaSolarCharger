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
        var openChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate != null)
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingProcess in openChargingProcesses)
        {
            try
            {
                await FinalizeChargingProcess(chargingProcess);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while updating charge prices of charging process with ID {chargingProcessId}.", chargingProcess.Id);
            }
        }
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
                .ForMember(d => d.UsedSolarEnergy, opt => opt.MapFrom(h => h.UsedSolarEnergyKwh == null ? 0m : Math.Round(h.UsedSolarEnergyKwh.Value, 2)))
                ;
        });

        var handledCharges = await context.ChargingProcesses
            .Where(h => h.CarId == carId && h.Cost != null)
            .OrderByDescending(h => h.StartDate)
            .ProjectTo<DtoHandledCharge>(mapper)
            .ToListAsync().ConfigureAwait(false);

        handledCharges.RemoveAll(c => (c.UsedGridEnergy + c.UsedSolarEnergy) < 0.1m);
        foreach (var dtoHandledCharge in handledCharges)
        {
            dtoHandledCharge.PricePerKwh = Math.Round(dtoHandledCharge.CalculatedPrice / (dtoHandledCharge.UsedGridEnergy + dtoHandledCharge.UsedSolarEnergy), 3);
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
        var prices = await GetPricesInTimeSpan(chargingProcess.StartDate, chargingProcess.EndDate.Value);
        //When a charging process is stopped and resumed later, the last charging detail is too old and should not be used because it would use the last value dring the whole time althoug the car was not charging
        var maxChargingDetailsDuration = TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds).Add(TimeSpan.FromSeconds(10));
        for (var index = 1; index < chargingDetails.Count; index++)
        {
            var price = GetPriceByTimeStamp(prices, chargingDetails[index].TimeStamp);
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
            cost += usedHomeBatteryEnergyWh * price.SolarPrice;
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

    public async Task AddChargingDetailsForAllCars()
    {
        logger.LogTrace("{method}()", nameof(AddChargingDetailsForAllCars));
        var powerBuffer = configurationWrapper.PowerBuffer(true);
        var overage = settings.Overage ?? (settings.InverterPower - (powerBuffer < 0 ? 0 : powerBuffer));
        var homeBatteryDischargingPower =  (- settings.HomeBatteryPower) ?? 0;
        var solarPower = overage ?? 0;
        var remainingHomeBatteryPower = homeBatteryDischargingPower;
        logger.LogTrace("SolarPower: {solarPower}", solarPower);
        logger.LogTrace("HomeBatteryDischargingPower: {homeBatteryDischargingPower}", homeBatteryDischargingPower);
        foreach (var car in settings.Cars)
        {
            logger.LogTrace("Add chargingpower {power} of car {carId} to solar power", car.ChargingPowerAtHome, car.Id);
            if (car.ChargingPowerAtHome is null or 0)
            {
                logger.LogTrace("Car not charging at home, do not add any charging power");
                continue;
            }
            if (remainingHomeBatteryPower > 0)
            {
                logger.LogTrace("{remainingHomeBatteryPower}W home battery power remaining", remainingHomeBatteryPower);
                if (remainingHomeBatteryPower > car.ChargingPowerAtHome)
                {
                    logger.LogTrace("Home battery power is enough to charge car {carId} with {chargingPowerAtHome}W", car.Id, car.ChargingPowerAtHome);
                    remainingHomeBatteryPower -= car.ChargingPowerAtHome.Value;
                }
                else
                {
                    logger.LogTrace("Home battery power is not enough to charge car {carId} with {chargingPowerAtHome}W", car.Id, car.ChargingPowerAtHome);
                    solarPower += car.ChargingPowerAtHome.Value - remainingHomeBatteryPower;
                    remainingHomeBatteryPower = 0;
                }
                continue;
            }
            logger.LogTrace("Add charging power of car {carId} to solar power", car.Id);
            solarPower += car.ChargingPowerAtHome.Value;
        }

        if (solarPower < 0)
        {
            logger.LogTrace("Even after adding car charging powers no solarPower availabale");
            solarPower = 0;
        }
        foreach (var car in settings.CarsToManage.OrderBy(c => c.ChargingPriority))
        {
            var chargingPowerAtHome = car.ChargingPowerAtHome ?? 0;
            if (chargingPowerAtHome < 1)
            {
                logger.LogTrace("Car with ID {carId} currently not charging at home so no charging detail to create.", car.Id);
                continue;
            }
            var chargingDetail = await GetAttachedChargingDetail(car.Id);
            if ((solarPower - chargingPowerAtHome) > 0)
            {
                logger.LogTrace("Solar power {solarPower} is enough to charge car {carId} with {chargingPowerAtHome}W", solarPower, car.Id, chargingPowerAtHome);
                chargingDetail.SolarPower = chargingPowerAtHome;
                chargingDetail.HomeBatteryPower = 0;
                chargingDetail.GridPower = 0;
            }
            else
            {
                logger.LogTrace("Solar power {solarPower} is not enough to charge car {carId} with {chargingPowerAtHome}W", solarPower, car.Id, chargingPowerAtHome);
                chargingDetail.SolarPower = solarPower;
                var remainingPower = chargingPowerAtHome - chargingDetail.SolarPower;
                logger.LogTrace("Remaining power after remove solarpower: {remainingPower}", remainingPower);

                if ((homeBatteryDischargingPower - remainingPower) > 0)
                {
                    logger.LogTrace("Home battery power {homeBatteryDischargingPower} is enough to charge car {carId} with {chargingPowerAtHome}W", homeBatteryDischargingPower, car.Id, chargingPowerAtHome);
                    chargingDetail.HomeBatteryPower = remainingPower;
                    chargingDetail.GridPower = 0;
                }
                else
                {
                    logger.LogTrace("Home battery power {homeBatteryDischargingPower} is not enough to charge car {carId} with {chargingPowerAtHome}W", homeBatteryDischargingPower, car.Id, chargingPowerAtHome);
                    chargingDetail.HomeBatteryPower = homeBatteryDischargingPower;
                    chargingDetail.GridPower = remainingPower - chargingDetail.HomeBatteryPower;
                }
            }
            solarPower -= chargingDetail.SolarPower;
            homeBatteryDischargingPower -= chargingDetail.HomeBatteryPower;
            if (solarPower < 0)
            {
                solarPower = 0;
            }
            if (homeBatteryDischargingPower < 0)
            {
                homeBatteryDischargingPower = 0;
            }
            logger.LogTrace("Solar power after charging detail: {solarPower}", solarPower);
            logger.LogTrace("Home battery power after charging detail: {homeBatteryDischargingPower}", homeBatteryDischargingPower);
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
