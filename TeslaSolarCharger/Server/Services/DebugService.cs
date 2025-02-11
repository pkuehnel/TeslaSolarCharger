using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Support;

namespace TeslaSolarCharger.Server.Services;

public class DebugService(ILogger<DebugService> logger,
    ITeslaSolarChargerContext context) : IDebugService
{
    public async Task<Dictionary<int, DtoDebugCar>> GetCars()
    {
        logger.LogTrace("{method}", nameof(GetCars));
        var cars = await context.Cars
            .Where(x => x.Vin != null)
            .ToDictionaryAsync(x => x.Id, x => new DtoDebugCar()
            {
                Name = x.Name,
                Vin = x.Vin,
                ShouldBeManaged = x.ShouldBeManaged == true,
                IsAvailableInTeslaAccount = x.IsAvailableInTeslaAccount,
            }).ConfigureAwait(false);
        logger.LogDebug("Found {carCount} cars", cars.Count);
        return cars;
    }
}
