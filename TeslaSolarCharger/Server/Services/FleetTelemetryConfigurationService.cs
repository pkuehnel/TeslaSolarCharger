using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class FleetTelemetryConfigurationService(ILogger<FleetTelemetryConfigurationService> logger,
    IBackendApiService backendApiService,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITscConfigurationService tscConfigurationService,
    ITeslaFleetApiService teslaFleetApiService,
    IConstants constants,
    IMemoryCache memoryCache,
    IDateTimeProvider dateTimeProvider) : IFleetTelemetryConfigurationService
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
        var cars = await teslaSolarChargerContext.Cars
            .Where(c => c.UseFleetTelemetry
                        && (c.ShouldBeManaged == true)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotWorking)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.OpenedLinkButNotTested)
                        && (c.TeslaFleetApiState != TeslaCarFleetApiState.NotConfigured)
                        && (c.IsFleetTelemetryHardwareIncompatible == false))
            .Select(c => new { c.Vin, IncludeTrackingRelevantFields = c.IncludeTrackingRelevantFields, })
            .ToListAsync();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        foreach (var car in cars)
        {
            if (car.Vin == default)
            {
                continue;
            }
            var reconfigurationRequired =
                !memoryCache.TryGetValue(constants.FleetTelemetryConfigurationExpiryKey + car.Vin, out DateTimeOffset expiryTime);
            if(!reconfigurationRequired)
            {
                reconfigurationRequired = expiryTime < currentDate.AddHours(constants.FleetTelemetryReconfigurationBufferHours);
            }
            if (!reconfigurationRequired)
            {
                logger.LogDebug("Fleet Telemetry reconfiguration for car {vin} not required as expires in the future", car.Vin);
                continue;
            }
            var result = await SetFleetTelemetryConfiguration(car.Vin, false);
            if (result.Success && (result.ValidUntil != default))
            {
                memoryCache.Set(constants.FleetTelemetryConfigurationExpiryKey + car.Vin, DateTimeOffset.FromUnixTimeSeconds(result.ValidUntil.Value));
            }
        }
    }
}
