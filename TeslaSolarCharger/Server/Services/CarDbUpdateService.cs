using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class CarDbUpdateService : ICarDbUpdateService
{
    private readonly ILogger<CarDbUpdateService> _logger;
    private readonly ISettings _settings;
    private readonly ITeslamateContext _teslamateContext;
    private readonly ITelegramService _telegramService;

    public CarDbUpdateService(ILogger<CarDbUpdateService> logger, ISettings settings, ITeslamateContext teslamateContext, ITelegramService telegramService)
    {
        _logger = logger;
        _settings = settings;
        _teslamateContext = teslamateContext;
        _telegramService = telegramService;
    }

    public async Task UpdateMissingCarDataFromDatabase()
    {
        _logger.LogTrace("{method}()", nameof(UpdateMissingCarDataFromDatabase));
        _logger.LogWarning("Deprecated method called");
        foreach (var car in _settings.Cars)
        {
            try
            {
                var batteryLevel = await _teslamateContext.Positions
                    .Where(p => p.CarId == car.Id)
                    .OrderByDescending(p => p.Date)
                    .Select(c => c.BatteryLevel)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                _logger.LogTrace("battery level for var {car} is {batteryLevel}", car.Id, batteryLevel);
                car.CarState.SoC = batteryLevel;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while trying to get pilot current from database. Retrying in one minute.");
            }


        }
    }
}
