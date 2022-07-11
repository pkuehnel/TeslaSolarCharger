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

    public async Task UpdateCarsFromDatabase()
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarsFromDatabase));
        foreach (var car in _settings.Cars)
        {
            try
            {
                var pilotCurrent = await _teslamateContext.Charges
                    .Where(c => c.ChargingProcess.CarId == car.Id)
                    .OrderByDescending(c => c.Date)
                    .Select(c => c.ChargerPilotCurrent)
                    .FirstOrDefaultAsync();
                _logger.LogTrace("Pilot Current for var {car} is {pilotCurrent}", car.Id, pilotCurrent);
                car.CarState.ChargerPilotCurrent = pilotCurrent;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error while trying to get pilot current from database. Retrying in one minute.");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            
            
        }
    }
}