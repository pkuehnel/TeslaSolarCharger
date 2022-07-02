using Microsoft.EntityFrameworkCore;
using SmartTeslaAmpSetter.Model.Contracts;
using SmartTeslaAmpSetter.Server.Contracts;
using SmartTeslaAmpSetter.Shared.Dtos.Contracts;

namespace SmartTeslaAmpSetter.Server.Services;

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
            var pilotCurrent = await _teslamateContext.Charges
                .Where(c => c.ChargingProcess.CarId == car.Id)
                .OrderByDescending(c => c.Date)
                .Select(c => c.ChargerPilotCurrent)
                .FirstOrDefaultAsync();
            _logger.LogTrace("Pilot Current for var {car} is {pilotCurrent}", car.Id, pilotCurrent);

            //ToDo: Remove telegram notification
            if (pilotCurrent < 16 && car.CarState.ChargerActualCurrent > 0)
            {
                await _telegramService.SendMessage($"Pilot Current for var {car.Id} is {pilotCurrent}");
            }
        }
    }
}