using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class ShouldStartStopChargingCalculator : IShouldStartStopChargingCalculator
{
    private readonly ILogger<ShouldStartStopChargingCalculator> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILoadPointManagementService _loadPointManagementService;

    public ShouldStartStopChargingCalculator(ILogger<ShouldStartStopChargingCalculator> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IDateTimeProvider dateTimeProvider,
        ILoadPointManagementService loadPointManagementService)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _loadPointManagementService = loadPointManagementService;
    }

    public async Task UpdateShouldStartStopChargingTimes(int targetPower)
    {
        _logger.LogTrace("{method}({targetPower})", nameof(UpdateShouldStartStopChargingTimes), targetPower);
        var carElements = await GetCarElements().ConfigureAwait(false);
        var ocppElements = await GetOcppElements().ConfigureAwait(false);
        var orderedElements = ocppElements.Concat(carElements)
            .OrderBy(e => e.ChargingPriority)
            .ToList();
        var chargingLoadPoints = await _loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var additionalAvailablePower = targetPower - chargingLoadPoints.Select(l => l.ChargingPower).Sum();
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var carConnectorMatches =
            await _loadPointManagementService.GetCarConnectorMatches(carElements.Select(c => c.Id), ocppElements.Select(e => e.Id)).ConfigureAwait(false);
        var alreadySetChargingConnectors = new HashSet<int>();
        foreach (var element in orderedElements)
        {
            _logger.LogTrace("Set Start/Stop Charging for loadpoint: {@element}", element);
            int? carId = null;
            int? ocppConnectorId = null;
            var elementTargetPower = additionalAvailablePower;
            if (element.DeviceType == DeviceType.Car)
            {
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
            var matchingCombination = element.DeviceType switch
            {
                DeviceType.Car => carConnectorMatches.FirstOrDefault(lp => lp.CarId == element.Id),
                DeviceType.OcppConnector => carConnectorMatches.FirstOrDefault(lp => lp.ChargingConnectorId == element.Id),
                _ => throw new ArgumentException(),
            };
            if (matchingCombination != default)
            {
                if (matchingCombination.ChargingConnectorId != default)
                {
                    alreadySetChargingConnectors.Add(matchingCombination.ChargingConnectorId.Value);
                }
            }
            var matchingLoadPoint = element.DeviceType switch
            {
                DeviceType.Car => chargingLoadPoints.FirstOrDefault(lp => lp.CarId == element.Id),
                DeviceType.OcppConnector => chargingLoadPoints.FirstOrDefault(lp => lp.ChargingConnectorId == element.Id),
                _ => throw new ArgumentException(),
            };
            if (matchingLoadPoint != default)
            {
                elementTargetPower += (matchingLoadPoint.ChargingPower);
            }
            if (carId != default)
            {
                var switchOnPower = element.SwitchOnAtPower;
                var switchOffPower = element.SwitchOffAtPower;
                var car = _settings.Cars.First(c => c.Id == carId.Value);
                car.ShouldStartCharging.Update(currentDate, switchOnPower < elementTargetPower);
                car.ShouldStopCharging.Update(currentDate, switchOffPower > elementTargetPower);
            }

            if (ocppConnectorId != default)
            {
                if(_settings.OcppConnectorStates.TryGetValue(ocppConnectorId.Value, out var ocppConnectorState))
                {
                    ocppConnectorState.ShouldStartCharging.Update(currentDate, element.SwitchOnAtPower < elementTargetPower);
                    ocppConnectorState.ShouldStopCharging.Update(currentDate, element.SwitchOffAtPower > elementTargetPower);
                    var currentPhasesCharging = ocppConnectorState.IsCharging.Value ? ocppConnectorState.PhaseCount.Value ?? 0 : 0;
                    if ((element.MinPowerOnePhase != default) && (element.MaxPowerOnePhase != default) && (element.MinPowerThreePhase != default))
                    {
                        if (currentPhasesCharging == 1)
                        {
                            ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, elementTargetPower < element.MinPowerThreePhase);
                            ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower >= element.MinPowerThreePhase);
                        }
                        else if (currentPhasesCharging == 3)
                        {
                            ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, elementTargetPower <= element.MaxPowerOnePhase);
                            ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower > element.MaxPowerOnePhase);
                        }
                        else
                        {
                            ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, (element.SwitchOnAtPower <= elementTargetPower)
                                                                                            && (elementTargetPower < element.MinPowerThreePhase));
                            ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower >= element.MinPowerThreePhase);
                        }
                    }
                    else
                    {
                        ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, null);
                        ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, null);
                    }
                }
                
            }
        }
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
            if (minPhases != maxPhases)
            {
                element.MinPowerOnePhase = ocppDatabaseData.MinCurrent.Value * voltage * minPhases;
                element.MaxPowerOnePhase = ocppDatabaseData.MaxCurrent.Value * voltage * minPhases;
                element.MinPowerThreePhase = ocppDatabaseData.MinCurrent.Value * voltage * maxPhases;
            }
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
    public int? MinPowerOnePhase { get; set; }
    public int? MaxPowerOnePhase { get; set; }
    public int? MinPowerThreePhase { get; set; }
    public int CurrentPower { get; set; }
    public int MaxPower { get; set; }
    public int ChargingPriority { get; set; }
}
