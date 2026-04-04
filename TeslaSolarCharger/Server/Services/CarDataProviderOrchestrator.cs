using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class CarDataProviderOrchestrator(
    ILogger<CarDataProviderOrchestrator> logger,
    IEnumerable<ICarDataProvider> carDataProviders)
    : ICarDataProviderOrchestrator
{
    public async Task RefreshAllCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshAllCarData));
        foreach (var provider in carDataProviders)
        {
            logger.LogDebug("Refreshing car data using {providerType} for {carType}", provider.GetType().Name, provider.SupportedCarType);
            await provider.RefreshCarData().ConfigureAwait(false);
        }
    }
}
