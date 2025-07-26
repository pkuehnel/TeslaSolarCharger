using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class CarConfigurationService(ILogger<CarConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IDateTimeProvider dateTimeProvider,
    ITeslaFleetApiService teslaFleetApiService,
    IConfigurationWrapper configurationWrapper,
    ITeslaMateDbContextWrapper teslaMateDbContextWrapper) : ICarConfigurationService
{
    public async Task AddAllMissingCarsFromTeslaAccount()
    {
        logger.LogTrace("{method}()", nameof(AddAllMissingCarsFromTeslaAccount));
        var teslaMateCars = new List<Model.Entities.TeslaMate.Car>();
        var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();
        if (teslaMateContext != default)
        {
            teslaMateCars = await teslaMateContext.Cars.ToListAsync();
        }
        var teslaAccountCarsResult = await teslaFleetApiService.GetAllCarsFromAccount().ConfigureAwait(false);
        var teslaAccountCars = teslaAccountCarsResult.Match(
            Succ: dtosList => dtosList,
            Fail: error =>
            {
                logger.LogError("Could not get new cars from Tesla account.");
                if (error.IsExceptional)
                {
                    throw error.ToException();
                }

                throw new Exception(error.Message);
            }// or any default value or throw an exception
        );

        var teslaSolarChargerCars = await teslaSolarChargerContext.Cars.ToListAsync();
        var highestChargingPriority = 0;
        if (teslaSolarChargerCars.Any())
        {
            highestChargingPriority = teslaSolarChargerCars.Max(c => c.ChargingPriority);
        }
        foreach (var teslaAccountCar in teslaAccountCars)
        {
            var teslaSolarChargerCar = teslaSolarChargerCars.FirstOrDefault(c => string.Equals(c.Vin, teslaAccountCar.Vin, StringComparison.CurrentCultureIgnoreCase));
            if (teslaSolarChargerCar != default)
            {
                var teslaMateCarId = teslaMateCars
                    .FirstOrDefault(c => string.Equals(c.Vin, teslaAccountCar.Vin, StringComparison.CurrentCultureIgnoreCase))?.Id;
                if(teslaSolarChargerCar.TeslaMateCarId != teslaMateCarId)
                {
                    teslaSolarChargerCar.TeslaMateCarId = teslaMateCarId;
                    await teslaSolarChargerContext.SaveChangesAsync();
                }

                if (!teslaSolarChargerCar.IsAvailableInTeslaAccount)
                {
                    teslaSolarChargerCar.IsAvailableInTeslaAccount = true;
                    await teslaSolarChargerContext.SaveChangesAsync();
                }
                continue;
            }
            teslaSolarChargerCar = new Car
            {
                TeslaMateCarId = teslaMateCars.FirstOrDefault(c => string.Equals(c.Vin, teslaAccountCar.Vin, StringComparison.CurrentCultureIgnoreCase))?.Id,
                Vin = teslaAccountCar.Vin,
                Name = teslaAccountCar.Name,
                TeslaFleetApiState = null,
                ChargeMode = ChargeModeV2.Auto,
                MinimumSoc = 10,
                LatestTimeToReachSoC = dateTimeProvider.UtcNow(),
                IgnoreLatestTimeToReachSocDate = false,
                IgnoreLatestTimeToReachSocDateOnWeekend = false,
                MaximumAmpere = 16,
                MinimumAmpere = 2,
                UsableEnergy = 75,
                ShouldBeManaged = true,
                ChargingPriority = ++highestChargingPriority,
            };
            teslaSolarChargerContext.Cars.Add(teslaSolarChargerCar);
            await teslaSolarChargerContext.SaveChangesAsync();
        }

        foreach (var teslaSolarChargerCar in teslaSolarChargerCars)
        {
            if (!teslaAccountCars.Any(c => string.Equals(c.Vin, teslaSolarChargerCar.Vin)))
            {
                logger.LogInformation("Car with VIN {vin} is not available in Tesla account anymore.", teslaSolarChargerCar.Vin);
                teslaSolarChargerCar.IsAvailableInTeslaAccount = false;
                teslaSolarChargerCar.ShouldBeManaged = false;
                await teslaSolarChargerContext.SaveChangesAsync();
            }
        }
    }
}
