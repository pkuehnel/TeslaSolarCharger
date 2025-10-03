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
    private readonly ILoadPointManagementService _loadPointManagementService;

    public CarValueEstimationService(ILogger<CarValueEstimationService> logger,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        ILoadPointManagementService loadPointManagementService)
    {
        _logger = logger;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _loadPointManagementService = loadPointManagementService;
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
        _logger.LogTrace("{method}({carId}) started", nameof(UpdateSocEstimation), car.Id);
        var lastNonEstimatedSoc = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.StateOfCharge
                          && cvl.Source > CarValueSource.Estimation)
            .OrderByDescending(cvl => cvl.Timestamp)
            .Select(cvl => new { Timestamp = new DateTimeOffset(cvl.Timestamp, TimeSpan.Zero), cvl.IntValue })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("{method} lastNonEstimatedSoc: {@lastNonEstimatedSoc}", nameof(UpdateSocEstimation), lastNonEstimatedSoc);
        if (lastNonEstimatedSoc?.IntValue == null)
        {
            _logger.LogTrace("{method} exiting: no lastNonEstimatedSoc", nameof(UpdateSocEstimation));
            return;
        }

        var lastPluggedOutObject = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.IsPluggedIn
                          && cvl.BooleanValue == false)
            .OrderByDescending(cvl => cvl.Timestamp)
            .Select(cvl => new { cvl.Timestamp })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("{method} lastPluggedOutObject: {@lastPluggedOutObject}", nameof(UpdateSocEstimation), lastPluggedOutObject);
        var lastPluggedOutTimeStamp = lastPluggedOutObject == default
            ? DateTimeOffset.MinValue
            : new(lastPluggedOutObject.Timestamp, TimeSpan.Zero);
        _logger.LogTrace("{method} lastPluggedOutTimeStamp: {lastPluggedOutTimeStamp}", nameof(UpdateSocEstimation), lastPluggedOutTimeStamp);
        var firstPluggedInAfterPlugOutObject = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.IsPluggedIn
                          && cvl.BooleanValue == true
                          && cvl.Timestamp > lastPluggedOutTimeStamp.DateTime)
            .OrderBy(cvl => cvl.Timestamp)
            .Select(cvl => new { cvl.Timestamp })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("{method} firstPluggedInAfterPlugOutObject: {@firstPluggedInAfterPlugOutObject}", nameof(UpdateSocEstimation), firstPluggedInAfterPlugOutObject);
        if (firstPluggedInAfterPlugOutObject == default)
        {
            _logger.LogTrace("{method} exiting: no firstPluggedInAfterPlugOutObject", nameof(UpdateSocEstimation));
            return;
        }

        var firstPluggedInAfterPlugOutTimeStamp = new DateTimeOffset(firstPluggedInAfterPlugOutObject.Timestamp, TimeSpan.Zero);
        _logger.LogTrace("{method} firstPluggedInAfterPlugOutTimeStamp: {firstPluggedInAfterPlugOutTimeStamp}", nameof(UpdateSocEstimation), firstPluggedInAfterPlugOutTimeStamp);
        if (lastNonEstimatedSoc.Timestamp < firstPluggedInAfterPlugOutTimeStamp)
        {
            _logger.LogTrace("{method} exiting: lastNonEstimatedSoc before firstPluggedInAfterPlugOut", nameof(UpdateSocEstimation));
            return;
        }

        var chargedEnergyAtLastNonEstimatedSoc = await _context.MeterValues
            .Where(m => m.Timestamp >= lastNonEstimatedSoc.Timestamp
                        && m.CarId == car.Id
                        && m.MeterValueKind == MeterValueKind.Car)
            .OrderBy(m => m.Timestamp)
            .Select(m => m.EstimatedEnergyWs)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("{method} chargedEnergyAtLastNonEstimatedSoc: {chargedEnergyAtLastNonEstimatedSoc}", nameof(UpdateSocEstimation), chargedEnergyAtLastNonEstimatedSoc);
        if (chargedEnergyAtLastNonEstimatedSoc == default)
        {
            _logger.LogTrace("{method} exiting: no chargedEnergyAtLastNonEstimatedSoc", nameof(UpdateSocEstimation));
            return;
        }

        var latestChargedEnergy = await _context.MeterValues
            .Where(m => m.Timestamp >= firstPluggedInAfterPlugOutTimeStamp
                        && m.CarId == car.Id
                        && m.MeterValueKind == MeterValueKind.Car)
            .OrderByDescending(m => m.Timestamp)
            .Select(m => m.EstimatedEnergyWs)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        _logger.LogTrace("{method} latestChargedEnergy: {latestChargedEnergy}", nameof(UpdateSocEstimation), latestChargedEnergy);
        if (latestChargedEnergy == default)
        {
            _logger.LogTrace("{method} exiting: no latestChargedEnergy", nameof(UpdateSocEstimation));
            return;
        }

        var chargedSinceLastNonEstimatedSoc = latestChargedEnergy.Value - chargedEnergyAtLastNonEstimatedSoc.Value;
        _logger.LogTrace("{method} chargedSinceLastNonEstimatedSoc: {chargedSinceLastNonEstimatedSoc}", nameof(UpdateSocEstimation), chargedSinceLastNonEstimatedSoc);
        var carBatteryCapacity = car.UsableEnergy * 3_600_000; // kWh to Ws
        _logger.LogTrace("{method} carBatteryCapacity: {carBatteryCapacity}", nameof(UpdateSocEstimation), carBatteryCapacity);
        var estimatedSoc = (int)(lastNonEstimatedSoc.IntValue.Value + (((float)chargedSinceLastNonEstimatedSoc / carBatteryCapacity) * 100));
        _logger.LogTrace("{method} estimatedSoc: {estimatedSoc}", nameof(UpdateSocEstimation), estimatedSoc);
        var estimatedSocCarValueLog = new CarValueLog()
        {
            CarId = car.Id,
            Timestamp = _dateTimeProvider.UtcNow(),
            Type = CarValueType.StateOfCharge,
            IntValue = estimatedSoc,
            Source = CarValueSource.Estimation,
        };
        _logger.LogTrace("{method} adding estimatedSocCarValueLog: {@estimatedSocCarValueLog}", nameof(UpdateSocEstimation), estimatedSocCarValueLog);
        _context.CarValueLogs.Add(estimatedSocCarValueLog);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var settingsCar = _settings.Cars.FirstOrDefault(c => c.Id == car.Id);
        if (settingsCar == default)
        {
            _logger.LogTrace("{method} exiting: no settingsCar found", nameof(UpdateSocEstimation));
            return;
        }

        settingsCar.SoC.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), estimatedSoc);
        _logger.LogTrace("{method} completed successfully with estimatedSoc={estimatedSoc}", nameof(UpdateSocEstimation), estimatedSoc);
        await _loadPointManagementService.CarStateChanged(car.Id);
    }

}
