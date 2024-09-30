using LanguageExt;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using ZXing;

namespace TeslaSolarCharger.Server.Services;

public class CarConfigurationService(ILogger<CarConfigurationService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITeslamateContext teslamateContext,
    IDateTimeProvider dateTimeProvider,
    ITeslaFleetApiService teslaFleetApiService,
    ISettings settings) : ICarConfigurationService
{
    public async Task AddAllMissingCarsFromTeslaAccount()
    {
        logger.LogTrace("{method}()", nameof(AddAllMissingCarsFromTeslaAccount));
        var teslaMateCars = new List<Model.Entities.TeslaMate.Car>();
        if (settings.UseTeslaMate)
        {
            teslaMateCars = await teslamateContext.Cars.ToListAsync();
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
            if (teslaSolarChargerCars.Any(c => string.Equals(c.Vin, teslaAccountCar.Vin, StringComparison.CurrentCultureIgnoreCase)))
            {
                continue;
            }
            var teslaSolarChargerCar = new Car
            {
                TeslaMateCarId = teslaMateCars.FirstOrDefault(c => string.Equals(c.Vin, teslaAccountCar.Vin, StringComparison.CurrentCultureIgnoreCase))?.Id,
                Vin = teslaAccountCar.Vin,
                Name = teslaAccountCar.Name,
                TeslaFleetApiState = null,
                ChargeMode = ChargeMode.PvAndMinSoc,
                MinimumSoc = 10,
                LatestTimeToReachSoC = dateTimeProvider.UtcNow(),
                IgnoreLatestTimeToReachSocDate = false,
                IgnoreLatestTimeToReachSocDateOnWeekend = false,
                MaximumAmpere = 16,
                MinimumAmpere = 6,
                UsableEnergy = 75,
                ShouldBeManaged = true,
                ChargingPriority = ++highestChargingPriority,
            };
            teslaSolarChargerContext.Cars.Add(teslaSolarChargerCar);
            await teslaSolarChargerContext.SaveChangesAsync();
        }
    }
}
