using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TargetChargingValueCalculationService : ITargetChargingValueCalculationService
{
    private readonly ILogger<TargetChargingValueCalculationService> _logger;
    private readonly ITeslaSolarChargerContext _context;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IConstants _constants;
    private readonly INotChargingWithExpectedPowerReasonHelper _notChargingWithExpectedPowerReasonHelper;

    public TargetChargingValueCalculationService(ILogger<TargetChargingValueCalculationService> logger,
        ITeslaSolarChargerContext context,
        ISettings settings,
        IConfigurationWrapper configurationWrapper,
        IConstants constants,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
    {
        _logger = logger;
        _context = context;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _constants = constants;
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
    }

    public async Task AppendTargetValues(List<DtoTargetChargingValues> targetChargingValues,
        List<DtoChargingSchedule> activeChargingSchedules, DateTimeOffset currentDate, int powerToControl,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({@targetChargingValues}, {@activeChargingSchedules}, {currentDate})", nameof(AppendTargetValues), targetChargingValues, activeChargingSchedules, currentDate);
        var maxCombinedCurrent = (decimal)_configurationWrapper.MaxCombinedCurrent();
        foreach (var loadPoint in targetChargingValues
                     .Where(t => activeChargingSchedules.Any(c => c.CarId == t.LoadPoint.CarId && c.OccpChargingConnectorId == t.LoadPoint.ChargingConnectorId && c.OnlyChargeOnAtLeastSolarPower == default)))
        {
            var chargingSchedule = activeChargingSchedules.First(c => c.CarId == loadPoint.LoadPoint.CarId && c.OccpChargingConnectorId == loadPoint.LoadPoint.ChargingConnectorId && c.OnlyChargeOnAtLeastSolarPower == default);
            var constraintValues = await GetConstraintValues(loadPoint.LoadPoint.CarId,
                loadPoint.LoadPoint.ChargingConnectorId, loadPoint.LoadPoint.ManageChargingPowerByCar, currentDate, maxCombinedCurrent,
                cancellationToken).ConfigureAwait(false);
            loadPoint.TargetValues = GetTargetValue(constraintValues, loadPoint.LoadPoint, chargingSchedule.ChargingPower, true, currentDate);
            maxCombinedCurrent -= CalculateEstimatedCurrentUsage(loadPoint, constraintValues);
        }

        var ascending = powerToControl > 0;
        foreach (var loadPoint in (ascending
                     ? targetChargingValues.Where(t => t.TargetValues == default).OrderBy(x => x.LoadPoint.ChargingPriority)
                     : targetChargingValues.Where(t => t.TargetValues == default).OrderByDescending(x => x.LoadPoint.ChargingPriority)))
        {
            var constraintValues = await GetConstraintValues(loadPoint.LoadPoint.CarId,
                loadPoint.LoadPoint.ChargingConnectorId, loadPoint.LoadPoint.ManageChargingPowerByCar, currentDate, maxCombinedCurrent,
                cancellationToken).ConfigureAwait(false);
            loadPoint.TargetValues = GetTargetValue(constraintValues, loadPoint.LoadPoint, powerToControl, false, currentDate);
            maxCombinedCurrent -= CalculateEstimatedCurrentUsage(loadPoint, constraintValues);
        }
    }

    private decimal CalculateEstimatedCurrentUsage(DtoTargetChargingValues loadPoint, ConstraintValues constraintValues)
    {
        _logger.LogTrace("{method}({@loadPoint}, {@constraintValues})", nameof(CalculateEstimatedCurrentUsage), loadPoint, constraintValues);
        if (loadPoint.TargetValues == default)
        {
            return 0;
        }
        if (loadPoint.TargetValues.StopCharging)
        {
            return 0;
        }
        var actualCurrent = loadPoint.LoadPoint.ActualCurrent ?? 0m;
        var lastSetCurrent = 0m;
        if (loadPoint.LoadPoint.ManageChargingPowerByCar && loadPoint.LoadPoint.CarId != default)
        {
            var car = _settings.Cars.First(c => c.Id == loadPoint.LoadPoint.CarId.Value);
            lastSetCurrent = car.LastSetAmp.Value;
        }
        else if (loadPoint.LoadPoint.ChargingConnectorId != default)
        {
            var ocppValues = _settings.OcppConnectorStates.GetValueOrDefault(loadPoint.LoadPoint.ChargingConnectorId.Value);
            lastSetCurrent = ocppValues?.LastSetCurrent.Value ?? 0m;
        }
        var notUsedCurrent = lastSetCurrent - actualCurrent;
        var currentToSet = loadPoint.TargetValues.TargetCurrent ?? actualCurrent;
        //If not all current was used on last current set, we estimate that the same amount of current will not be used again
        return currentToSet - notUsedCurrent;
    }

    private TargetValues? GetTargetValue(ConstraintValues constraintValues, DtoLoadPointOverview loadpoint, int powerToSet, bool ignoreTimers, DateTimeOffset currentDate)
    {
        _logger.LogTrace("{method}({@constraintValues}, {@loadpoint}, {powerToSet}, {ignoreTimers}, {currentDate})", nameof(GetTargetValue), constraintValues, loadpoint, powerToSet, ignoreTimers, currentDate);
        if (loadpoint.IsPluggedIn != true || loadpoint.IsHome == false)
        {
            _logger.LogTrace("Loadpoint is not plugged in or not home, returning null.");
            return null;
        }

        if (constraintValues.MaxCurrent < constraintValues.MinCurrent)
        {
            _logger.LogWarning("Max current {maxCurrent} is lower than min current {minCurrent} for loadpoint {@loadpoint}. Very likely due to low configured \"Max combined charging current\" in Base Configuration.", constraintValues.MaxCurrent, constraintValues.MinCurrent, loadpoint);
            _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId, new("Charging stopped because of not enough max combined current."));
            return constraintValues.IsCharging == true ? new TargetValues() { StopCharging = true, } : null;
        }
        if (constraintValues.ChargeMode == ChargeModeV2.Off)
        {
            return constraintValues.IsCharging == true ? new TargetValues() { StopCharging = true, } : null;
        }
        if (constraintValues.ChargeMode == ChargeModeV2.Manual)
        {
            return null;
        }
        if (constraintValues.RequiresChargeStartDueToCarFullyChargedSinceLastCurrentSet == true)
        {
            var phasesToSet = constraintValues.PhaseSwitchingEnabled == true ? constraintValues.MaxPhases : null;
            var currentToSet = constraintValues.MaxCurrent;
            return new TargetValues()
            {
                StartCharging = true,
                TargetCurrent = currentToSet,
                TargetPhases = phasesToSet,
            };
        }
        if (constraintValues.ChargeMode == ChargeModeV2.Auto)
        {
            if ((!ignoreTimers) && (constraintValues.ChargeStopAllowed == true))
            {
                return constraintValues.IsCharging == true ? new TargetValues() { StopCharging = true, } : null;
            }

            var ocppValues = loadpoint.ChargingConnectorId != default
                ? _settings.OcppConnectorStates.GetValueOrDefault(loadpoint.ChargingConnectorId.Value)
                : null;
            var car = loadpoint.CarId != default ? _settings.Cars.FirstOrDefault(c => c.Id == loadpoint.CarId.Value) : null;

            if (constraintValues.MinPhases == default || constraintValues.MaxPhases == default)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId, new("Min Phases or Max Phases is unkown. Check the logs for further details."));
                _logger.LogWarning("Can not handle loadpoint {@loadpoint} as minphases or maxphases is not known", loadpoint);
                return null;
            }
            if (loadpoint.EstimatedVoltageWhileCharging == default)
            {
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId, new("Estimated voltage while charging is unkown. Check the logs for further details."));
                _logger.LogWarning("Can not handle loadpoint {@loadpoint} as estimated voltage while charging is not known", loadpoint);
                return null;
            }
            var lastSetPhases = ocppValues?.LastSetPhases.Value;
            var phasesToUse = loadpoint.ActualPhases ?? lastSetPhases ?? constraintValues.MaxPhases.Value;
            var currentToSet = powerToSet * (1m / (loadpoint.EstimatedVoltageWhileCharging.Value * phasesToUse));
            // should reduce phases
            if ((currentToSet < constraintValues.MinCurrent)
                 && (phasesToUse != constraintValues.MinPhases.Value)
                 && ((constraintValues.PhaseReductionAllowed == true)
                     || ignoreTimers))
            {
                if (constraintValues.IsCharging == true)
                {
                    _logger.LogTrace("Stopping charging to allow phase reduction for loadpoint {@loadpoint}", loadpoint);
                    return new() { StopCharging = true, };
                }
                if (constraintValues.LastIsChargingChange > (currentDate - constraintValues.PhaseSwitchCoolDownTime))
                {
                    _logger.LogTrace("Waiting cooldone time of {coolDownTime} before starting to charge", loadpoint);
                    return null;
                }
                phasesToUse = 1;
            }
            // should increase phases
            else if ((currentToSet > constraintValues.MaxCurrent)
                       && (phasesToUse != constraintValues.MaxPhases.Value)
                        && ((constraintValues.PhaseIncreaseAllowed == true)
                           || ignoreTimers))
            {
                if (constraintValues.IsCharging == true)
                {
                    _logger.LogTrace("Stopping charging to allow phase increase for loadpoint {@loadpoint}", loadpoint);
                    return new() { StopCharging = true, };
                }
                if (constraintValues.LastIsChargingChange > (currentDate - constraintValues.PhaseSwitchCoolDownTime))
                {
                    _logger.LogTrace("Waiting cooldone time of {coolDownTime} before starting to charge", loadpoint);
                    return null;
                }
                phasesToUse = 3;
            }
            //recalculate current to set based on phases to use
            currentToSet = powerToSet * (1m / (loadpoint.EstimatedVoltageWhileCharging.Value * phasesToUse));

            if (currentToSet < constraintValues.MinCurrent)
            {
                _logger.LogTrace("Increase current to set from {oldCurrentToSet} as is below min current of {newCurrentToSet}", currentToSet, constraintValues.MinCurrent);
                currentToSet = constraintValues.MinCurrent.Value;
            }
            else if (currentToSet > constraintValues.MaxCurrent)
            {
                _logger.LogTrace("Decrease current to set from {oldCurrentToSet} as is above max current of {newCurrentToSet}.", currentToSet, constraintValues.MaxCurrent);
                currentToSet = constraintValues.MaxCurrent.Value;
            }

            if (constraintValues.IsCharging != true)
            {
                if ((constraintValues.ChargeStartAllowed != true) && (!ignoreTimers))
                {
                    return null;
                }
                if (constraintValues.Soc >= constraintValues.MaxSoc)
                {
                    return null;
                }
                if (constraintValues.CarSocLimit <= (constraintValues.Soc - _constants.MinimumSocDifference))
                {
                    return null;
                }
                return new()
                {
                    StartCharging = true,
                    TargetCurrent = currentToSet,
                    TargetPhases = constraintValues.PhaseSwitchingEnabled == true ? phasesToUse : null,
                };
            }
            return new()
            {
                TargetCurrent = currentToSet,
                TargetPhases = constraintValues.PhaseSwitchingEnabled == true ? phasesToUse : null,
            };

        }
        if (constraintValues.ChargeMode == ChargeModeV2.MaxPower)
        {
            if ((constraintValues.IsCharging == false)
                && (constraintValues.CarSocLimit <= (constraintValues.Soc + _constants.MinimumSocDifference)))
            {
                return null;
            }
            //stop charging to allow phase switching
            if (constraintValues.IsCharging == true
                && loadpoint.ChargingConnectorId != default
                && constraintValues.PhaseSwitchingEnabled == true
                && loadpoint.ActualPhases != constraintValues.MaxPhases
                && constraintValues.PhaseSwitchingEnabled == true
                && !loadpoint.ManageChargingPowerByCar)
            {
                var ocppValues = _settings.OcppConnectorStates.GetValueOrDefault(loadpoint.ChargingConnectorId.Value);
                if (ocppValues != default && ocppValues.LastSetPhases.Value != constraintValues.MaxPhases)
                {
                    return new TargetValues() { StopCharging = true };
                }
            }
            return new TargetValues()
            {
                StartCharging = constraintValues.IsCharging != true,
                TargetCurrent = constraintValues.MaxCurrent,
                TargetPhases = constraintValues.MaxPhases,
            };
        }
        return null;
    }

    private async Task<ConstraintValues> GetConstraintValues(int? carId, int? connectorId, bool useCarToManageChargingSpeed,
        DateTimeOffset currentDate, decimal maxCombinedCurrent, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({carId}, {connectorId})", nameof(GetConstraintValues), carId, connectorId);
        var timeSpanUntilSwitchOn = _configurationWrapper.TimespanUntilSwitchOn();
        var timeSpanUntilSwitchOff = _configurationWrapper.TimespanUntilSwitchOff();
        var constraintValues = new ConstraintValues();
        if (carId != default)
        {
            var carConfigValues = await _context.Cars
                .Where(c => c.Id == carId.Value)
                .Select(c => new
                {
                    c.MinimumAmpere,
                    c.MaximumAmpere,
                    c.ChargeMode,
                    c.MaximumSoc,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            constraintValues.MinCurrent = carConfigValues.MinimumAmpere;
            constraintValues.MaxCurrent = carConfigValues.MaximumAmpere;
            constraintValues.ChargeMode = carConfigValues.ChargeMode;
            var car = _settings.Cars.First(c => c.Id == carId);
            constraintValues.MinPhases = car.ActualPhases;
            constraintValues.MaxPhases = car.ActualPhases;
            constraintValues.MaxSoc = carConfigValues.MaximumSoc;
            constraintValues.CarSocLimit = car.SocLimit;
            constraintValues.Soc = car.SoC;
            if (useCarToManageChargingSpeed)
            {
                constraintValues.ChargeStartAllowed = (car.ShouldStartCharging.Value == true)
                                                      && IsTimeStampedValueRelevant(car.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn, out _);
                constraintValues.ChargeStopAllowed = (car.ShouldStopCharging.Value == true)
                                                     && IsTimeStampedValueRelevant(car.ShouldStopCharging, currentDate, timeSpanUntilSwitchOff, out _);
                constraintValues.IsCharging = car.State == CarStateEnum.Charging;
            }
        }

        // ReSharper disable once InvertIf
        if (connectorId != default)
        {
            var chargingConnectorConfigValues = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == connectorId.Value)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                    c.AutoSwitchBetween1And3PhasesEnabled,
                    c.ChargeMode,
                    c.PhaseSwitchCoolDownTimeSeconds,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (constraintValues.MinCurrent == default)
            {
                constraintValues.MinCurrent = chargingConnectorConfigValues.MinCurrent;
            }
            else if ((!useCarToManageChargingSpeed) && (chargingConnectorConfigValues.MinCurrent > constraintValues.MinCurrent))
            {
                constraintValues.MinCurrent = chargingConnectorConfigValues.MinCurrent;
            }

            if (constraintValues.MaxCurrent == default || chargingConnectorConfigValues.MaxCurrent < constraintValues.MaxCurrent)
            {
                constraintValues.MaxCurrent = chargingConnectorConfigValues.MaxCurrent;
            }

            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (constraintValues.MinPhases == default)
            {
                constraintValues.MinPhases = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled
                    ? 1
                    : chargingConnectorConfigValues.ConnectedPhasesCount;
            }

            constraintValues.PhaseSwitchingEnabled = chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled;

            // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
            if (constraintValues.MaxPhases == default)
            {
                constraintValues.MaxPhases = chargingConnectorConfigValues.ConnectedPhasesCount;
            }
            if (constraintValues.ChargeMode == default)
            {
                constraintValues.ChargeMode = chargingConnectorConfigValues.ChargeMode;
            }
            constraintValues.PhaseSwitchCoolDownTime = chargingConnectorConfigValues.PhaseSwitchCoolDownTimeSeconds == default
                ? null
                : TimeSpan.FromSeconds(chargingConnectorConfigValues.PhaseSwitchCoolDownTimeSeconds.Value);
            var ocppValues = _settings.OcppConnectorStates.GetValueOrDefault(connectorId.Value);
            constraintValues.IsCarFullyCharged = ocppValues?.IsCarFullyCharged.Value;
            constraintValues.LastIsChargingChange = ocppValues?.IsCharging.LastChanged;
            if ((constraintValues.IsCarFullyCharged == true) && (!useCarToManageChargingSpeed))
            {
                constraintValues.RequiresChargeStartDueToCarFullyChargedSinceLastCurrentSet = ocppValues?.IsCarFullyCharged.LastChanged > ocppValues?.LastSetCurrent.Timestamp;
            }
            if ((ocppValues != default) && (!useCarToManageChargingSpeed))
            {
                constraintValues.IsCharging = ocppValues.IsCharging.Value;
                constraintValues.ChargeStartAllowed = (ocppValues.ShouldStartCharging.Value == true)
                                                      && IsTimeStampedValueRelevant(ocppValues.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn, out _);
                constraintValues.ChargeStopAllowed = (ocppValues.ShouldStopCharging.Value == true)
                                                     && IsTimeStampedValueRelevant(ocppValues.ShouldStopCharging, currentDate, timeSpanUntilSwitchOff, out _);
                if (chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled)
                {
                    constraintValues.PhaseReductionAllowed = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOff, true, out _)
                                                             && IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOff, false, out _);
                    constraintValues.PhaseIncreaseAllowed = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOff, false, out _)
                                                             && IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOff, true, out _);
                }
                else
                {
                    constraintValues.PhaseReductionAllowed = false;
                    constraintValues.PhaseIncreaseAllowed = false;
                }
            }

        }

        if (useCarToManageChargingSpeed)
        {
            constraintValues.PhaseReductionAllowed = false;
            constraintValues.PhaseIncreaseAllowed = false;
        }

        if (constraintValues.MaxCurrent > maxCombinedCurrent)
        {
            constraintValues.MaxCurrent = (int)maxCombinedCurrent;
        }
        return constraintValues;
    }

    private bool IsTimeStampedValueRelevantAndFullFilled<T>(DtoTimeStampedValue<T> timeStampedValue, DateTimeOffset currentDate,
        TimeSpan timeSpanUntilIsRelevant, T comparator, out DateTimeOffset? relevantAt)
    {
        _logger.LogTrace("{method}({@timeStampedValue}, {currentDate}, {timeSpanUntilIsRelevant}, {comparator})", nameof(IsTimeStampedValueRelevantAndFullFilled), timeStampedValue, currentDate, timeSpanUntilIsRelevant, comparator);
        var isValueRelevant = IsTimeStampedValueRelevant(timeStampedValue, currentDate, timeSpanUntilIsRelevant, out relevantAt);
        return isValueRelevant && EqualityComparer<T>.Default.Equals(timeStampedValue.Value, comparator);
    }

    private bool IsTimeStampedValueRelevant<T>(DtoTimeStampedValue<T> timeStampedValue, DateTimeOffset currentDate,
        TimeSpan timeSpanUntilIsRelevant, out DateTimeOffset? relevantAt)
    {
        relevantAt = null;
        if (timeStampedValue.LastChanged == default)
        {
            return true; // If no last changed time is set, we assume it is relevant as it might never change when the value is true since startup
        }
        var isRelevant = timeStampedValue.LastChanged < (currentDate - timeSpanUntilIsRelevant);
        if (!isRelevant)
        {
            relevantAt = timeStampedValue.LastChanged.Value.Add(timeSpanUntilIsRelevant);
        }
        return isRelevant;
    }
}
