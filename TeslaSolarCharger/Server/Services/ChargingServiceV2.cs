using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
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
                currentToSetAfterMinMaxChecks = chargerInformation.MaxCurrent.Value;            }
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
