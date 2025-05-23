using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class OcppChargePointConfigurationService(ILogger<OcppChargePointConfigurationService> logger,
    IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService) : IOcppChargePointConfigurationService
{
    private const string MeterValueSampleIntervalKey = "MeterValueSampleInterval";
    private const string MeterValuesSampledDataKey = "MeterValuesSampledData";
    private const string NumberOfConnectorsKey = "NumberOfConnectors";
    private const string UnlockConnectorOnEvSideDisconnectKey = "UnlockConnectorOnEVSideDisconnect";
    private const string ConnectorSwitch3To1PhaseSupportedKey = "ConnectorSwitch3to1PhaseSupported";

    private const int MeterValuesSampleIntervalDefaultValue = 5;
    private readonly HashSet<string> _meterValuesSampledDataDefaultValue = ["Power.Active.Import","Current.Import","Voltage"];

    public async Task<Result<object>> TriggerStatusNotification(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargePointId})", nameof(TriggerStatusNotification), chargepointId);
        var request = new TriggerMessageRequest()
        {
            RequestedMessage = RequestedMessage.StatusNotification,
        };

        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<TriggerMessageResponse>(chargepointId,
                "TriggerMessage",
                request,
                cancellationToken);
            if (ocppResponse.Status == TriggerMessageStatus.Accepted)
            {
                return new(ocppResponse, null, null);
            }
            return new(ocppResponse, $"The chargepoint responded with status {ocppResponse.Status}", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargepointId);
            return new(null, ex.Message, null);
        }
    }

    public async Task<Result<object>> RebootCharger(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(RebootCharger), chargepointId);
        var request = new ResetRequest() { Type = ResetType.Hard, };

        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<ResetResponse>(chargepointId,
                "Reset",
                request,
                cancellationToken);
            if (ocppResponse.Status == ResetStatus.Accepted)
            {
                return new(ocppResponse, null, null);
            }
            return new(ocppResponse, $"The chargepoint responded with status {ocppResponse.Status}", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargepointId);
            return new(null, ex.Message, null);
        }
    }

    public async Task<Result<bool?>> CanSwitchBetween1And3Phases(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(CanSwitchBetween1And3Phases), chargepointId);
        var getConfigurationResponse = await GetOcppConfigurations(chargepointId, cancellationToken);
        if (getConfigurationResponse.HasError)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, getConfigurationResponse.ErrorMessage);
            return new(null, getConfigurationResponse.ErrorMessage, null);
        }
        var result = getConfigurationResponse.Data;
        if (result?.ConfigurationKey == null)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Response was null", chargepointId);
            return new(null, "Response was null", null);
        }
        var canSwitchBetweenPhases = result.ConfigurationKey.FirstOrDefault(x => x.Key == ConnectorSwitch3To1PhaseSupportedKey);
        if (canSwitchBetweenPhases?.Value == null)
        {
            logger.LogError("Could not get can switch between phases for charge point {chargePointId}. Value was null", chargepointId);
            return new(null, "Value was null", null);
        }
        if (bool.TryParse(canSwitchBetweenPhases.Value, out var boolCanSwitchBetweenPhasesValue))
        {
            return new(boolCanSwitchBetweenPhasesValue, null, null);
        }
        return new(null, $"Could not parse boolean of can switch phases: {canSwitchBetweenPhases.Value}", null);
    }

    public async Task<Result<int?>> NumberOfConnectors(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(NumberOfConnectors), chargepointId);
        var getConfigurationResponse = await GetOcppConfigurations(chargepointId, cancellationToken);
        if (getConfigurationResponse.HasError)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, getConfigurationResponse.ErrorMessage);
            return new(null, getConfigurationResponse.ErrorMessage, null);
        }
        var result = getConfigurationResponse.Data;
        if (result?.ConfigurationKey == null)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Response was null", chargepointId);
            return new(null, "Response was null", null);
        }
        var numberOfConnectors = result.ConfigurationKey.FirstOrDefault(x => x.Key == NumberOfConnectorsKey);
        if (numberOfConnectors?.Value == null)
        {
            logger.LogError("Could not get number of connectors for charge point {chargePointId}. Value was null", chargepointId);
            return new(null, "Value was null", null);
        }
        if(int.TryParse(numberOfConnectors.Value, out var integerNumberOfConnectors))
        {
            return new(integerNumberOfConnectors, null, null);
        }
        return new(null, $"Could not parse number of connectors: {numberOfConnectors.Value}", null);
    }

    public async Task<Result<bool?>> IsReconfigurationRequired(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(IsReconfigurationRequired), chargepointId);
        var getConfigurationResponse = await GetOcppConfigurations(chargepointId, cancellationToken);
        if (getConfigurationResponse.HasError)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Error message: {errorMessage}", chargepointId, getConfigurationResponse.ErrorMessage);
            return new(null, getConfigurationResponse.ErrorMessage, null);
        }
        var result = getConfigurationResponse.Data;
        if (result?.ConfigurationKey == null)
        {
            logger.LogError("Could not get configuration keys for charge point {chargePointId}. Response was null", chargepointId);
            return new(null, "Response was null", null);
        }
        var meterValueSampleInterval = result.ConfigurationKey.FirstOrDefault(x => x.Key == MeterValueSampleIntervalKey);
        if (meterValueSampleInterval?.Value != MeterValuesSampleIntervalDefaultValue.ToString())
        {
            return new Result<bool?>(true, null, null);
        }
        var meterValuesSampledData = result.ConfigurationKey.FirstOrDefault(x => x.Key == MeterValuesSampledDataKey);
        if (meterValuesSampledData?.Value != null)
        {
            var meterValuesSampledDataList = meterValuesSampledData.Value.Split(',').ToHashSet();
            if (!meterValuesSampledDataList.SetEquals(_meterValuesSampledDataDefaultValue))
            {
                return new Result<bool?>(true, null, null);
            }
        }
        return new Result<bool?>(false, null, null);
    }

    public async Task<Result<GetConfigurationResponse>> GetOcppConfigurations(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(GetOcppConfigurations), chargepointId);

        var getConfigurationRequest = new GetConfigurationRequest()
        {
            Key = new List<string>()
            {
                MeterValueSampleIntervalKey,
                MeterValuesSampledDataKey,
                NumberOfConnectorsKey,
                UnlockConnectorOnEvSideDisconnectKey,
                ConnectorSwitch3To1PhaseSupportedKey,
            },
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<GetConfigurationResponse>(chargepointId,
                "GetConfiguration",
                getConfigurationRequest,
                cancellationToken);
            return new(ocppResponse, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargepointId);
            return new(null, ex.Message, null);
        }
    }

    public async Task<Result<ChangeConfigurationResponse>> SetMeterValuesSampledDataConfiguration(string chargePointId,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({charegPointId})", nameof(SetMeterValuesSampledDataConfiguration), chargePointId);
        var response = await SetOcppConfiguration(chargePointId, MeterValuesSampledDataKey, "Power.Active.Import,Current.Import,Voltage", cancellationToken);
        return response;
    }

    public async Task<Result<ChangeConfigurationResponse>> SetMeterValuesSampleIntervalConfiguration(string chargePointId,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({charegPointId})", nameof(SetMeterValuesSampleIntervalConfiguration), chargePointId);
        var response = await SetOcppConfiguration(chargePointId, MeterValueSampleIntervalKey, "5", cancellationToken);
        return response;
    }


    private async Task<Result<ChangeConfigurationResponse>> SetOcppConfiguration(string chargepointId, string key, string value, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId}, {key}, {value})", nameof(SetOcppConfiguration), chargepointId, key, value);
        var changeConfigurationRequest = new ChangeConfigurationRequest()
        {
            Key = key,
            Value = value,
        };
        try
        {
            var ocppResponse = await ocppWebSocketConnectionHandlingService.SendRequestAsync<ChangeConfigurationResponse>(chargepointId,
                "ChangeConfiguration",
                changeConfigurationRequest,
                cancellationToken);
            if (ocppResponse.Status == ConfigurationStatus.Accepted || ocppResponse.Status == ConfigurationStatus.RebootRequired)
            {
                return new Result<ChangeConfigurationResponse>(ocppResponse, null, null);
            }

            return new Result<ChangeConfigurationResponse>(ocppResponse,
                $"The chargepoint responded with configuration status {ocppResponse.Status}", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send message to charge point {chargePointId} or charge point did not answer properly", chargepointId);
            return new(null, ex.Message, null);
        }
    }
}



