using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TscOnlyChargingCostService(ILogger<TscOnlyChargingCostService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper) : ITscOnlyChargingCostService
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

    private async Task FinalizeChargingProcess(ChargingProcess chargingProcess)
    {
        logger.LogTrace("{method}({chargingProcessId})", nameof(FinalizeChargingProcess), chargingProcess.Id);
        var chargingDetails = await context.ChargingDetails
            .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
            .OrderBy(cd => cd.Id)
            .ToListAsync().ConfigureAwait(false);
        decimal usedSolarEnergy = 0;
        decimal usedGridEnergy = 0;
        decimal cost = 0;
        for (var index = 1; index < chargingDetails.Count; index++)
        {
            var chargingDetail = chargingDetails[index];
            var timeSpanSinceLastDetail = chargingDetail.TimeStamp - chargingDetails[index - 1].TimeStamp;
            usedSolarEnergy += (decimal)(chargingDetail.SolarPower * timeSpanSinceLastDetail.TotalHours);
            usedGridEnergy += (decimal)(chargingDetail.GridPower * timeSpanSinceLastDetail.TotalHours);
        }
        chargingProcess.EndDate = chargingDetails.Last().TimeStamp;
        chargingProcess.UsedSolarEnergy = usedSolarEnergy;
        chargingProcess.UsedGridEnergy = usedGridEnergy;
        chargingProcess.Cost = cost;
        await context.SaveChangesAsync().ConfigureAwait(false);
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
