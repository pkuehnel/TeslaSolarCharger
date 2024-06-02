namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISpotPriceService
{
    Task UpdateSpotPrices();
    Task<DateTimeOffset> LatestKnownSpotPriceTime();
    Task GetSpotPricesSinceFirstChargeDetail();
}
