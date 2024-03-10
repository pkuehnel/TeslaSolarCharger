using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TscOnlyChargingCostService(ILogger<TscOnlyChargingCostService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IServiceProvider serviceProvider) : ITscOnlyChargingCostService
{
    public async Task FinalizeFinishedChargingProcesses()
    {
        logger.LogTrace("{method}()", nameof(FinalizeFinishedChargingProcesses));
        var openChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate == null)
            .ToListAsync().ConfigureAwait(false);
        var timeSpanToHandleChargingProcessAsCompleted = TimeSpan.FromMinutes(10);
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

    private async Task FinalizeChargingProcess(ChargingProcess chargingProcess)
    {
        logger.LogTrace("{method}({chargingProcessId})", nameof(FinalizeChargingProcess), chargingProcess.Id);
        
        var chargingDetails = await context.ChargingDetails
            .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
            .OrderBy(cd => cd.TimeStamp)
            .ToListAsync().ConfigureAwait(false);
        decimal usedSolarEnergyWh = 0;
        decimal usedGridEnergyWh = 0;
        decimal cost = 0;
        chargingProcess.EndDate = chargingDetails.Last().TimeStamp;
        var prices = await GetPricesInTimeSpan(chargingProcess.StartDate, chargingProcess.EndDate.Value);
        for (var index = 1; index < chargingDetails.Count; index++)
        {
            var price = GetPriceByTimeStamp(prices, chargingDetails[index].TimeStamp);
            var chargingDetail = chargingDetails[index];
            var timeSpanSinceLastDetail = chargingDetail.TimeStamp - chargingDetails[index - 1].TimeStamp;
            var usedSolarWhSinceLastChargingDetail = (decimal)(chargingDetail.SolarPower * timeSpanSinceLastDetail.TotalHours);
            usedSolarEnergyWh += usedSolarWhSinceLastChargingDetail;
            var usedGridPowerSinceLastChargingDetail = (decimal)(chargingDetail.GridPower * timeSpanSinceLastDetail.TotalHours);
            usedGridEnergyWh += usedGridPowerSinceLastChargingDetail;
            cost += usedGridPowerSinceLastChargingDetail * price.Value;
            cost += usedSolarWhSinceLastChargingDetail * price.SolarPrice;
        }
        chargingProcess.UsedSolarEnergyKwh = usedSolarEnergyWh / 1000m;
        chargingProcess.UsedGridEnergyKwh = usedGridEnergyWh / 1000m;
        chargingProcess.Cost = cost;
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
        var toDateTimeOffset = new DateTimeOffset(to, TimeSpan.Zero);
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
        var availableSolarPower = GetSolarPower();
        foreach (var car in settings.CarsToManage.OrderBy(c => c.ChargingPriority))
        {
            var chargingPowerAtHome = car.ChargingPowerAtHome ?? 0;
            if (chargingPowerAtHome < 1)
            {
                logger.LogTrace("Car with ID {carId} 0 Watt chargingPower at home", car.Id);
                continue;
            }
            var chargingDetail = await GetAttachedChargingDetail(car.Id);
            if (chargingPowerAtHome < availableSolarPower)
            {
                chargingDetail.SolarPower = chargingPowerAtHome;
                chargingDetail.GridPower = 0;
            }
            else
            {
                chargingDetail.SolarPower = availableSolarPower;
                chargingDetail.GridPower = chargingPowerAtHome - availableSolarPower;
            }
            availableSolarPower -= chargingDetail.SolarPower;
            if (availableSolarPower < 0)
            {
                availableSolarPower = 0;
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private int GetSolarPower()
    {
        var solarPower = settings.Overage;
        if (solarPower == default && settings.InverterPower != default)
        {
            //no grid meter available, so we have to calculate the power by using the inverter power and the power buffer
            var powerBuffer = configurationWrapper.PowerBuffer(true);
            solarPower = settings.InverterPower
                         //if powerBuffer is negative, it will be subtracted as it should be expected home power usage when no grid meter is available
                         - (powerBuffer > 0 ? powerBuffer : 0);
        }

        if (solarPower == default)
        {
            logger.LogInformation("No solar power available, using 0 as default.");
            return 0;
        }
        return (int)solarPower;
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
        }
        return chargingDetail;
    }
}
