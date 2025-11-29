using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class CarValueEstimationService : ICarValueEstimationService
{
    private readonly ILogger<CarValueEstimationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly IConstants _constants;

    public CarValueEstimationService(ILogger<CarValueEstimationService> logger,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        ILoadPointManagementService loadPointManagementService,
        IConstants constants)
    {
        _logger = logger;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _loadPointManagementService = loadPointManagementService;
        _constants = constants;
    }

    public async Task PlugoutCarsAndClearSocIfRequired(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(PlugoutCarsAndClearSocIfRequired));
        var manualCarIds = await _context.Cars
            .Where(c => c.ShouldBeManaged == true && c.CarType == CarType.Manual)
            .Select(c => c.Id)
            .ToHashSetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var manualCarId in manualCarIds)
        {
            var car = _settings.Cars.FirstOrDefault(c => c.Id == manualCarId);
            if (car == default)
            {
                continue;
            }
            _logger.LogTrace("Car {id} last connector match: {lastMatch}", car.Id, car.LastMatchedToChargingConnector);
            var lastConnectorMatch = car.LastMatchedToChargingConnector ?? _settings.StartupTime;
            _logger.LogTrace("Using {timestamp} as last connector match", lastConnectorMatch);
            if (lastConnectorMatch < currentDate.AddMinutes(-_constants.ManualCarMinutesUntilForgetSoc))
            {
                _logger.LogTrace("Plugging out manual car {carId} and clearing SoC as last connector match was more than {minutes} minutes ago", car.Id, _constants.ManualCarMinutesUntilForgetSoc);
                car.PluggedIn.Update(currentDate, false);
                car.SoC.Update(currentDate, null);
                await _loadPointManagementService.CarStateChanged(car.Id);
            }
        }
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
        _logger.LogTrace("lastNonEstimatedSoc: {@lastNonEstimatedSoc}", lastNonEstimatedSoc);
        if (lastNonEstimatedSoc?.IntValue == null)
        {
            _logger.LogTrace("exiting: no lastNonEstimatedSoc");
            return;
        }

        _logger.LogTrace("Loading plug state changes since lastNonEstimatedSoc for car {carId}", car.Id);
        // All plug state changes after lastNonEstimatedSoc
        var plugChanges = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.IsPluggedIn
                          && cvl.Timestamp > lastNonEstimatedSoc.Timestamp.UtcDateTime)
            .OrderBy(cvl => cvl.Timestamp)
            .Select(cvl => new { cvl.Timestamp, cvl.BooleanValue })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Last plug state before / at lastNonEstimatedSoc (to know what state we started in)
        var plugStateBeforeLastNonEstimatedSoc = await _context.CarValueLogs
            .Where(cvl => cvl.CarId == car.Id
                          && cvl.Type == CarValueType.IsPluggedIn
                          && cvl.Timestamp <= lastNonEstimatedSoc.Timestamp.UtcDateTime)
            .OrderByDescending(cvl => cvl.Timestamp)
            .Select(cvl => new { cvl.Timestamp, cvl.BooleanValue })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogTrace("plugStateBeforeLastNonEstimatedSoc: {@plugStateBeforeLastNonEstimatedSoc}", plugStateBeforeLastNonEstimatedSoc);

        if (plugStateBeforeLastNonEstimatedSoc != default)
        {
            plugChanges.Insert(0, plugStateBeforeLastNonEstimatedSoc);
        }

        _logger.LogTrace("Plug states (including before lastNonEstimatedSoc): {@plugChanges}", plugChanges);

        // Walk through all plug changes and check if there is at least one plug-in after a plug-out
        DateTimeOffset? lastPluggedOut = null;
        var maxPluggedOutTime = TimeSpan.FromMinutes(_constants.ManualCarMinutesUntilForgetSoc);
        var settingsCar = _settings.Cars.FirstOrDefault(c => c.Id == car.Id);
        var socSetToNull = false;
        for (var i = 0; i < plugChanges.Count; i++)
        {
            var plugChange = plugChanges[i];
            _logger.LogTrace("Handling plug change [{i}] {@plugChange}", i, plugChange);

            // Same shape as your manual-car logic:
            if (i == 0 && plugChange.BooleanValue == false)
            {
                _logger.LogTrace("Set lastPluggedOut to {timestamp} as first plugChange is pluggedOut", plugChange.Timestamp);
                lastPluggedOut = new DateTimeOffset(plugChange.Timestamp, TimeSpan.Zero);
                continue;
            }

            if (plugChange.BooleanValue == false && lastPluggedOut == default)
            {
                _logger.LogTrace("Set lastPluggedOut to {timestamp} as plugChange is pluggedOut", plugChange.Timestamp);
                lastPluggedOut = new DateTimeOffset(plugChange.Timestamp, TimeSpan.Zero);
            }
            else if (plugChange.BooleanValue == true)
            {
                if (lastPluggedOut != default)
                {
                    _logger.LogTrace("Last plugged out was not default, so checking if was longer plugged out than {maxPluggedOutTime}", maxPluggedOutTime);
                    var timeDiff = new DateTimeOffset(plugChange.Timestamp, TimeSpan.Zero) - lastPluggedOut.Value;
                    _logger.LogTrace("Actual time diff is {timeDiff}", timeDiff);
                    if (timeDiff > maxPluggedOutTime)
                    {
                        _logger.LogTrace("Time diff is too long so set soc to null");
                        settingsCar?.SoC.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), null, true);
                        socSetToNull = true;
                        break;
                    }
                }
                _logger.LogTrace("Set last plugged to null as plugChange is pluggedIn");
                lastPluggedOut = null;
            }
        }
        if (lastPluggedOut != null && settingsCar?.SoC.Value == null)
        {
            var timeDiff = _dateTimeProvider.DateTimeOffSetUtcNow() - lastPluggedOut.Value;
            _logger.LogTrace("Car has been plugged out for {timeDiff}", timeDiff);
            if (timeDiff > maxPluggedOutTime)
            {
                _logger.LogTrace("Time diff is too long so set soc to null");
                settingsCar?.SoC.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), null, true);
                socSetToNull = true;
            }
        }
        if (socSetToNull)
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
        _logger.LogTrace("chargedEnergyAtLastNonEstimatedSoc: {chargedEnergyAtLastNonEstimatedSoc}", chargedEnergyAtLastNonEstimatedSoc);
        if (chargedEnergyAtLastNonEstimatedSoc == default)
        {
            _logger.LogTrace("exiting: no chargedEnergyAtLastNonEstimatedSoc");
            return;
        }

        var latestChargedEnergy = await _context.MeterValues
            .Where(m => m.Timestamp >= lastNonEstimatedSoc.Timestamp
                        && m.CarId == car.Id
                        && m.MeterValueKind == MeterValueKind.Car)
            .OrderByDescending(m => m.Timestamp)
            .Select(m => m.EstimatedEnergyWs)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        _logger.LogTrace("latestChargedEnergy: {latestChargedEnergy}", latestChargedEnergy);
        if (latestChargedEnergy == default)
        {
            _logger.LogTrace("exiting: no latestChargedEnergy");
            return;
        }

        var chargedSinceLastNonEstimatedSoc = latestChargedEnergy.Value - chargedEnergyAtLastNonEstimatedSoc.Value;
        _logger.LogTrace("chargedSinceLastNonEstimatedSoc: {chargedSinceLastNonEstimatedSoc}", chargedSinceLastNonEstimatedSoc);
        var carBatteryCapacity = car.UsableEnergy * 3_600_000; // kWh to Ws
        _logger.LogTrace("carBatteryCapacity: {carBatteryCapacity}", carBatteryCapacity);
        if (carBatteryCapacity <= 0)
        {
            _logger.LogWarning("Can not estimate soc for car {carId} as usable energy is {usableEnergy} which is <= 0", car.Id, car.UsableEnergy);
            return;
        }
        var estimatedSoc = (int)(lastNonEstimatedSoc.IntValue.Value + (((float)chargedSinceLastNonEstimatedSoc / carBatteryCapacity) * 100));
        _logger.LogTrace("estimatedSoc: {estimatedSoc}", estimatedSoc);
        var estimatedSocCarValueLog = new CarValueLog()
        {
            CarId = car.Id,
            Timestamp = _dateTimeProvider.UtcNow(),
            Type = CarValueType.StateOfCharge,
            IntValue = estimatedSoc,
            Source = CarValueSource.Estimation,
        };
        _logger.LogTrace("adding estimatedSocCarValueLog: {@estimatedSocCarValueLog}", estimatedSocCarValueLog);
        _context.CarValueLogs.Add(estimatedSocCarValueLog);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (settingsCar == default)
        {
            _logger.LogTrace("exiting: no settingsCar found");
            return;
        }

        settingsCar.SoC.Update(_dateTimeProvider.DateTimeOffSetUtcNow(), estimatedSoc);
        _logger.LogTrace("completed successfully with estimatedSoc={estimatedSoc}", estimatedSoc);
        await _loadPointManagementService.CarStateChanged(car.Id);
    }

}
