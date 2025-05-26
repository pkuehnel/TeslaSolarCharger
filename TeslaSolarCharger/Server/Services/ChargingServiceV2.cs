using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class ChargingServiceV2 : IChargingServiceV2
{
    private readonly ILogger<ChargingServiceV2> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly ITeslaSolarChargerContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOcppChargePointActionService _ocppChargePointActionService;
    private readonly ISettings _settings;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
    }

    public async Task SetNewChargingValues(int? restPowerToUse, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({restPowerToUse})", nameof(SetNewChargingValues), restPowerToUse);
        if (!_configurationWrapper.UseChargingServiceV2())
        {
            _logger.LogDebug("Charging Service V2 not enabled, skip setting charging values");
            return;
        }

        var loadPoints = await _loadPointManagementService.GetPluggedInLoadPoints();
        //foreach (var dtoLoadpoint in loadPoints)
        //{
        //    var chargingSchedules = await GetChargingSchedulesForLoadPoint(dtoLoadpoint.Car?.Id, dtoLoadpoint.OcppConnectorId, cancellationToken);
        //}

        var currentLocalDate = _dateTimeProvider.Now();
        foreach (var loadPoint in loadPoints)
        {
            if (loadPoint.OcppConnectorState == default || loadPoint.OcppConnectorId == default)
            {
                continue;
            }
            if (loadPoint.Car != default)
            {
                await SetChargingStationToMaxPowerIfTeslaIsConnected(loadPoint, currentLocalDate, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (restPowerToUse == default)
            {
                //ToDo: implement rest power to use before this foreach loop
                continue;
            }

            if (loadPoint.OcppConnectorState.IsCarFullyCharged.Value == true)
            {
                _logger.LogTrace("Car on chargepoint {chargingConnectorId} is full, no change in charging power required", loadPoint.OcppConnectorId);
                continue;
            }

            var voltage = _settings.AverageHomeGridVoltage ?? 230;
            var phasesToCalculateWith = 3;
            var chargerInformation = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == loadPoint.OcppConnectorId)
                .Select(c => new
                {
                    c.MinCurrent,
                    c.SwitchOffAtCurrent,
                    c.SwitchOnAtCurrent,
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                })
                .FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if ((loadPoint.OcppConnectorState.PhaseCount.Value) == default
                || (loadPoint.OcppConnectorState.PhaseCount.Value == 0))
            {
                if (chargerInformation.ConnectedPhasesCount != default)
                {
                    phasesToCalculateWith = chargerInformation.ConnectedPhasesCount.Value;
                }
            }
            else
            {
                phasesToCalculateWith = loadPoint.OcppConnectorState.PhaseCount.Value.Value;
            }
            var currentIncreaseBeforeMinMaxChecks = ((decimal)restPowerToUse.Value) / voltage / phasesToCalculateWith;
            var currentIncreaseAfterMinMaxChecks = currentIncreaseBeforeMinMaxChecks;
            var currentCurrent = loadPoint.OcppConnectorState.ChargingCurrent.Value;
            var currentToSetBeforeMinMaxChecks = currentCurrent + currentIncreaseBeforeMinMaxChecks;
            var currentToSetAfterMinMaxChecks = currentToSetBeforeMinMaxChecks;
            if (chargerInformation.MaxCurrent < currentToSetBeforeMinMaxChecks)
            {
                currentToSetAfterMinMaxChecks = chargerInformation.MaxCurrent.Value;
            }
            else if (chargerInformation.MinCurrent > currentToSetBeforeMinMaxChecks)
            {
                currentToSetAfterMinMaxChecks = chargerInformation.MinCurrent.Value;
            }
            currentIncreaseAfterMinMaxChecks += currentToSetAfterMinMaxChecks - currentToSetBeforeMinMaxChecks;
            if (loadPoint.OcppConnectorState.IsCharging.Value)
            {
                if (currentToSetBeforeMinMaxChecks < chargerInformation.SwitchOffAtCurrent)
                {
                    var result = await _ocppChargePointActionService.StopCharging(loadPoint.OcppConnectorId.Value, cancellationToken)
                        .ConfigureAwait(false);
                    if (!result.HasError)
                    {
                        restPowerToUse += (int)(currentCurrent * voltage * phasesToCalculateWith);
                    }
                }
                else
                {
                    var result = await _ocppChargePointActionService.SetChargingCurrent(loadPoint.OcppConnectorId.Value, currentToSetAfterMinMaxChecks, null,
                        cancellationToken).ConfigureAwait(false);
                    if (!result.HasError)
                    {
                        restPowerToUse -= (int)(currentIncreaseAfterMinMaxChecks * voltage * phasesToCalculateWith);
                    }
                }

            }
            else
            {
                if (currentToSetBeforeMinMaxChecks > chargerInformation.SwitchOnAtCurrent)
                {
                    var result = await _ocppChargePointActionService.StartCharging(loadPoint.OcppConnectorId.Value, currentToSetAfterMinMaxChecks, null,
                        cancellationToken).ConfigureAwait(false);
                    if (!result.HasError)
                    {
                        restPowerToUse -= (int)(currentIncreaseAfterMinMaxChecks * voltage * phasesToCalculateWith);
                    }
                }
                else
                {
                    _logger.LogTrace("Do not start charging as current to set {currentToSet} is lower than switch on current {minimumCurrent}",
                        currentToSetAfterMinMaxChecks, chargerInformation.SwitchOnAtCurrent);
                }
            }
        }
    }

    public async Task<List<DtoChargingSchedule>> GetChargingSchedulesForLoadPoint(int? carId, int? chargingConnectorId, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({carId}, {chargingConnectorId})", nameof(GetChargingSchedulesForLoadPoint), carId, chargingConnectorId);
        if (carId == default)
        {
            _logger.LogDebug("No car found for loadpoint {chargingConnectorId}, skipping schedule retrieval.", chargingConnectorId);
            return new List<DtoChargingSchedule>();
        }

        var chargingTargets = await _context.CarChargingTargets
            .Where(c => c.CarId == carId)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (chargingTargets.Count < 1)
        {
            _logger.LogDebug("No charging targets found for car {carId}, skipping schedule retrieval.", carId);
            return new();
        }
        DtoTimeZonedChargingTarget? nextTarget = null;
        foreach (var carChargingTarget in chargingTargets)
        {
            var nextTargetUtc = GetNextTargetUtc(carChargingTarget);
            if (nextTarget == default || (nextTargetUtc < nextTarget?.NextExecutionTime))
            {
                nextTarget = new()
                {
                    Id = carChargingTarget.Id,
                    NextExecutionTime = nextTargetUtc,
                };
            }
        }
        if(nextTarget == default)
        {
            _logger.LogDebug("No next target found for car {carId}, skipping schedule retrieval.", carId);
            return new List<DtoChargingSchedule>();
        }
        var relevantChargingTarget = chargingTargets.First(c => c.Id == nextTarget?.Id);
        var latestPossibleMaxPowerSchedule =
            await GenerateLatestPossibleChargingSchedule(carId, chargingConnectorId, relevantChargingTarget, nextTarget.NextExecutionTime);
        if (latestPossibleMaxPowerSchedule == default)
        {
            return new();
        }
        var result = new List<DtoChargingSchedule>();
        result.Add(latestPossibleMaxPowerSchedule);
        return result;
    }

    private async Task<DtoChargingSchedule?> GenerateLatestPossibleChargingSchedule(int? carId, int? chargingConnectorId, CarChargingTarget chargingTarget, DateTimeOffset targetTimeUtc)
    {
        _logger.LogTrace("{method}({carId}, {chargingConnectorId}, {@chargingTarget})", nameof(GenerateLatestPossibleChargingSchedule), carId, chargingConnectorId, chargingTarget);
        int? maxPhases = null;
        int? maxCurrent = null;
        int? energyToCharge = null;
        if (chargingConnectorId != default)
        {
            var chargingConnectorValues = await _context.OcppChargingStationConnectors
                .Where(c => c.Id == chargingConnectorId)
                .Select(c => new
                {
                    c.MaxCurrent,
                    c.ConnectedPhasesCount,
                })
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (chargingConnectorValues != default)
            {
                maxPhases = chargingConnectorValues.ConnectedPhasesCount;
                maxCurrent = chargingConnectorValues.MaxCurrent;
            }
        }
        if (carId != default)
        {
            var carValues = await _context.Cars
                .Where(c => c.Id == carId)
                .Select(c => new
                {
                    c.MaximumAmpere,
                    c.UsableEnergy,
                })
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (maxCurrent == default)
            {
                maxCurrent = carValues?.MaximumAmpere;
            }
            else if (carValues != default && carValues.MaximumAmpere < maxCurrent)
            {
                maxCurrent = carValues.MaximumAmpere;
            }
            var car = _settings.Cars.FirstOrDefault(c => c.Id == carId);
            var maxCarPhases = car?.ActualPhases;
            if (maxPhases == default)
            {
                maxPhases = maxCarPhases;
            }
            else if (maxCarPhases < maxPhases)
            {
                maxPhases = maxCarPhases;
            }
            if(carValues != default && carValues.UsableEnergy > 0)
            {
                var socToCharge = chargingTarget.TargetSoc - (car?.SoC ?? 100);
                // *10 as soc is 100 times of the real value and usable energy is 1000 times of the real value
                var energyToChargeInWhBasedOnCarSoc = socToCharge * carValues.UsableEnergy * 10;
                if (energyToCharge == default)
                {
                    energyToCharge = energyToChargeInWhBasedOnCarSoc;
                }
                else if (energyToChargeInWhBasedOnCarSoc > energyToCharge)
                {
                    energyToCharge = energyToChargeInWhBasedOnCarSoc;
                }
            }
        }

        if ((energyToCharge == default) || (energyToCharge < 1))
        {
            _logger.LogDebug("No energy to charge for car {carId} and charging connector {chargingConnectorId}, skipping schedule generation.", carId, chargingConnectorId);
            return null;
        }
        var voltageToCalculatWith = _settings.AverageHomeGridVoltage ?? 230;
        if(maxCurrent == default || maxPhases == default)
        {
            _logger.LogError("Could not determine maximum current or phases for car {carId} and charging connector {chargingConnectorId}.", carId, chargingConnectorId);
            return null;
        }
        var maxChargingPower  = maxCurrent.Value * maxPhases.Value * voltageToCalculatWith;
        _logger.LogTrace("Loadpoint can charge with max {maxChargingPower}W, energy to charge {energyToCharge}Wh", maxChargingPower, energyToCharge);
        //Cast to doue to avoid integer division which is unprecise
        var maxPowerChargingDuration = TimeSpan.FromHours((double)energyToCharge.Value / maxChargingPower);
        _logger.LogTrace("Max power charging duration is {maxPowerChargingDuration}", maxPowerChargingDuration);
        var startChargingTime = targetTimeUtc - maxPowerChargingDuration;
        _logger.LogTrace("Start charging time is {startChargingTime}", startChargingTime);
        return new()
        {
            StartTime = startChargingTime,
            EndTime = targetTimeUtc,
            ChargingCurrent = maxCurrent.Value,
            ChargingPower = maxChargingPower,
            NumberOfPhases = maxPhases.Value,
        };
    }

    internal DateTimeOffset GetNextTargetUtc(CarChargingTarget chargingTarget)
    {
        var tz = string.IsNullOrWhiteSpace(chargingTarget.ClientTimeZone)
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(chargingTarget.ClientTimeZone);

        var currentUtcDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var earliestExecutionTime = TimeZoneInfo.ConvertTime(currentUtcDate, tz);

        DateTimeOffset? candidate;

        if (chargingTarget.TargetDate.HasValue)
        {
            candidate = new DateTimeOffset(chargingTarget.TargetDate.Value, chargingTarget.TargetTime,
                tz.GetUtcOffset(new(chargingTarget.TargetDate.Value, chargingTarget.TargetTime)));

            //candidate is irrelevant if it is in the past
            if (candidate > earliestExecutionTime)
            {
                // if no repetition is set, return the candidate
                if (!chargingTarget.RepeatOnMondays
                    && !chargingTarget.RepeatOnTuesdays
                    && !chargingTarget.RepeatOnWednesdays
                    && !chargingTarget.RepeatOnThursdays
                    && !chargingTarget.RepeatOnFridays
                    && !chargingTarget.RepeatOnSaturdays
                    && !chargingTarget.RepeatOnSundays)
                {
                    return candidate.Value.ToUniversalTime();
                }
                // if repetition is set the set date is considered as the earliest execution time. But we still need to check if it is the first enabled weekday
                earliestExecutionTime = candidate.Value;
            }
            // otherwise fall back to the repeating schedule
        }


        //ToDO: take earliest execution time and use first repeating weekday at or after that date
        for (var i = 0; i < 7; i++)
        {
            var date = earliestExecutionTime.Date.AddDays(i);
            var isEnabled = date.DayOfWeek switch
            {
                DayOfWeek.Monday => chargingTarget.RepeatOnMondays,
                DayOfWeek.Tuesday => chargingTarget.RepeatOnTuesdays,
                DayOfWeek.Wednesday => chargingTarget.RepeatOnWednesdays,
                DayOfWeek.Thursday => chargingTarget.RepeatOnThursdays,
                DayOfWeek.Friday => chargingTarget.RepeatOnFridays,
                DayOfWeek.Saturday => chargingTarget.RepeatOnSaturdays,
                DayOfWeek.Sunday => chargingTarget.RepeatOnSundays,
                _ => false,
            };

            if (!isEnabled)
                continue;

            // build the local DateTimeOffset for that day + target time
            var localDt = date + chargingTarget.TargetTime.ToTimeSpan();
            candidate = new DateTimeOffset(localDt, tz.GetUtcOffset(localDt));

            if (candidate >= earliestExecutionTime)
            {
                return candidate.Value.ToUniversalTime();
            }
        }

        throw new InvalidOperationException(
            "Could not find any upcoming target. Please check TargetDate or repeat flags."
        );
    }

    private async Task SetChargingStationToMaxPowerIfTeslaIsConnected(
        DtoLoadpoint loadPoint, DateTime currentLocalDate, CancellationToken cancellationToken)
    {
        if (loadPoint.Car == default || loadPoint.OcppConnectorState == default || loadPoint.OcppConnectorId == default)
        {
            throw new ArgumentNullException(nameof(loadPoint), "Car, OcppChargingConnector and OCPP Charging Connector ID are note allowed to be null here");
        }

        if (loadPoint.Car.AutoFullSpeedCharge || (loadPoint.Car.ShouldStartChargingSince < currentLocalDate))
        {
            _logger.LogTrace("Loadpoint with car ID {carId} and chargingConnectorId {chargingConnectorId} should currently charge. Setting ocpp station to max current charge.", loadPoint.Car.Id, loadPoint.OcppConnectorId);
            if (loadPoint.OcppConnectorState.IsCarFullyCharged.Value != true)
            {
                _logger.LogInformation("Not fully charged Tesla connected to OCPP Charging station.");
                var chargePointInfo = await _context.OcppChargingStationConnectors
                    .Where(c => c.Id == loadPoint.OcppConnectorId)
                    .Select(c => new
                    {
                        c.MaxCurrent,
                        c.ConnectedPhasesCount,
                    })
                    .FirstAsync(cancellationToken: cancellationToken);
                if (chargePointInfo.MaxCurrent == default)
                {
                    _logger.LogError("Chargepoint not fully configured, can not set charging current");
                    return;
                }
                if (!loadPoint.OcppConnectorState.IsCharging.Value)
                {
                    await _ocppChargePointActionService.StartCharging(loadPoint.OcppConnectorId.Value,
                        chargePointInfo.MaxCurrent.Value,
                        chargePointInfo.ConnectedPhasesCount,
                        cancellationToken).ConfigureAwait(false);
                }
                else if ((loadPoint.Car.ChargerPilotCurrent < loadPoint.Car.MaximumAmpere)
                         && (loadPoint.Car.ChargerPilotCurrent < chargePointInfo.MaxCurrent))
                {

                    await _ocppChargePointActionService.SetChargingCurrent(loadPoint.OcppConnectorId.Value,
                        chargePointInfo.MaxCurrent.Value,
                        chargePointInfo.ConnectedPhasesCount,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
