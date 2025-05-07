using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class OcppChargePointConfigurationService(ILogger<OcppChargePointConfigurationService> logger,
    IOcppWebSocketConnectionHandlingService ocppWebSocketConnectionHandlingService) : IOcppChargePointConfigurationService
{
    public async Task<Result<GetConfigurationResponse>> GetOcppConfigurations(string chargepointId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({chargepointId})", nameof(GetOcppConfigurations), chargepointId);

        var getConfigurationRequest = new GetConfigurationRequest()
        {
            Key = new List<string>()
            {
                "MeterValueSampleInterval",
                "MeterValuesSampledData",
                "NumberOfConnectors",
                "UnlockConnectorOnEVSideDisconnect",
                "ConnectorSwitch3to1PhaseSupported",
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
        var response = await SetOcppConfiguration(chargePointId, "MeterValuesSampledData", "Power.Active.Import,Current.Import,Voltage", cancellationToken);
        return response;
    }

    public async Task<Result<ChangeConfigurationResponse>> SetMeterValuesSampleIntervalConfiguration(string chargePointId,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({charegPointId})", nameof(SetMeterValuesSampleIntervalConfiguration), chargePointId);
        var response = await SetOcppConfiguration(chargePointId, "MeterValueSampleInterval", "5", cancellationToken);
        return response;
    }


    public async Task<Result<ChangeConfigurationResponse>> SetOcppConfiguration(string chargepointId, string key, string value, CancellationToken cancellationToken)
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
            return new Result<ChangeConfigurationResponse>(ocppResponse, null, null);
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
