using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class ShouldStartStopChargingCalculator : IShouldStartStopChargingCalculator
{
    private readonly ILogger<ShouldStartStopChargingCalculator> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ShouldStartStopChargingCalculator(ILogger<ShouldStartStopChargingCalculator> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task UpdateShouldStartStopChargingTimes(int targetPower, List<DtoLoadpoint> loadpoints)
    {
        _logger.LogTrace("{method}({targetPower})", nameof(UpdateShouldStartStopChargingTimes), targetPower);
        var ocppElements = await GetOcppElements().ConfigureAwait(false);
        var carElements = await GetCarElements().ConfigureAwait(false);
        var orderedElements = ocppElements.Concat(carElements)
            .OrderBy(e => e.ChargingPriority)
            .ToList();
        var additionalAvailablePower = targetPower - loadpoints.Select(l => l.ActualChargingPower ?? 0).Sum();
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var alreadySetCarIds = new HashSet<int>();
        var alreadySetChargingConnectors = new HashSet<int>();
        foreach (var element in orderedElements)
        {
            int? carId = null;
            int? ocppConnectorId = null;
            var elementTargetPower = additionalAvailablePower;
            if (element.DeviceType == DeviceType.Car)
            {
                if (!alreadySetCarIds.Add(element.Id))
                {
                    continue;
                }
                carId = element.Id;
            }
            if (element.DeviceType == DeviceType.OcppConnector)
            {
                if (!alreadySetChargingConnectors.Add(element.Id))
                {
                    continue;
                }
                ocppConnectorId = element.Id;
            }
            var matchingLoadpoint = element.DeviceType switch
            {
                DeviceType.Car => loadpoints.FirstOrDefault(lp => lp.Car?.Id == element.Id),
                DeviceType.OcppConnector => loadpoints.FirstOrDefault(lp => lp.OcppConnectorId == element.Id),
                _ => throw new ArgumentException(),
            };
            if (matchingLoadpoint != default)
            {
                elementTargetPower += (matchingLoadpoint.ActualChargingPower ?? 0);
                if (matchingLoadpoint.Car != default)
                {
                    alreadySetCarIds.Add(matchingLoadpoint.Car.Id);
                }
                if (matchingLoadpoint.OcppConnectorId != default)
                {
                    alreadySetChargingConnectors.Add(matchingLoadpoint.OcppConnectorId.Value);
                }
            }

            if (carId != default)
            {
                var car = _settings.Cars.First(c => c.Id == carId);
                var phases = car.ActualPhases;
                var carSettings = await _context.Cars
                    .Where(c => c.Id == car.Id)
                    .Select(c => new
                    {
                        c.MinimumAmpere,
                        c.SwitchOffAtCurrent,
                        c.SwitchOnAtCurrent,
                    })
                    .FirstAsync().ConfigureAwait(false);
                var switchOnPower = GetPowerAtPhasesAndCurrent(phases, carSettings.SwitchOnAtCurrent ?? carSettings.MinimumAmpere);
                var switchOffPower = GetPowerAtPhasesAndCurrent(phases, carSettings.SwitchOffAtCurrent ?? carSettings.MinimumAmpere);
                car.ShouldStartCharging.Update(currentDate, switchOnPower < elementTargetPower);
                car.ShouldStopCharging.Update(currentDate, switchOffPower > elementTargetPower);
                continue;
            }

            if (ocppConnectorId != default)
            {
                var ocppDatabaseData = await _context.OcppChargingStationConnectors
                    .Where(c => c.Id == ocppConnectorId)
                    .Select(c => new
                    {
                        c.MinCurrent,
                        c.MaxCurrent,
                        c.ConnectedPhasesCount,
                        c.AutoSwitchBetween1And3PhasesEnabled,
                        c.SwitchOffAtCurrent,
                        c.SwitchOnAtCurrent,
                    })
                    .FirstAsync().ConfigureAwait(false);
                if (ocppDatabaseData.ConnectedPhasesCount == default)
                {
                    _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                if (ocppDatabaseData.MinCurrent == default)
                {
                    _logger.LogError("Min current unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                if (ocppDatabaseData.MaxCurrent == default)
                {
                    _logger.LogError("Max current unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                if (ocppDatabaseData.ConnectedPhasesCount == default)
                {
                    _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                if (ocppDatabaseData.SwitchOffAtCurrent == default)
                {
                    _logger.LogError("Switch off current unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                if (ocppDatabaseData.SwitchOnAtCurrent == default)
                {
                    _logger.LogError("Switch on current unknown for connector {connectorId}", ocppConnectorId);
                    continue;
                }
                var minPhases = ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled ? 1 : ocppDatabaseData.ConnectedPhasesCount.Value;
                var shouldStartChargingPower =
                    GetPowerAtPhasesAndCurrent(minPhases, ocppDatabaseData.SwitchOnAtCurrent.Value);
                if(_settings.OcppConnectorStates.TryGetValue(ocppConnectorId.Value, out var ocppConnectorState))
                {
                    ocppConnectorState.ShouldStartCharging.Update(currentDate, shouldStartChargingPower < elementTargetPower);
                    var shouldStopChargingPower =
                        GetPowerAtPhasesAndCurrent(minPhases, ocppDatabaseData.SwitchOffAtCurrent.Value);
                    ocppConnectorState.ShouldStopCharging.Update(currentDate, shouldStopChargingPower > elementTargetPower);
                    if (ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled)
                    {
                        var minPowerThreePhase =
                            GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MinCurrent.Value);
                        ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, minPowerThreePhase < elementTargetPower);
                        var minPowerOnePhase =
                            GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MinCurrent.Value);
                        var maxPowerOnOnePhase =
                            GetPowerAtPhasesAndCurrent(ocppDatabaseData.ConnectedPhasesCount.Value, ocppDatabaseData.MaxCurrent.Value);
                        ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, (minPowerOnePhase < elementTargetPower)
                            && (maxPowerOnOnePhase > elementTargetPower));
                    }
                }
                
            }
        }
    }

    private int GetPowerAtPhasesAndCurrent(int connectedPhasesCount, decimal maxCurrent)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        return (int)(connectedPhasesCount * maxCurrent * voltage);
    }

    private async Task<List<DtoStartStopChargingHelper>> GetCarElements()
    {
        _logger.LogTrace("{method}()", nameof(GetCarElements));
        var elements = new List<DtoStartStopChargingHelper>();
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        foreach (var dtoCar in _settings.CarsToManage)
        {
            var carDatabaseValues = await _context.Cars
                .Where(c => c.Id == dtoCar.Id)
                .Select(c => new
                {
                    SwitchOnAtCurrent = c.SwitchOnAtCurrent ?? c.MinimumAmpere,
                    SwitchOffAtCurrent = c.SwitchOffAtCurrent ?? c.MinimumAmpere,
                    c.ChargingPriority,
                    c.MaximumAmpere,
                })
                .FirstAsync().ConfigureAwait(false);
            var element = new DtoStartStopChargingHelper()
            {
                Id = dtoCar.Id,
                DeviceType = DeviceType.Car,
                SwitchOnAtPower = carDatabaseValues.SwitchOnAtCurrent * voltage * dtoCar.ActualPhases,
                SwitchOffAtPower = carDatabaseValues.SwitchOffAtCurrent * voltage * dtoCar.ActualPhases,
                CurrentPower = dtoCar.ChargingPowerAtHome ?? 0,
                MaxPower = carDatabaseValues.MaximumAmpere * voltage * dtoCar.ActualPhases,
                ChargingPriority = carDatabaseValues.ChargingPriority,
            };
            elements.Add(element);
        }
        return elements;
    }

    private async Task<List<DtoStartStopChargingHelper>> GetOcppElements()
    {
        _logger.LogTrace("{method}()", nameof(GetOcppElements));
        var elements = new List<DtoStartStopChargingHelper>();
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        foreach (var ocppConnectorState in _settings.OcppConnectorStates)
        {
            var ocppDatabaseData = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == ocppConnectorState.Key)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                    c.SwitchOffAtCurrent,
                    c.SwitchOnAtCurrent,
                    c.ChargingPriority,
                })
                .FirstAsync().ConfigureAwait(false);

            #region Check values for null
            if (ocppDatabaseData.ConnectedPhasesCount == default)
            {
                _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            if (ocppDatabaseData.MinCurrent == default)
            {
                _logger.LogError("Min current unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            if (ocppDatabaseData.MaxCurrent == default)
            {
                _logger.LogError("Max current unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            if (ocppDatabaseData.ConnectedPhasesCount == default)
            {
                _logger.LogError("Connected phases unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            if (ocppDatabaseData.SwitchOffAtCurrent == default)
            {
                _logger.LogError("Switch off current unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            if (ocppDatabaseData.SwitchOnAtCurrent == default)
            {
                _logger.LogError("Switch on current unknown for connector {connectorId}", ocppConnectorState.Key);
                continue;
            }
            #endregion

            var minPhases = ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled ? 1 : ocppDatabaseData.ConnectedPhasesCount.Value;
            var maxPhases = ocppDatabaseData.ConnectedPhasesCount.Value;
            //If a car with less phases than Charger phases is connected, use this amount of phases
            if (ocppConnectorState.Value.PhaseCount.Value < minPhases)
            {
                minPhases = ocppConnectorState.Value.PhaseCount.Value.Value;
                //charging on less phases than last set phases means car does not support that many phases
                if ((ocppConnectorState.Value.PhaseCount.Value < ocppConnectorState.Value.LastSetPhases.Value)
                    //If Wallbox does not support phase switch and actual phases are less than charger is connected to, this means the car does not support that many phases
                    || ((!ocppDatabaseData.AutoSwitchBetween1And3PhasesEnabled) && (ocppConnectorState.Value.PhaseCount.Value < ocppDatabaseData.ConnectedPhasesCount)))
                {
                    maxPhases = ocppConnectorState.Value.PhaseCount.Value.Value;
                }
            }

            var element = new DtoStartStopChargingHelper()
            {
                Id = ocppConnectorState.Key,
                DeviceType = DeviceType.OcppConnector,
                SwitchOnAtPower = ocppDatabaseData.SwitchOnAtCurrent.Value * voltage * minPhases,
                SwitchOffAtPower = ocppDatabaseData.SwitchOffAtCurrent.Value * voltage * minPhases,
                CurrentPower = ocppConnectorState.Value.ChargingPower.Value,
                MaxPower = ocppDatabaseData.MaxCurrent.Value * voltage * maxPhases,
                ChargingPriority = ocppDatabaseData.ChargingPriority,
            };
            elements.Add(element);
        }

        return elements;
    }
}

public class DtoStartStopChargingHelper
{
    public int Id { get; set; }
    public DeviceType DeviceType { get; set; }
    public int SwitchOnAtPower { get; set; }
    public int SwitchOffAtPower { get; set; }
    public int CurrentPower { get; set; }
    public int MaxPower { get; set; }
    public int ChargingPriority { get; set; }
}

public enum DeviceType
{
    OcppConnector,
    Car,
}
