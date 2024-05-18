using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TeslaMateChargeCostUpdateService (ILogger<TeslaMateChargeCostUpdateService> logger,
    ITeslamateContext teslamateContext,
    ITeslaSolarChargerContext teslaSolarChargerContext) : ITeslaMateChargeCostUpdateService
{
    public async Task UpdateTeslaMateChargeCosts()
    {
        logger.LogTrace("{method}()", nameof(UpdateTeslaMateChargeCosts));
        var teslaMateCars = await teslaSolarChargerContext.Cars
            .Select(c => new
            {
                c.Id,
                c.TeslaMateCarId,
            })
            .Where(c => c.TeslaMateCarId != null)
            .ToListAsync();
        foreach (var teslaMateCar in teslaMateCars)
        {
            var teslaMateChargingProcesses = teslamateContext.ChargingProcesses
                .Where(cp => cp.CarId == teslaMateCar.TeslaMateCarId)
                .OrderByDescending(cp => cp.StartDate)
                .ToList();
            var teslaSolarChargerChargingProcesses = teslaSolarChargerContext.ChargingProcesses
                .Where(cp => cp.CarId == teslaMateCar.Id)
                .OrderByDescending(cp => cp.StartDate)
                .ToList();
            foreach (var teslaMateChargingProcess in teslaMateChargingProcesses)
            {
                var overlappingTeslaSolarChargerProcesses = teslaSolarChargerChargingProcesses
                    .Where(tscp => tscp.StartDate < teslaMateChargingProcess.EndDate && tscp.EndDate > teslaMateChargingProcess.StartDate)
                    .ToList();
                if (overlappingTeslaSolarChargerProcesses.Count == 0)
                {
                    continue;
                }
                var cost = 0m;
                foreach (var overlappingTeslaSolarChargerProcess in overlappingTeslaSolarChargerProcesses)
                {
                    var overlapDuration = GetOverlapDuration(overlappingTeslaSolarChargerProcess, teslaMateChargingProcess);
                    var tscChargingProcessDuration = overlappingTeslaSolarChargerProcess.EndDate - overlappingTeslaSolarChargerProcess.StartDate;
                    if (overlapDuration == default
                        || tscChargingProcessDuration == default
                        || tscChargingProcessDuration == TimeSpan.Zero
                        || overlappingTeslaSolarChargerProcess.Cost == default)
                    {
                        continue;
                    }
                    cost += (decimal)(overlapDuration.Value.TotalSeconds / tscChargingProcessDuration.Value.TotalSeconds) * overlappingTeslaSolarChargerProcess.Cost.Value;
                }
                teslaMateChargingProcess.Cost = cost;
            }
        }
        await teslamateContext.SaveChangesAsync();
    }

    private static TimeSpan? GetOverlapDuration(Model.Entities.TeslaSolarCharger.ChargingProcess tscProcess, Model.Entities.TeslaMate.ChargingProcess teslaMateProcess)
    {
        var overlapStart = tscProcess.StartDate > teslaMateProcess.StartDate ? tscProcess.StartDate : teslaMateProcess.StartDate;
        var overlapEnd = tscProcess.EndDate < teslaMateProcess.EndDate ? tscProcess.EndDate : teslaMateProcess.EndDate;

        if (overlapStart < overlapEnd)
        {
            return overlapEnd - overlapStart;
        }

        return TimeSpan.Zero;
    }
}