/* Options to set for key "MeterValuesSampledData":
   Current.Export Instantaneous current flow from EV
   Current.Import Instantaneous current flow to EV
   Current.Offered Maximum current offered to EV
   Energy.Active.Export.Register Numerical value read from the "active electrical energy" (Wh or kWh) register of the (most authoritative)
   electrical meter measuring energy exported (to the grid).
   Energy.Active.Import.Register Numerical value read from the "active electrical energy" (Wh or kWh) register of the (most authoritative)
   electrical meter measuring energy imported (from the grid supply).
   Energy.Reactive.Export.Register Numerical value read from the "reactive electrical energy" (VARh or kVARh) register of the (most
   authoritative) electrical meter measuring energy exported (to the grid).
   Energy.Reactive.Import.Register Numerical value read from the "reactive electrical energy" (VARh or kVARh) register of the (most
   authoritative) electrical meter measuring energy imported (from the grid supply).
   Energy.Active.Export.Interval Absolute amount of "active electrical energy" (Wh or kWh) exported (to the grid) during an associated time
   "interval", specified by a Metervalues ReadingContext, and applicable interval duration configuration values
   (in seconds) for "ClockAlignedDataInterval" and "MeterValueSampleInterval".
   Energy.Active.Import.Interval Absolute amount of "active electrical energy" (Wh or kWh) imported (from the grid supply) during an
   associated time "interval", specified by a Metervalues ReadingContext, and applicable interval duration
   configuration values (in seconds) for "ClockAlignedDataInterval" and "MeterValueSampleInterval".
   Energy.Reactive.Export.Interval Absolute amount of "reactive electrical energy" (VARh or kVARh) exported (to the grid) during an associated
   time "interval", specified by a Metervalues ReadingContext, and applicable interval duration configuration
   values (in seconds) for "ClockAlignedDataInterval" and "MeterValueSampleInterval".
   Energy.Reactive.Import.Interval Absolute amount of "reactive electrical energy" (VARh or kVARh) imported (from the grid supply) during an
   associated time "interval", specified by a Metervalues ReadingContext, and applicable interval duration
   configuration values (in seconds) for "ClockAlignedDataInterval" and "MeterValueSampleInterval".
   Frequency Instantaneous reading of powerline frequency. NOTE: OCPP 1.6 does not have a UnitOfMeasure for
   frequency, the UnitOfMeasure for any SampledValue with measurand: Frequency is Hertz.
   Power.Active.Export Instantaneous active power exported by EV. (W or kW)
   Power.Active.Import Instantaneous active power imported by EV. (W or kW)
   Power.Factor Instantaneous power factor of total energy flow
   Power.Offered Maximum power offered to EV
   Power.Reactive.Export Instantaneous reactive power exported by EV. (var or kvar)
   Power.Reactive.Import Instantaneous reactive power imported by EV. (var or kvar)
   RPM Fan speed in RPM
   SoC State of charge of charging vehicle in percentage
   Temperature Temperature reading inside Charge Point.
   Voltage Instantaneous AC RMS supply voltage
 */
