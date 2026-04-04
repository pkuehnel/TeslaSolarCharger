using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaCarDataProvider(
    ILogger<TeslaCarDataProvider> logger,
    ITeslaFleetApiService teslaFleetApiService)
    : ICarDataProvider
{
    public CarType SupportedCarType => CarType.Tesla;

    public async Task RefreshCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshCarData));
        await teslaFleetApiService.RefreshCarData().ConfigureAwait(false);
    }
}
