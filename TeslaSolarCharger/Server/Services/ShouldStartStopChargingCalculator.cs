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
        var chargingLoadPoints = await _loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var additionalAvailablePower = targetPower - chargingLoadPoints.Select(l => l.ChargingPower).Sum();
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var loadPointsToManage = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        foreach (var dtoLoadPointOverview in loadPointsToManage)
        {
            _logger.LogTrace("Set Start/Stop Charging for loadpoint: {@dtoLoadPointOverview}", dtoLoadPointOverview);
            var carId = dtoLoadPointOverview.CarId;
            var ocppConnectorId = dtoLoadPointOverview.ChargingConnectorId;
            var elementTargetPower = additionalAvailablePower;
            var matchingLoadPoint = chargingLoadPoints
                .FirstOrDefault(lp => lp.CarId == carId
                                      && lp.ChargingConnectorId == ocppConnectorId);
            if (matchingLoadPoint != default)
            {
                elementTargetPower += (matchingLoadPoint.ChargingPower);
            }
            var carElement = carId == default ? null : carElements.FirstOrDefault(c => c.Id == carId.Value);
            var ocppElement = ocppConnectorId == default ? null : ocppElements.FirstOrDefault(c => c.Id == ocppConnectorId.Value);
            var minPhaseCount = GetMinPhases(carElement, ocppElement, dtoLoadPointOverview.CarType);
            if ((minPhaseCount == default) || (minPhaseCount < 1))
            {
                _logger.LogError("Min phases unknown for car {carId} and connector {connectorId}", carId, ocppConnectorId);
                continue;
            }
            _logger.LogTrace("Min phase count for car {carId} and connector {connectorId}: {value}", carId, ocppConnectorId, minPhaseCount);
            var switchOnCurrent = GetSwitchOnCurrent(carElement, ocppElement);
            if (switchOnCurrent == default)
            {
                _logger.LogError("switchOnCurrent unknown for car {carId} and connector {connectorId}", carId, ocppConnectorId);
                continue;
            }
            _logger.LogTrace("Switch on current for car {carId} and connector {connectorId}: {value}", carId, ocppConnectorId, switchOnCurrent);
            var switchOffCurrent = GetSwitchOffCurrent(carElement, ocppElement);
            if (switchOffCurrent == default)
            {
                _logger.LogError("switchOffCurrent unknown for car {carId} and connector {connectorId}", carId, ocppConnectorId);
                continue;
            }
            _logger.LogTrace("Switch off current for car {carId} and connector {connectorId}: {value}", carId, ocppConnectorId, switchOffCurrent);
            var voltage = _settings.AverageHomeGridVoltage ?? 230;
            var switchOnAtPower = switchOnCurrent.Value * minPhaseCount.Value * voltage;
            var switchOffAtPower = switchOffCurrent.Value * minPhaseCount.Value * voltage;
            if (carId != default)
            {
                var car = _settings.Cars.First(c => c.Id == carId.Value);
                car.ShouldStartCharging.Update(currentDate, switchOnAtPower < elementTargetPower);
                car.ShouldStopCharging.Update(currentDate, switchOffAtPower > elementTargetPower);
            }

            if (ocppConnectorId != default
                && ocppElement != default
                && _settings.OcppConnectorStates.TryGetValue(ocppConnectorId.Value, out var ocppConnectorState))
            {
                ocppConnectorState.ShouldStartCharging.Update(currentDate, switchOnAtPower < elementTargetPower);
                ocppConnectorState.ShouldStopCharging.Update(currentDate, switchOffAtPower > elementTargetPower);
                var currentPhasesCharging = ocppConnectorState.IsCharging.Value ? ocppConnectorState.PhaseCount.Value ?? 0 : 0;
                if ((ocppElement.MinPowerOnePhase != default) && (ocppElement.MaxPowerOnePhase != default) && (ocppElement.MinPowerThreePhase != default))
                {
                    if (currentPhasesCharging == 1)
                    {
                        ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, elementTargetPower < ocppElement.MinPowerThreePhase);
                        ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower >= ocppElement.MinPowerThreePhase);
                    }
                    else if (currentPhasesCharging == 3)
                    {
                        ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, elementTargetPower <= ocppElement.MaxPowerOnePhase);
                        ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower > ocppElement.MaxPowerOnePhase);
                    }
                    else
                    {
                        ocppConnectorState.CanHandlePowerOnOnePhase.Update(currentDate, (switchOnAtPower <= elementTargetPower)
                                                                                        && (elementTargetPower < ocppElement.MinPowerThreePhase));
                        ocppConnectorState.CanHandlePowerOnThreePhase.Update(currentDate, elementTargetPower >= ocppElement.MinPowerThreePhase);
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

    private int? GetSwitchOffCurrent(DtoStartStopChargingHelper? carElement, DtoStartStopChargingHelper? ocppElement)
    {
        var value = new[]
            {
                carElement?.SwitchOffAt.Current,
                ocppElement?.SwitchOffAt.Current,
            }
            .Where(p => p.HasValue)
            .DefaultIfEmpty()
            .Max();
        return value;
    }

    private int? GetSwitchOnCurrent(DtoStartStopChargingHelper? carElement, DtoStartStopChargingHelper? ocppElement)
    {
        var value = new[]
            {
                carElement?.SwitchOnAt.Current,
                ocppElement?.SwitchOnAt.Current,
            }
            .Where(p => p.HasValue)
            .DefaultIfEmpty()
            .Max();
        return value;
    }

    private int? GetMinPhases(DtoStartStopChargingHelper? carElement, DtoStartStopChargingHelper? ocppElement, CarType? carType)
    {
        if (carType == CarType.Tesla)
        {
            return carElement?.SwitchOnAt.PhaseCount;
        }
        var minPhaseCount = new[]
            {
                carElement?.SwitchOnAt.PhaseCount,
                ocppElement?.SwitchOnAt.PhaseCount,
            }
            .Where(p => p.HasValue)
            .DefaultIfEmpty()
            .Min();
        return minPhaseCount;
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
                    c.CarType,
                    c.MaximumPhases,
                })
                .FirstAsync().ConfigureAwait(false);
            var phases = carDatabaseValues.CarType == CarType.Tesla ? dtoCar.ActualPhases : carDatabaseValues.MaximumPhases;
            var switchOnPoint = new SwitchPoint()
            {
                Current = carDatabaseValues.SwitchOnAtCurrent,
                PhaseCount = phases,
                Voltage = voltage,
            };
            var switchOffPoint = new SwitchPoint()
            {
                Current = carDatabaseValues.SwitchOffAtCurrent,
                PhaseCount = phases,
                Voltage = voltage,
            };
            var element = new DtoStartStopChargingHelper(switchOnPoint, switchOffPoint)
            {
                Id = dtoCar.Id,
                DeviceType = DeviceType.Car,
                CurrentPower = dtoCar.ChargingPowerAtHome ?? 0,
                MaxPower = carDatabaseValues.MaximumAmpere * voltage * phases,
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
            var maxCurrent = ocppDatabaseData.MaxCurrent.Value;
            var minCurrent = ocppDatabaseData.MinCurrent.Value;
            if (maxCurrent < minCurrent)
            {
                minCurrent = maxCurrent;
            }

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

            var switchOnPoint = new SwitchPoint()
            {
                Current = ocppDatabaseData.SwitchOnAtCurrent.Value,
                PhaseCount = minPhases,
                Voltage = voltage,
            };
            var switchOffPoint = new SwitchPoint()
            {
                Current = ocppDatabaseData.SwitchOffAtCurrent.Value,
                PhaseCount = minPhases,
                Voltage = voltage,
            };
            var element = new DtoStartStopChargingHelper(switchOnPoint, switchOffPoint)
            {
                Id = ocppConnectorState.Key,
                DeviceType = DeviceType.OcppConnector,
                CurrentPower = ocppConnectorState.Value.ChargingPower.Value,
                MaxPower = maxCurrent * voltage * maxPhases,
                ChargingPriority = ocppDatabaseData.ChargingPriority,
            };
            if (minPhases != maxPhases)
            {
                element.MinPowerOnePhase = minCurrent * voltage * minPhases;
                element.MaxPowerOnePhase = maxCurrent * voltage * minPhases;
                element.MinPowerThreePhase = minCurrent * voltage * maxPhases;
            }
            elements.Add(element);
        }

        return elements;
    }
}

public class DtoStartStopChargingHelper
{
    public DtoStartStopChargingHelper(SwitchPoint switchOnAt, SwitchPoint switchOffAt)
    {
        SwitchOnAt = switchOnAt;
        SwitchOffAt = switchOffAt;
    }

    public int Id { get; set; }
    public DeviceType DeviceType { get; set; }
    public SwitchPoint SwitchOnAt { get; set; }
    public SwitchPoint SwitchOffAt { get; set; }
    public int? MinPowerOnePhase { get; set; }
    public int? MaxPowerOnePhase { get; set; }
    public int? MinPowerThreePhase { get; set; }
    public int CurrentPower { get; set; }
    public int MaxPower { get; set; }
    public int ChargingPriority { get; set; }
}

public class SwitchPoint
{
    public int Current { get; set; }
    public int PhaseCount { get; set; }
    public int Voltage { get; set; }
    public int Power => Current * PhaseCount * Voltage;
}
