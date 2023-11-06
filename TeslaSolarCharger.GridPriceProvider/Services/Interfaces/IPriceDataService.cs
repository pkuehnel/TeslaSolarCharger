using TeslaSolarCharger.GridPriceProvider.Data;

namespace TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

public interface IPriceDataService
{
    Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString);
}
