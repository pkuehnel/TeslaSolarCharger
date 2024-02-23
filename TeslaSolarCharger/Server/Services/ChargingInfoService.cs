using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class ChargingInfoService (ILogger<ChargingInfoService> logger, ITeslaSolarChargerContext context)
{
    public async Task SetNewChargingValues()
    {
        var cars = await context.Cars.ToListAsync().ConfigureAwait(false);
    }
}
