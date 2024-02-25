using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class LatestTimeToReachSocUpdateService : ILatestTimeToReachSocUpdateService
{
    private readonly ILogger<LatestTimeToReachSocUpdateService> _logger;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigJsonService _configJsonService;

    public LatestTimeToReachSocUpdateService(ILogger<LatestTimeToReachSocUpdateService> logger, ISettings settings,
        IDateTimeProvider dateTimeProvider, IConfigJsonService configJsonService)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _configJsonService = configJsonService;
    }

    public async Task UpdateAllCars()
    {
        _logger.LogTrace("{method}()", nameof(UpdateAllCars));
        foreach (var car in _settings.CarsToManage)
        {
            if (car.ChargingPowerAtHome > 0)
            {
                _logger.LogInformation("Charge date is not updated as car {carId} is currently charging", car.Id);
                continue;
            }
            UpdateCarConfiguration(car);
            var carConfiguration = new CarConfiguration()
            {
                ChargeMode = car.ChargeMode,
                MinimumSoC = car.MinimumSoC,
                LatestTimeToReachSoC = car.LatestTimeToReachSoC,
                IgnoreLatestTimeToReachSocDate = car.IgnoreLatestTimeToReachSocDate,
                MaximumAmpere = car.MaximumAmpere,
                MinimumAmpere = car.MinimumAmpere,
                UsableEnergy = car.UsableEnergy,
                ShouldBeManaged = car.ShouldBeManaged,
                ShouldSetChargeStartTimes = car.ShouldSetChargeStartTimes,
                ChargingPriority = car.ChargingPriority
            };
            await _configJsonService.UpdateCarConfiguration(car.Vin, carConfiguration).ConfigureAwait(false);
        }
        
    }

    internal void UpdateCarConfiguration(DtoCar car)
    {
        _logger.LogTrace("{method}({@param})", nameof(UpdateCarConfiguration), car);

        var dateTimeOffSetNow = _dateTimeProvider.DateTimeOffSetNow();
        if (car.IgnoreLatestTimeToReachSocDate)
        {
            var dateToSet = dateTimeOffSetNow.DateTime.Date;
            if (car.LatestTimeToReachSoC.TimeOfDay <= dateTimeOffSetNow.ToLocalTime().TimeOfDay)
            {
                dateToSet = dateTimeOffSetNow.DateTime.AddDays(1).Date;
            }
            car.LatestTimeToReachSoC = dateToSet + car.LatestTimeToReachSoC.TimeOfDay;
        }
        else
        {
            var localDateTime = dateTimeOffSetNow.ToLocalTime().DateTime;
            if (car.LatestTimeToReachSoC.Date < localDateTime.Date)
            {
                car.LatestTimeToReachSoC = _dateTimeProvider.Now().Date.AddDays(-1) +
                                                        car.LatestTimeToReachSoC.TimeOfDay;
            }
        }
    }
}
