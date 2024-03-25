using TeslaSolarCharger.Server.Services.GridPrice.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice.Contracts;

public interface IPriceDataService
{
    Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString);
}
