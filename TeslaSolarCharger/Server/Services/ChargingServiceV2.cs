using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
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
    private readonly ITscOnlyChargingCostService _tscOnlyChargingCostService;

    public ChargingServiceV2(ILogger<ChargingServiceV2> logger,
        IConfigurationWrapper configurationWrapper,
        ILoadPointManagementService loadPointManagementService,
        ITeslaSolarChargerContext context,
        IDateTimeProvider dateTimeProvider,
        IOcppChargePointActionService ocppChargePointActionService,
        ISettings settings,
        ITscOnlyChargingCostService tscOnlyChargingCostService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _loadPointManagementService = loadPointManagementService;
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _ocppChargePointActionService = ocppChargePointActionService;
        _settings = settings;
        _tscOnlyChargingCostService = tscOnlyChargingCostService;
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
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var dtoLoadpoint in loadPoints)
        {
            if (dtoLoadpoint.Car != default)
            {
                var (carUsableEnergy, carSoC, maxPhases, maxCurrent) = await GetChargingScheduleRelevantData(dtoLoadpoint.Car.Id, dtoLoadpoint.OcppConnectorId).ConfigureAwait(false);
                if (dtoLoadpoint.Car.MinimumSoC < dtoLoadpoint.Car.SoC)
                {
                    var earliestPossibleChargingSchedule =
                        GenerateEarliestOrLatestPossibleChargingSchedule(dtoLoadpoint.Car.MinimumSoC, null,
                            carUsableEnergy, carSoC, maxPhases, maxCurrent);
                }
                var nextTarget = await GetNextTarget(dtoLoadpoint.Car.Id, cancellationToken).ConfigureAwait(false);
                if (nextTarget != default)
                {
                    var gridPrices = await _tscOnlyChargingCostService.GetPricesInTimeSpan(currentDate, nextTarget.NextExecutionTime);
                    var latestPossibleChargingSchedule =
                        GenerateEarliestOrLatestPossibleChargingSchedule(nextTarget.TargetSoc, nextTarget.NextExecutionTime,
                            carUsableEnergy, carSoC, maxPhases, maxCurrent);
                }
            }
        }

        #region Workaround while new service is not working
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


        #endregion
    }

    private async Task<DtoTimeZonedChargingTarget?> GetNextTarget(int carId, CancellationToken cancellationToken)
    {
        var chargingTargets = await _context.CarChargingTargets
            .Where(c => c.CarId == carId)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (chargingTargets.Count < 1)
        {
            _logger.LogDebug("No charging targets found for car {carId}.", carId);
            return null;
        }
        DtoTimeZonedChargingTarget? nextTarget = null;
        foreach (var carChargingTarget in chargingTargets)
        {
            var nextTargetUtc = GetNextTargetUtc(carChargingTarget);
            if (nextTarget == default || (nextTargetUtc < nextTarget.NextExecutionTime))
            {
                nextTarget = new()
                {
                    Id = carChargingTarget.Id,
                    TargetSoc = carChargingTarget.TargetSoc,
                    TargetDate = carChargingTarget.TargetDate,
                    TargetTime = carChargingTarget.TargetTime,
                    RepeatOnMondays = carChargingTarget.RepeatOnMondays,
                    RepeatOnTuesdays = carChargingTarget.RepeatOnTuesdays,
                    RepeatOnWednesdays = carChargingTarget.RepeatOnWednesdays,
                    RepeatOnThursdays = carChargingTarget.RepeatOnThursdays,
                    RepeatOnFridays = carChargingTarget.RepeatOnFridays,
                    RepeatOnSaturdays = carChargingTarget.RepeatOnSaturdays,
                    RepeatOnSundays = carChargingTarget.RepeatOnSundays,
                    ClientTimeZone = carChargingTarget.ClientTimeZone,
                    CarId = carChargingTarget.CarId,
                    NextExecutionTime = nextTargetUtc,
                };
            }
        }
        return nextTarget;
    }

    /// <summary>
    /// When targetTimeUtc is null it will generate the eraliest possible charging schedule, otherwise the latest possible charging schedule.
    /// </summary>
    /// <param name="chargingTargetSoc"></param>
    /// <param name="targetTimeUtc"></param>
    /// <param name="carUsableEnergy"></param>
    /// <param name="carSoC"></param>
    /// <param name="maxPhases"></param>
    /// <param name="maxCurrent"></param>
    /// <returns></returns>
    private DtoChargingSchedule? GenerateEarliestOrLatestPossibleChargingSchedule(
        int chargingTargetSoc,
        DateTimeOffset? targetTimeUtc,
        int? carUsableEnergy, int? carSoC, int? maxPhases, int? maxCurrent)
    {
        _logger.LogTrace(
            "{method}({chargingTargetSoc}, {targetTimeUtc}, {usableEnergy}, {soc}, {maxPhases}, {maxCurrent})",
            nameof(GenerateEarliestOrLatestPossibleChargingSchedule),
            chargingTargetSoc, targetTimeUtc, carUsableEnergy, carSoC, maxPhases, maxCurrent);

        var energyToCharge = CalculateEnergyToCharge(
            chargingTargetSoc,
            carSoC,
            carUsableEnergy);

        if (energyToCharge == default || energyToCharge < 1)
        {
            return null;
        }

        var maxChargingPower = GetMaxChargingPower(maxPhases, maxCurrent);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (maxChargingPower == default || maxChargingPower <= 0)
        {
            _logger.LogWarning("No valid charging power found for car with usable energy {usableEnergy} and SoC {soc}.", carUsableEnergy, carSoC);
            return null;
        }

        var chargingDuration = CalculateChargingDuration(
            energyToCharge.Value,
            maxChargingPower.Value);

        if (targetTimeUtc == default)
        {
            return new DtoChargingSchedule()
            {
                StartTime = _dateTimeProvider.DateTimeOffSetUtcNow(),
                EndTime = _dateTimeProvider.DateTimeOffSetUtcNow() + chargingDuration,
                ChargingPower = (int)maxChargingPower,
            };
        }

        var startTime = targetTimeUtc.Value - chargingDuration;

        return new DtoChargingSchedule()
        {
            StartTime = startTime,
            EndTime = targetTimeUtc.Value,
            ChargingPower = (int)maxChargingPower,
        };
    }

    private double? GetMaxChargingPower(int? maxPhases, int? maxCurrent)
    {
        var voltage = _settings.AverageHomeGridVoltage ?? 230;
        if (maxPhases == default || maxCurrent == default)
        {
            return null;
        }
        var maxChargingPower = CalculateMaxChargingPower(
            maxCurrent.Value,
            maxPhases.Value,
            voltage);
        return maxChargingPower;
    }

    private async Task<(int? carUsableEnergy, int? carSoC, int? maxPhases, int? maxCurrent)> GetChargingScheduleRelevantData(int? carId, int? chargingConnectorId)
    {
        var connectorData = chargingConnectorId != default
            ? await _context.OcppChargingStationConnectors
                .Where(c => c.Id == chargingConnectorId)
                .Select(c => new { c.ConnectedPhasesCount, c.MaxCurrent })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        var carData = carId != default
            ? await _context.Cars
                .Where(c => c.Id == carId)
                .Select(c => new { c.MaximumAmpere, c.UsableEnergy })
                .FirstOrDefaultAsync()
                .ConfigureAwait(false)
            : null;

        // 2) Get settings for car phases and SoC
        var carSetting = _settings.Cars.FirstOrDefault(c => c.Id == carId);
        var carSoC = carSetting?.SoC;
        var carPhases = carSetting?.ActualPhases;

        // 3) Compute atomic values
        var maxPhases = CalculateMaxPhases(
            connectorData?.ConnectedPhasesCount,
            carPhases);

        var maxCurrent = CalculateMaxCurrent(
            connectorData?.MaxCurrent,
            carData?.MaximumAmpere);
        return (carData?.UsableEnergy, carSoC, maxPhases, maxCurrent);
    }

    // Helpers — pure, primitive parameters

    private int? CalculateMaxPhases(
        int? connectorPhases,
        int? carPhases)
    {
        if (connectorPhases == default)
        {
            return carPhases;
        }

        if (carPhases != default && carPhases < connectorPhases)
        {
            return carPhases;
        }

        return connectorPhases;
    }

    private int? CalculateMaxCurrent(
        int? connectorCurrent,
        int? carMaxAmpere)
    {
        if (connectorCurrent == default)
        {
            return carMaxAmpere;
        }

        if (carMaxAmpere != default && carMaxAmpere < connectorCurrent)
        {
            return carMaxAmpere;
        }

        return connectorCurrent;
    }

    private int? CalculateEnergyToCharge(
        int chargingTargetSoc,
        int? currentSoC,
        int? usableEnergy)
    {
        if (usableEnergy == default || currentSoC == default || usableEnergy <= 0)
        {
            return default;
        }

        var socDiff = chargingTargetSoc - currentSoC;
        var energyWh = socDiff * usableEnergy * 10; // soc*10 vs usableEnergy*1000 scale

        return energyWh > 0
            ? energyWh
            : default;
    }

    private double CalculateMaxChargingPower(
        int maxCurrent,
        int maxPhases,
        int voltage)
    {
        // W = A * phases * V
        return (double)maxCurrent * maxPhases * voltage;
    }

    private TimeSpan CalculateChargingDuration(
        int energyToChargeWh,
        double maxChargingPowerW)
    {
        // hours = Wh / W
        return TimeSpan.FromHours(energyToChargeWh / maxChargingPowerW);
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
