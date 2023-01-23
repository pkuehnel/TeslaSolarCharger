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

    public LatestTimeToReachSocUpdateService(ILogger<LatestTimeToReachSocUpdateService> logger, ISettings settings,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
    }

    public void UpdateAllCars()
    {
        _logger.LogTrace("{method}()", nameof(UpdateAllCars));
        foreach (var car in _settings.Cars)
        {
            if (car.CarState.ChargingPowerAtHome > 0)
            {
                _logger.LogInformation("Charge date is not updated as car {carId} is currently charging", car.Id);
                continue;
            }
            var carConfiguration = car.CarConfiguration;
            UpdateCarConfiguration(carConfiguration);
        }
    }

    internal void UpdateCarConfiguration(CarConfiguration carConfiguration)
    {
        _logger.LogTrace("{method}({@param})", nameof(UpdateCarConfiguration), carConfiguration);
        if (carConfiguration.ShouldBeManaged != true)
        {
            return;
        }

        var dateTimeOffSetNow = _dateTimeProvider.DateTimeOffSetNow();
        if (carConfiguration.LatestTimeToReachSoC < dateTimeOffSetNow)
        {
            carConfiguration.LatestTimeToReachSoC = _dateTimeProvider.Now().Date.AddDays(-1) +
                                                    carConfiguration.LatestTimeToReachSoC.TimeOfDay;
        }

        if (carConfiguration.IgnoreLatestTimeToReachSocDate)
        {
            carConfiguration.LatestTimeToReachSoC =
                _dateTimeProvider.Now().Date + carConfiguration.LatestTimeToReachSoC.TimeOfDay;
        }

        if (carConfiguration.LatestTimeToReachSoC.TimeOfDay <= dateTimeOffSetNow.ToLocalTime().TimeOfDay)
        {
            carConfiguration.LatestTimeToReachSoC = carConfiguration.LatestTimeToReachSoC.AddDays(1);
        }
    }
}
