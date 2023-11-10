using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

namespace TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

public interface IFixedPriceService : IPriceDataService
{
    string GenerateConfigString(List<FixedPrice> prices);
    List<FixedPrice> ParseConfigString(string configString);
}
