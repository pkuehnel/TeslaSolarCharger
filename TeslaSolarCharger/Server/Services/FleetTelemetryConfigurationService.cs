using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryConfigurationService(ILogger<FleetTelemetryConfigurationService> logger,
    IBackendApiService backendApiService,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITscConfigurationService tscConfigurationService,
    ITeslaFleetApiService teslaFleetApiService,
    IConstants constants) : IFleetTelemetryConfigurationService
{
    public async Task<DtoGetFleetTelemetryConfiguration> GetFleetTelemetryConfiguration(string vin)
    {
        logger.LogTrace("{method}({vin})", nameof(GetFleetTelemetryConfiguration), vin);
        var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
        var fleetApiProxyRequired = await teslaFleetApiService.IsFleetApiProxyEnabled(vin).ConfigureAwait(false);
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            throw new InvalidOperationException("Decryption key not found do not send command");
        }
        var result = await backendApiService.SendRequestToBackend<DtoGetFleetTelemetryConfiguration>(HttpMethod.Get, token.AccessToken,
            $"FleetTelemetryConfiguration/GetFleetTelemetryConfiguration?encryptionKey={Uri.EscapeDataString(decryptionKey)}&vin={vin}&carRequiresProxy={fleetApiProxyRequired.Value}", null);
        if (result.HasError)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }

        if (result.Data == default)
        {
            throw new InvalidOperationException("No data returned from backend");
        }
        return result.Data;
    }

    public async Task<DtoFleetTelemetryConfigurationResult> SetFleetTelemetryConfiguration(string vin, bool forceReconfiguration)
    {
        logger.LogTrace("{method}({vin}, {forceReconfiguration})", nameof(SetFleetTelemetryConfiguration), vin, forceReconfiguration);
        var carSettings = teslaSolarChargerContext.Cars
            .Where(c => c.Vin == vin)
            .Select(c =>  new
            {
                c.IncludeTrackingRelevantFields,
                c.IsFleetTelemetryHardwareIncompatible,
            })
            .FirstOrDefault();
        if (carSettings == default)
        {
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = "Car not found in local TSC database",
            };
        }

        if (carSettings.IsFleetTelemetryHardwareIncompatible)
        {
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = "Car hardware is not compatible with fleet telemetry",
                ConfigurationErrorType = TeslaFleetTelemetryConfigurationErrorType.UnsupportedHardware,
            };
        }

        var token = await teslaSolarChargerContext.BackendTokens.SingleAsync();
        //Fleet API Proxy is required to set fleet Telemetry configuration
        var fleetApiProxyRequired = true;
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            throw new InvalidOperationException("Decryption key not found do not send command");
        }
        var result = await backendApiService.SendRequestToBackend<DtoFleetTelemetryConfigurationResult>(HttpMethod.Post, token.AccessToken,
            $"FleetTelemetryConfiguration/SetFleetTelemetryConfiguration?encryptionKey={Uri.EscapeDataString(decryptionKey)}&vin={vin}&carRequiresProxy={fleetApiProxyRequired}&includeTrackingRelevantFields={carSettings.IncludeTrackingRelevantFields}&forceReconfiguration={forceReconfiguration}", null);
        if (result.HasError)
        {
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
            };
        }
        var cloudData = result.Data;
        if (cloudData == default)
        {
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = "No data returned from backend",
            };
        }

        if (cloudData.ConfigurationErrorType != default)
        {
            logger.LogError("Error setting fleet telemetry configuration: {errorType}", cloudData.ConfigurationErrorType);
            var car = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Vin == vin);
            switch (cloudData.ConfigurationErrorType)
            {
                case TeslaFleetTelemetryConfigurationErrorType.MissingKey:
                    car.TeslaFleetApiState = TeslaCarFleetApiState.NotWorking;
                    break;
                case TeslaFleetTelemetryConfigurationErrorType.UnsupportedFirmware:
                    logger.LogWarning("Disable Fleet Telemetry for car {vin} as firmware is not supported", vin);
                    car.UseFleetTelemetry = false;
                    car.IncludeTrackingRelevantFields = false;
                    break;
                case TeslaFleetTelemetryConfigurationErrorType.UnsupportedHardware:
                    logger.LogWarning("Disable Fleet Telemetry for car {vin} as hardware is not supported", vin);
                    car.IsFleetTelemetryHardwareIncompatible = true;
                    car.UseFleetTelemetry = false;
                    car.IncludeTrackingRelevantFields = false;
                    break;
                case TeslaFleetTelemetryConfigurationErrorType.MaxConfigs:
                    logger.LogWarning("Disable Fleet Telemetry for car {vin} as max configurations reached", vin);
                    car.UseFleetTelemetry = false;
                    car.IncludeTrackingRelevantFields = false;
                    break;
                case null:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            await teslaSolarChargerContext.SaveChangesAsync();
        }
        return cloudData;
    }
}
