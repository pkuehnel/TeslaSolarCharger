using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

public class SetupCapabilityProbe(
    ILogger<SetupCapabilityProbe> logger,
    ITokenHelper tokenHelper,
    IBackendApiService backendApiService,
    IGenericValueService genericValueService,
    IOcppChargingStationConfigurationService chargingStationConfigurationService,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext)
    : ISetupCapabilityProbe
{
    public async Task<DtoSetupCapabilities> GetCapabilities()
    {
        logger.LogTrace("{method}()", nameof(GetCapabilities));
        var capabilities = new DtoSetupCapabilities
        {
            BackendTokenState = await SafeGet<TokenState?>(async () => await tokenHelper.GetBackendTokenState(true).ConfigureAwait(false), null, nameof(DtoSetupCapabilities.BackendTokenState)).ConfigureAwait(false),
            FleetApiTokenState = await SafeGet<TokenState?>(async () => await tokenHelper.GetFleetApiTokenState(true).ConfigureAwait(false), null, nameof(DtoSetupCapabilities.FleetApiTokenState)).ConfigureAwait(false),
            UsesTeslaMateAsDataSource = configurationWrapper.UseTeslaMateIntegration() && !configurationWrapper.GetVehicleDataFromTesla(),
        };

        //A licence lookup that fails stays null: reporting it as "not licensed" would send the user off to buy
        //something they may already own.
        var licenseResult = await SafeGet(() => backendApiService.IsBaseAppLicensed(true), null, nameof(DtoSetupCapabilities.IsBaseAppLicensed)).ConfigureAwait(false);
        capabilities.IsBaseAppLicensed = licenseResult?.Data;

        var measuredUsages = GetMeasuredValueUsages();
        capabilities.HasGridPowerSource = measuredUsages.Contains(ValueUsage.GridPower);
        capabilities.HasSolarGenerationSource = measuredUsages.Contains(ValueUsage.InverterPower);
        capabilities.HasHomeBatterySocSource = measuredUsages.Contains(ValueUsage.HomeBatterySoc);
        capabilities.HasHomeBatteryPowerSource = measuredUsages.Contains(ValueUsage.HomeBatteryPower);

        capabilities.KnownChargingStationConnectorIds = await GetKnownConnectorIds().ConfigureAwait(false);
        return capabilities;
    }

    public async Task<DtoSetupCarCapabilities?> GetCarCapabilities(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(GetCarCapabilities), carId);
        var stored = await teslaSolarChargerContext.Cars
            .Where(c => c.Id == carId)
            .Select(c => new
            {
                c.Id,
                c.Vin,
                c.IsFleetTelemetryHardwareIncompatible,
                c.UseFleetTelemetry,
                c.TeslaFleetApiState,
            })
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (stored == null)
        {
            return null;
        }

        var car = new DtoSetupCarCapabilities
        {
            CarId = stored.Id,
            IsFleetTelemetryHardwareIncompatible = stored.IsFleetTelemetryHardwareIncompatible,
            UsesFleetTelemetry = stored.UseFleetTelemetry,
            FleetApiState = stored.TeslaFleetApiState,
        };

        var vin = stored.Vin;
        if (!string.IsNullOrEmpty(vin))
        {
            //A licence lookup that fails stays null: setup must not tell the user to buy something they may own.
            car.IsFleetApiLicensed = await SafeGet<bool?>(
                async () => await backendApiService.IsFleetApiLicensed(vin, true).ConfigureAwait(false),
                null,
                nameof(DtoSetupCarCapabilities.IsFleetApiLicensed)).ConfigureAwait(false);
        }

        return car;
    }

    private HashSet<ValueUsage> GetMeasuredValueUsages()
    {
        try
        {
            return genericValueService.GetSourceValues(false)
                .Select(v => v.UsedFor)
                .ToHashSet();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not determine which solar values are configured.");
            return new HashSet<ValueUsage>();
        }
    }

    private async Task<List<int>> GetKnownConnectorIds()
    {
        var connectorIds = new List<int>();
        try
        {
            var stations = await chargingStationConfigurationService.GetChargingStations().ConfigureAwait(false);
            foreach (var station in stations)
            {
                var connectors = await chargingStationConfigurationService.GetChargingStationConnectors(station.Id).ConfigureAwait(false);
                connectorIds.AddRange(connectors.Select(c => c.Id));
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not list the connected charging stations.");
        }

        return connectorIds;
    }

    private async Task<T?> SafeGet<T>(Func<Task<T>> get, T? fallback, string what)
    {
        try
        {
            return await get().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not detect {what} while preparing setup.", what);
            return fallback;
        }
    }
}
