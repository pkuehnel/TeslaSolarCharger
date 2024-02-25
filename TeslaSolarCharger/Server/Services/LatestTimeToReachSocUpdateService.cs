using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class LatestTimeToReachSocUpdateService(
    ILogger<LatestTimeToReachSocUpdateService> logger,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    ITeslaSolarChargerContext teslaSolarChargerContext)
    : ILatestTimeToReachSocUpdateService
{

    public async Task UpdateAllCars()
    {
        logger.LogTrace("{method}()", nameof(UpdateAllCars));
        foreach (var car in settings.CarsToManage)
        {
            if (car.ChargingPowerAtHome > 0)
            {
                logger.LogInformation("Charge date is not updated as car {carId} is currently charging", car.Id);
                continue;
            }
            var newTime = GetNewLatestTimeToReachSoc(car);
            if (newTime.Equals(car.LatestTimeToReachSoC))
            {
                continue;
            }
            var databaseCar = teslaSolarChargerContext.Cars.First(c => c.Id == car.Id);
            databaseCar.LatestTimeToReachSoC = newTime;
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            car.LatestTimeToReachSoC = newTime;
        }
        
    }

    internal DateTime GetNewLatestTimeToReachSoc(DtoCar car)
    {
        logger.LogTrace("{method}({@param})", nameof(GetNewLatestTimeToReachSoc), car);

        var dateTimeOffSetNow = dateTimeProvider.DateTimeOffSetNow();
        if (car.IgnoreLatestTimeToReachSocDate)
        {
            var dateToSet = dateTimeOffSetNow.DateTime.Date;
            if (car.LatestTimeToReachSoC.TimeOfDay <= dateTimeOffSetNow.ToLocalTime().TimeOfDay)
            {
                dateToSet = dateTimeOffSetNow.DateTime.AddDays(1).Date;
            }
            return dateToSet + car.LatestTimeToReachSoC.TimeOfDay;
        }

        var localDateTime = dateTimeOffSetNow.ToLocalTime().DateTime;
        if (car.LatestTimeToReachSoC.Date < localDateTime.Date)
        {
            return dateTimeProvider.Now().Date.AddDays(-1) +
                   car.LatestTimeToReachSoC.TimeOfDay;
        }
        return car.LatestTimeToReachSoC;
    }
}
