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
        await _configJsonService.UpdateCarConfiguration().ConfigureAwait(false);
    }

    internal void UpdateCarConfiguration(CarConfiguration carConfiguration)
    {
        _logger.LogTrace("{method}({@param})", nameof(UpdateCarConfiguration), carConfiguration);
        if (carConfiguration.ShouldBeManaged != true)
        {
            return;
        }

        var dateTimeOffSetNow = _dateTimeProvider.DateTimeOffSetNow();
        if (carConfiguration.IgnoreLatestTimeToReachSocDate)
        {
            var dateToSet = dateTimeOffSetNow.DateTime.Date;
            if (carConfiguration.LatestTimeToReachSoC.TimeOfDay <= dateTimeOffSetNow.ToLocalTime().TimeOfDay)
            {
                dateToSet = dateTimeOffSetNow.DateTime.AddDays(1).Date;
            }
            carConfiguration.LatestTimeToReachSoC = dateToSet + carConfiguration.LatestTimeToReachSoC.TimeOfDay;
        }
        else
        {
            var localDateTime = dateTimeOffSetNow.ToLocalTime().DateTime;
            if (carConfiguration.LatestTimeToReachSoC.Date < localDateTime.Date)
            {
                carConfiguration.LatestTimeToReachSoC = _dateTimeProvider.Now().Date.AddDays(-1) +
                                                        carConfiguration.LatestTimeToReachSoC.TimeOfDay;
            }
        }
    }
}
