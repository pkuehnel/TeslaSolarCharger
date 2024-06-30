using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks.Sources;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Services.Services;

public class CarConfigurationService(ILogger<CarConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITeslamateContext teslamateContext,
    IDateTimeProvider dateTimeProvider) : ICarConfigurationService
{
    public async Task AddAllMissingTeslaMateCars()
    {
        logger.LogTrace("{method}()", nameof(AddAllMissingTeslaMateCars));
        var teslaMateCars = await teslamateContext.Cars.ToListAsync();
        var teslaSolarChargerCars = await teslaSolarChargerContext.Cars.ToListAsync();
        var highestChargingPriority = 0;
        if (teslaSolarChargerCars.Any())
        {
            highestChargingPriority = teslaSolarChargerCars.Max(c => c.ChargingPriority);
        }
        foreach (var teslaMateCar in teslaMateCars)
        {
            var vin = teslaMateCar.Vin;
            if (string.IsNullOrWhiteSpace(vin))
            {
                logger.LogWarning("Car with id {id} has no vin", teslaMateCar.Id);
                continue;
            }
            if (teslaSolarChargerContext.Cars.Any(c => c.Vin == vin))
            {
                continue;
            }
            var teslaSolarChargerCar = new Car
            {
                TeslaMateCarId = teslaMateCar.Id,
                Vin = vin,
                Name = teslaMateCar.Name,
                TeslaFleetApiState = TeslaCarFleetApiState.NotConfigured,
                ChargeMode = ChargeMode.PvAndMinSoc,
                MinimumSoc = 10,
                LatestTimeToReachSoC = dateTimeProvider.UtcNow(),
                IgnoreLatestTimeToReachSocDate = false,
                IgnoreLatestTimeToReachSocDateOnWeekend = false,
                MaximumAmpere = 16,
                MinimumAmpere = 6,
                UsableEnergy = 75,
                ShouldBeManaged = true,
                ShouldSetChargeStartTimes = false,
                ChargingPriority = ++highestChargingPriority,
            };
            teslaSolarChargerContext.Cars.Add(teslaSolarChargerCar);
            await teslaSolarChargerContext.SaveChangesAsync();
        }
    }
}
