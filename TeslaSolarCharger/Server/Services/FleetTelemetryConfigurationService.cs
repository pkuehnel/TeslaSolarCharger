using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryConfigurationService(ILogger<FleetTelemetryConfigurationService> logger,
    IBackendApiService backendApiService,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITscConfigurationService tscConfigurationService,
    ITeslaFleetApiService teslaFleetApiService,
    IConstants constants,
    IErrorHandlingService errorHandlingService,
    IIssueKeys issueKeys) : IFleetTelemetryConfigurationService
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
        if (!await backendApiService.IsBaseAppLicensed(true))
        {
            logger.LogWarning("Base App is not licensed, do not connect to Fleet Telemetry");
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = "Can not configure Fleet Telemetry as TSC is not licensed",
            };
        }
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
        if (carSettings.IncludeTrackingRelevantFields && (!await backendApiService.IsFleetApiLicensed(vin, true)))
        {
            logger.LogWarning("Car {vin} is not licensed for Fleet API, do not connect as IncludeTrackingRelevant fields is enabled", vin);
            return new DtoFleetTelemetryConfigurationResult
            {
                Success = false,
                ErrorMessage = "Fleet API license required for car {vin} as Include Tracking Relevant Fields is enabled",
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

    public async Task ReconfigureAllCarsIfRequired()
    {
        logger.LogTrace("{method}", nameof(ReconfigureAllCarsIfRequired));
        var vins = await teslaSolarChargerContext.Cars
            .Where(c => c.UseFleetTelemetry
                        && (c.ShouldBeManaged == true)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotWorking)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.OpenedLinkButNotTested)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotConfigured)
                        && (c.IsFleetTelemetryHardwareIncompatible == false))
            .Select(c => c.Vin)
            .ToListAsync();
        foreach (var vin in vins)
        {
            if (vin == default)
            {
                continue;
            }
            var result = await SetFleetTelemetryConfiguration(vin, false);
            if (result.Success)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetTelemetryConfigurationError, vin).ConfigureAwait(false);
            }
            else
            {
                await errorHandlingService.HandleError(nameof(FleetTelemetryConfigurationService), nameof(ReconfigureAllCarsIfRequired),
                    $"Error while configuring Fleet Telemetry for car {vin}", $"{result.ErrorMessage}\r\nNote: The error only disappears after fxing the root cause, restarting TSC and waiting for 2 minutes.", issueKeys.FleetTelemetryConfigurationError, vin, null).ConfigureAwait(false);
            }
        }
    }
}
