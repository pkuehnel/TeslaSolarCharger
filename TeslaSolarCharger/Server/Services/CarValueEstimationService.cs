using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class CarValueEstimationService : ICarValueEstimationService
{
    private readonly ILogger<CarValueEstimationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;

    public CarValueEstimationService(ILogger<CarValueEstimationService> logger,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        ISettings settings)
    {
        _logger = logger;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
    }


    public async Task UpdateAllCarValueEstimations(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(UpdateAllCarValueEstimations));
        var carsToEstimateValuesFor = await _context.Cars
            .Where(c => c.ShouldBeManaged == true && c.CarType != CarType.Tesla)
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var car in carsToEstimateValuesFor)
        {
            await UpdateSocEstimation(car, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UpdateSocEstimation(Car car, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({carId})", nameof(UpdateSocEstimation), car.Id);
        var lastNonEstimatedSoc = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.StateOfCharge
                          && cvl.Source > CarValueSource.Estimation)
            .OrderByDescending(cvl => cvl.Timestamp)
            .Select(cvl => new { Timestamp = new DateTimeOffset(cvl.Timestamp, TimeSpan.Zero), cvl.IntValue })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (lastNonEstimatedSoc?.IntValue == null)
        {
            return;
        }
        var lastPluggedOutObject = await _context.CarValueLogs
        .Where(cvl => cvl.CarId == car.Id
                      && cvl.Type == CarValueType.IsPluggedIn
                      && cvl.BooleanValue == false)
        .OrderByDescending(cvl => cvl.Timestamp)
        .Select(cvl => new { cvl.Timestamp })
        .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var lastPluggedOutTimeStamp = lastPluggedOutObject == default
            ? DateTimeOffset.MinValue
            : new(lastPluggedOutObject.Timestamp, TimeSpan.Zero);
        var firstPluggedInAfterPlugOutObject = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.IsPluggedIn
                          && cvl.BooleanValue == true
                          && cvl.Timestamp > lastPluggedOutTimeStamp.DateTime)
            .OrderBy(cvl => cvl.Timestamp)
            .Select(cvl => new { cvl.Timestamp })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (firstPluggedInAfterPlugOutObject == default)
        {
            return;
        }
        var firstPluggedInAfterPlugOutTimeStamp = new DateTimeOffset(firstPluggedInAfterPlugOutObject.Timestamp, TimeSpan.Zero);
        if (lastNonEstimatedSoc.Timestamp < firstPluggedInAfterPlugOutTimeStamp)
        {
            return;
        }
        var chargedEnergyAtLastNonEstimatedSoc = await _context.MeterValues
            .Where(m => m.Timestamp >= lastNonEstimatedSoc.Timestamp
                        && m.CarId == car.Id
                        && m.MeterValueKind == MeterValueKind.Car)
            .OrderBy(m => m.Timestamp)
            .Select(m => m.EstimatedEnergyWs)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (chargedEnergyAtLastNonEstimatedSoc == default)
        {
            return;
        }
        var latestChargedEnergy = await _context.MeterValues
            .Where(m => m.Timestamp >= firstPluggedInAfterPlugOutTimeStamp
                        && m.CarId == car.Id
                        && m.MeterValueKind == MeterValueKind.Car)
            .OrderByDescending(m => m.Timestamp)
            .Select(m => m.EstimatedEnergyWs)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        if (latestChargedEnergy == default)
        {
            return;
        }
        var chargedSinceLastNonEstimatedSoc = latestChargedEnergy.Value - chargedEnergyAtLastNonEstimatedSoc.Value;
        var carBatteryCapacity = car.UsableEnergy * 3_600_000; //kWh to Ws
        var estimatedSoc = (int)(lastNonEstimatedSoc.IntValue.Value + ((float)chargedSinceLastNonEstimatedSoc / carBatteryCapacity));
        var estimatedSocCarValueLog = new CarValueLog()
        {
            CarId = car.Id,
            Timestamp = _dateTimeProvider.UtcNow(),
            Type = CarValueType.StateOfCharge,
            IntValue = estimatedSoc,
            Source = CarValueSource.Estimation,
        };
        _context.CarValueLogs.Add(estimatedSocCarValueLog);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var settingsCar = _settings.Cars.FirstOrDefault(c => c.Id == car.Id);
        if (settingsCar == default)
        {
            return;
        }
        settingsCar.SoC.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), estimatedSoc);
    }
}
