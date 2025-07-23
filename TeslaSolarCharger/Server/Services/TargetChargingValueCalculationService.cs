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
                     .Where(t => activeChargingSchedules.Any(c => c.CarId == t.LoadPoint.CarId && c.OccpChargingConnectorId == t.LoadPoint.ChargingConnectorId && c.OnlyChargeOnAtLeastSolarPower == default))
                     .OrderBy(x => x.LoadPoint.ChargingPriority))
        {
            var chargingSchedule = activeChargingSchedules.First(c => c.CarId == loadPoint.LoadPoint.CarId && c.OccpChargingConnectorId == loadPoint.LoadPoint.ChargingConnectorId && c.OnlyChargeOnAtLeastSolarPower == default);
            var constraintValues = await GetConstraintValues(loadPoint.LoadPoint.CarId,
                loadPoint.LoadPoint.ChargingConnectorId, loadPoint.LoadPoint.ManageChargingPowerByCar, currentDate, maxCombinedCurrent,
                cancellationToken).ConfigureAwait(false);
            var targetPower = chargingSchedule.ChargingPower > powerToControl
                ? chargingSchedule.ChargingPower
                : powerToControl;
            loadPoint.TargetValues = GetTargetValue(constraintValues, loadPoint.LoadPoint, targetPower, true, currentDate);
            var estimatedCurrentUsage = CalculateEstimatedCurrentUsage(loadPoint, constraintValues);
            maxCombinedCurrent -= estimatedCurrentUsage;
            powerToControl -= CalculateEstimatedPowerUsage(loadPoint, estimatedCurrentUsage);
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
            var estimatedCurrentUsage = CalculateEstimatedCurrentUsage(loadPoint, constraintValues);
            maxCombinedCurrent -= estimatedCurrentUsage;
            powerToControl -= CalculateEstimatedPowerUsage(loadPoint, estimatedCurrentUsage);
        }
    }

    private int CalculateEstimatedPowerUsage(DtoTargetChargingValues loadPointTargetValues, decimal estimatedCurrentUsage)
    {
        _logger.LogTrace("{method}({@loadPointTargetValues}, {estimatedCurrentUsage})", nameof(CalculateEstimatedPowerUsage), loadPointTargetValues, estimatedCurrentUsage);
        var voltage = loadPointTargetValues.LoadPoint.EstimatedVoltageWhileCharging ?? _settings.AverageHomeGridVoltage ?? 230m;
        var phases = loadPointTargetValues.LoadPoint.ActualPhases ?? loadPointTargetValues.TargetValues?.TargetPhases ?? 3;
        var estimatedPower = estimatedCurrentUsage * voltage * phases;
        _logger.LogTrace("Estimated power: {estimatedPower})", estimatedPower);
        return (int)estimatedPower;
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
        if (notUsedCurrent > 1)
        {
            return actualCurrent;
        }
        var currentToSet = loadPoint.TargetValues.TargetCurrent ?? actualCurrent;
        return currentToSet;
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
        if ((constraintValues.ChargeMode == ChargeModeV2.Off)
            || (constraintValues.Soc > constraintValues.MaxSoc))
        {
            return constraintValues.IsCharging == true ? new TargetValues() { StopCharging = true, } : null;
        }
        if (constraintValues.ChargeMode == ChargeModeV2.Manual)
        {
            return null;
        }
        if (constraintValues.IsCarFullyCharged == true)
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
        if ((constraintValues.ChargeMode == ChargeModeV2.Auto)
            && (!(constraintValues.MinSoc > constraintValues.Soc)))
        {
            if ((!ignoreTimers) && (constraintValues.ChargeStopAllowed == true))
            {
                return constraintValues.IsCharging == true ? new TargetValues() { StopCharging = true, } : null;
            }

            var ocppValues = loadpoint.ChargingConnectorId != default
                ? _settings.OcppConnectorStates.GetValueOrDefault(loadpoint.ChargingConnectorId.Value)
                : null;
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
            // should reduce phases and is allowed
            if ((currentToSet < constraintValues.MinCurrent)
                 && (phasesToUse != constraintValues.MinPhases.Value)
                 && ((constraintValues.PhaseReductionAllowed == true)
                     || ignoreTimers))
            {
                if (constraintValues.IsCharging == true)
                {
                    _logger.LogTrace("Stopping charging to allow phase reduction for loadpoint {@loadpoint}", loadpoint);
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new("Waiting phase switch cooldown time before starting to charge", currentDate + constraintValues.PhaseSwitchCoolDownTime + _configurationWrapper.ChargingValueJobUpdateIntervall()));
                    return new() { StopCharging = true, };
                }
                phasesToUse = 1;
            }
            // should reduce phases but is not allowed
            else if ((currentToSet < constraintValues.MinCurrent)
                     && (phasesToUse != constraintValues.MinPhases.Value)
                     && (constraintValues.PhaseReductionAllowed != true)
                     && (!ignoreTimers)
                     && (constraintValues.IsCharging == true)
                     && (constraintValues.ChargeStopAllowedAt == default))
            {
                _logger.LogTrace("Loadpoint {@loadpoint} is not charging with expected power as it should reduce phases but is not allowed to do so.", loadpoint);
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                    new("Waiting for phase reduction", constraintValues.PhaseReductionAllowedAt + _configurationWrapper.ChargingValueJobUpdateIntervall()));
            }
            // should increase phases and is allowed
            else if ((currentToSet > constraintValues.MaxCurrent)
                       && (phasesToUse != constraintValues.MaxPhases.Value)
                        && ((constraintValues.PhaseIncreaseAllowed == true)
                           || ignoreTimers))
            {
                if (constraintValues.IsCharging == true)
                {
                    _logger.LogTrace("Stopping charging to allow phase increase for loadpoint {@loadpoint}", loadpoint);
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new("Waiting phase switch cooldown time before starting to charge", currentDate + constraintValues.PhaseSwitchCoolDownTime + _configurationWrapper.ChargingValueJobUpdateIntervall()));
                    return new() { StopCharging = true, };
                }
                phasesToUse = 3;
            }
            else if ((currentToSet > constraintValues.MaxCurrent)
                     && (phasesToUse != constraintValues.MaxPhases.Value)
                     && (constraintValues.PhaseIncreaseAllowed != true)
                     && (!ignoreTimers)
                     && (constraintValues.IsCharging == true))
            {
                _logger.LogTrace("Loadpoint {@loadpoint} is not charging with expected power as it should increase phases but is not allowed to do so.", loadpoint);
                _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                    new("Waiting for phase increase", constraintValues.PhaseIncreaseAllowedAt + _configurationWrapper.ChargingValueJobUpdateIntervall()));
            }
            //recalculate current to set based on phases to use
            currentToSet = powerToSet * (1m / (loadpoint.EstimatedVoltageWhileCharging.Value * phasesToUse));

            if (currentToSet < constraintValues.MinCurrent)
            {
                _logger.LogTrace("Increase current to set from {oldCurrentToSet} as is below min current of {newCurrentToSet}", currentToSet, constraintValues.MinCurrent);
                currentToSet = constraintValues.MinCurrent.Value;
                if ((constraintValues.ChargeStopAllowedAt != default)
                    && (constraintValues.IsCharging == true))
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new("Waiting for charge stop", constraintValues.ChargeStopAllowedAt + _configurationWrapper.ChargingValueJobUpdateIntervall()));
                }
            }
            else if (currentToSet > constraintValues.MaxCurrent)
            {
                _logger.LogTrace("Decrease current to set from {oldCurrentToSet} as is above max current of {newCurrentToSet}.", currentToSet, constraintValues.MaxCurrent);
                currentToSet = constraintValues.MaxCurrent.Value;
            }

            if (constraintValues.IsCharging != true)
            {
                if (constraintValues.Soc >= constraintValues.MaxSoc)
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new("Configured max Soc is reached"));
                    return null;
                }
                if (constraintValues.CarSocLimit <= (constraintValues.Soc + _constants.MinimumSocDifference))
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new($"Car side SOC limit is reached. To start charging, the car side SOC limit needs to be at least {_constants.MinimumSocDifference}% higher than the actual SOC."));
                    return null;
                }
                if (constraintValues.IsCarFullyCharged == true
                    && !loadpoint.ManageChargingPowerByCar)
                {
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId, new("Charging stopped by car, e.g. it is full or its charge limit is reached."));
                    return null;
                }
                if ((constraintValues.ChargeStartAllowed != true) && (!ignoreTimers))
                {
                    if (constraintValues.ChargeStartAllowedAt != default)
                    {
                        _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                            new("Waiting for charge start", constraintValues.ChargeStartAllowedAt + _configurationWrapper.ChargingValueJobUpdateIntervall()));
                    }
                    return null;
                }
                if ((constraintValues.PhaseSwitchingEnabled == true)
                    && (constraintValues.LastIsChargingChange > (currentDate - constraintValues.PhaseSwitchCoolDownTime)))
                {
                    _logger.LogTrace("Waitingcool down time of {coolDownTime} before starting to charge", loadpoint);
                    _notChargingWithExpectedPowerReasonHelper.AddLoadPointSpecificReason(loadpoint.CarId, loadpoint.ChargingConnectorId,
                        new("Waiting phase switch cooldown time before starting to charge", constraintValues.LastIsChargingChange + constraintValues.PhaseSwitchCoolDownTime + _configurationWrapper.ChargingValueJobUpdateIntervall()));
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
        if ((constraintValues.ChargeMode == ChargeModeV2.MaxPower)
            || (constraintValues.MinSoc > constraintValues.Soc))
        {
            // Check for maximum SOC reached is already done above, so we can skip it here
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
                    c.MinimumSoc,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            constraintValues.MinCurrent = carConfigValues.MinimumAmpere;
            constraintValues.MaxCurrent = carConfigValues.MaximumAmpere;
            constraintValues.ChargeMode = carConfigValues.ChargeMode;
            var car = _settings.Cars.First(c => c.Id == carId);
            constraintValues.MinPhases = car.ActualPhases;
            constraintValues.MaxPhases = car.ActualPhases;
            constraintValues.MaxSoc = carConfigValues.MaximumSoc;
            constraintValues.MinSoc = carConfigValues.MinimumSoc;
            constraintValues.CarSocLimit = car.SocLimit;
            constraintValues.Soc = car.SoC;
            if (useCarToManageChargingSpeed)
            {
                DateTimeOffset? chargeStartAllowedAt = null;
                constraintValues.ChargeStartAllowed = (car.ShouldStartCharging.Value == true)
                                                      && IsTimeStampedValueRelevant(car.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn,
                                                          out chargeStartAllowedAt);
                constraintValues.ChargeStartAllowedAt = chargeStartAllowedAt;
                DateTimeOffset? chargeStopAllowedAt = null;
                constraintValues.ChargeStopAllowed = (car.ShouldStopCharging.Value == true)
                                                     && IsTimeStampedValueRelevant(car.ShouldStopCharging, currentDate, timeSpanUntilSwitchOff,
                                                         out chargeStopAllowedAt);
                constraintValues.ChargeStopAllowedAt = chargeStopAllowedAt;
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
            constraintValues.LastIsChargingChange = ocppValues?.IsCharging.LastChanged;
            if ((ocppValues?.IsCarFullyCharged.Value == true) && (!useCarToManageChargingSpeed))
            {
                _logger.LogTrace("Car for loadpoint (CarId: {carId}, ConnectorId: {connectorId}) is detected as fully charged {@isCarFullyCharged}", carId, connectorId, ocppValues?.IsCarFullyCharged);
                //Only set to max current if is fully charged is longer qual to 2 minutes ago as some cars switch this value on and off very quickly on charge start
                if (ocppValues?.IsCarFullyCharged.LastChanged < currentDate.AddMinutes(-2))
                {
                    constraintValues.IsCarFullyCharged = true;
                    _logger.LogTrace("Set {propertyName} to {propertyValue} on Loadpint (CarId: {carId}, ConnectorId {connectorId}) as is fully charged for more than two minutes", nameof(constraintValues.IsCarFullyCharged), constraintValues.IsCarFullyCharged, carId, connectorId);
                }
                else
                {
                    _logger.LogTrace("Car for loadpoint (CarId: {carId}, ConnectorId: {connectorId}) is detected as fully charged {@isCarFullyCharged} but last changed is less than 2 minutes ago, so not setting RequiresChargeStartDueToCarFullyChargedSinceLastCurrentSet", carId, connectorId, ocppValues?.IsCarFullyCharged);
                }
            }
            if ((ocppValues != default) && (!useCarToManageChargingSpeed))
            {
                constraintValues.IsCharging = ocppValues.IsCharging.Value;
                DateTimeOffset? chargeStartAllowedAt = null;
                constraintValues.ChargeStartAllowed = (ocppValues.ShouldStartCharging.Value == true)
                                                      && IsTimeStampedValueRelevant(ocppValues.ShouldStartCharging, currentDate, timeSpanUntilSwitchOn,
                                                          out chargeStartAllowedAt);
                constraintValues.ChargeStartAllowedAt = chargeStartAllowedAt;
                DateTimeOffset? chargeStopAllowedAt = null;
                constraintValues.ChargeStopAllowed = (ocppValues.ShouldStopCharging.Value == true)
                                                     && IsTimeStampedValueRelevant(ocppValues.ShouldStopCharging, currentDate, timeSpanUntilSwitchOff,
                                                         out chargeStopAllowedAt);
                constraintValues.ChargeStopAllowedAt = chargeStopAllowedAt;
                if (chargingConnectorConfigValues.AutoSwitchBetween1And3PhasesEnabled)
                {
                    _logger.LogTrace("Set auto phase switching timers.");
                    DateTimeOffset? phaseReductionAllowedBasedOnThreePhaseHandling = null;
                    _logger.LogTrace("Starting phase reduction check for connector {connectorId}", connectorId);
                    _logger.LogTrace("Initial values - currentDate: {currentDate}, timeSpanUntilSwitchOff: {timeSpanUntilSwitchOff}", currentDate, timeSpanUntilSwitchOff);
                    _logger.LogTrace("CanHandlePowerOnOnePhase: {@canHandlePowerOnOnePhase}", ocppValues.CanHandlePowerOnOnePhase);
                    _logger.LogTrace("CanHandlePowerOnThreePhase: {@canHandlePowerOnThreePhase}", ocppValues.CanHandlePowerOnThreePhase);

                    var phaseReductionOnePhaseResult = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOff, true,
                        out var phaseReductionAllowedBasedOnOnePhaseHandling);
                    _logger.LogTrace("Phase reduction one phase handling result: {result}, relevantAt: {relevantAt}",
                        phaseReductionOnePhaseResult, phaseReductionAllowedBasedOnOnePhaseHandling);

                    var phaseReductionThreePhaseResult = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOff, false,
                        out phaseReductionAllowedBasedOnThreePhaseHandling);
                    _logger.LogTrace("Phase reduction three phase handling result: {result}, relevantAt: {relevantAt}",
                        phaseReductionThreePhaseResult, phaseReductionAllowedBasedOnThreePhaseHandling);

                    constraintValues.PhaseReductionAllowed = phaseReductionOnePhaseResult && phaseReductionThreePhaseResult;
                    _logger.LogTrace("Phase reduction allowed: {phaseReductionAllowed}", constraintValues.PhaseReductionAllowed);

                    if ((constraintValues.PhaseReductionAllowed != true)
                        && (phaseReductionAllowedBasedOnOnePhaseHandling != default)
                        && (phaseReductionAllowedBasedOnThreePhaseHandling != default))
                    {
                        _logger.LogTrace("Phase reduction not allowed, calculating PhaseReductionAllowedAt");
                        _logger.LogTrace("OnePhaseHandling date: {onePhase}, ThreePhaseHandling date: {threePhase}",
                            phaseReductionAllowedBasedOnOnePhaseHandling, phaseReductionAllowedBasedOnThreePhaseHandling);

                        constraintValues.PhaseReductionAllowedAt = phaseReductionAllowedBasedOnOnePhaseHandling > phaseReductionAllowedBasedOnThreePhaseHandling
                            ? phaseReductionAllowedBasedOnOnePhaseHandling
                            : phaseReductionAllowedBasedOnThreePhaseHandling;

                        _logger.LogTrace("Setting phase reduction allowed at value for ocppConnector {connectorId} to {phaseReductionAllowedAt}",
                            connectorId, constraintValues.PhaseReductionAllowedAt);
                    }
                    else
                    {
                        _logger.LogTrace("Skipping PhaseReductionAllowedAt calculation - PhaseReductionAllowed: {allowed}, OnePhaseDate: {onePhase}, ThreePhaseDate: {threePhase}",
                            constraintValues.PhaseReductionAllowed, phaseReductionAllowedBasedOnOnePhaseHandling, phaseReductionAllowedBasedOnThreePhaseHandling);
                    }

                    // Phase Increase Logic
                    DateTimeOffset? phaseIncreaseAllowedBasedOnThreePhaseHandling = null;
                    _logger.LogTrace("Starting phase increase check for connector {connectorId}", connectorId);

                    var phaseIncreaseOnePhaseResult = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnOnePhase, currentDate, timeSpanUntilSwitchOn, false,
                        out var phaseIncreaseAllowedBasedOnOnePhaseHandling);
                    _logger.LogTrace("Phase increase one phase handling result: {result}, relevantAt: {relevantAt}",
                        phaseIncreaseOnePhaseResult, phaseIncreaseAllowedBasedOnOnePhaseHandling);

                    var phaseIncreaseThreePhaseResult = IsTimeStampedValueRelevantAndFullFilled(ocppValues.CanHandlePowerOnThreePhase, currentDate, timeSpanUntilSwitchOn, true,
                        out phaseIncreaseAllowedBasedOnThreePhaseHandling);
                    _logger.LogTrace("Phase increase three phase handling result: {result}, relevantAt: {relevantAt}",
                        phaseIncreaseThreePhaseResult, phaseIncreaseAllowedBasedOnThreePhaseHandling);

                    constraintValues.PhaseIncreaseAllowed = phaseIncreaseOnePhaseResult && phaseIncreaseThreePhaseResult;
                    _logger.LogTrace("Phase increase allowed: {phaseIncreaseAllowed}", constraintValues.PhaseIncreaseAllowed);

                    if ((constraintValues.PhaseIncreaseAllowed != true)
                        && (phaseIncreaseAllowedBasedOnOnePhaseHandling != default)
                        && (phaseIncreaseAllowedBasedOnThreePhaseHandling != default))
                    {
                        _logger.LogTrace("Phase increase not allowed, calculating PhaseIncreaseAllowedAt");
                        _logger.LogTrace("OnePhaseHandling date: {onePhase}, ThreePhaseHandling date: {threePhase}",
                            phaseIncreaseAllowedBasedOnOnePhaseHandling, phaseIncreaseAllowedBasedOnThreePhaseHandling);

                        constraintValues.PhaseIncreaseAllowedAt = phaseIncreaseAllowedBasedOnOnePhaseHandling > phaseIncreaseAllowedBasedOnThreePhaseHandling
                            ? phaseIncreaseAllowedBasedOnOnePhaseHandling
                            : phaseIncreaseAllowedBasedOnThreePhaseHandling;

                        _logger.LogTrace("Setting phase increase allowed at value for ocppConnector {connectorId} to {phaseIncreaseAllowedAt}",
                            connectorId, constraintValues.PhaseIncreaseAllowedAt);
                    }
                    else
                    {
                        _logger.LogTrace("Skipping PhaseIncreaseAllowedAt calculation - PhaseIncreaseAllowed: {allowed}, OnePhaseDate: {onePhase}, ThreePhaseDate: {threePhase}",
                            constraintValues.PhaseIncreaseAllowed, phaseIncreaseAllowedBasedOnOnePhaseHandling, phaseIncreaseAllowedBasedOnThreePhaseHandling);
                    }
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
        _logger.LogTrace("{method}({@timeStampedValue}, {currentDate}, {timeSpanUntilIsRelevant}, {comparator})",
            nameof(IsTimeStampedValueRelevantAndFullFilled), timeStampedValue, currentDate, timeSpanUntilIsRelevant, comparator);

        var isValueRelevant = IsTimeStampedValueRelevant(timeStampedValue, currentDate, timeSpanUntilIsRelevant, out relevantAt);
        _logger.LogTrace("IsTimeStampedValueRelevant returned: {isValueRelevant}, relevantAt: {relevantAt}", isValueRelevant, relevantAt);

        var valuesEqual = EqualityComparer<T>.Default.Equals(timeStampedValue.Value, comparator);
        _logger.LogTrace("Value comparison - timeStampedValue.Value: {value}, comparator: {comparator}, equals: {equals}",
            timeStampedValue.Value, comparator, valuesEqual);

        var result = isValueRelevant && valuesEqual;
        _logger.LogTrace("IsTimeStampedValueRelevantAndFullFilled final result: {result}", result);

        return result;
    }

    private bool IsTimeStampedValueRelevant<T>(DtoTimeStampedValue<T> timeStampedValue, DateTimeOffset currentDate,
        TimeSpan timeSpanUntilIsRelevant, out DateTimeOffset? relevantAt)
    {
        _logger.LogTrace("{method}({@timeStampedValue}, {currentDate}, {timespanUntilIsRelevant})",
            nameof(IsTimeStampedValueRelevant), timeStampedValue, currentDate, timeSpanUntilIsRelevant);

        relevantAt = null;

        if (timeStampedValue.LastChanged == default)
        {
            _logger.LogTrace("No last changed time set for timestamped value (LastChanged is default), assuming it is relevant.");
            return true; // If no last changed time is set, we assume it is relevant as it might never change when the value is true since startup
        }

        var thresholdDate = currentDate - timeSpanUntilIsRelevant;
        _logger.LogTrace("Calculating relevance - LastChanged: {lastChanged}, currentDate: {currentDate}, threshold: {threshold}",
            timeStampedValue.LastChanged, currentDate, thresholdDate);

        var isRelevant = timeStampedValue.LastChanged < thresholdDate;

        if (!isRelevant)
        {
            _logger.LogTrace("TimeStampedValue is not relevant yet (LastChanged >= threshold)");
            relevantAt = timeStampedValue.LastChanged.Value.Add(timeSpanUntilIsRelevant);
            _logger.LogTrace("Calculated relevantAt: {relevantAt} (LastChanged + timeSpan)", relevantAt);
        }
        else
        {
            _logger.LogTrace("Time stamped value is relevant (LastChanged < threshold)");
        }

        _logger.LogTrace("IsTimeStampedValueRelevant returning: {isRelevant}", isRelevant);
        return isRelevant;
    }
}
