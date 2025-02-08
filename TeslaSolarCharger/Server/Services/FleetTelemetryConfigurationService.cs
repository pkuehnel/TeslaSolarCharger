using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Dtos.FleetTelemetry;
using TeslaSolarCharger.Server.Services.Contracts;
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
}
