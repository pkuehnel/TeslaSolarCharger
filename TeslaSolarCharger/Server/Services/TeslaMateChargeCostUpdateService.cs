using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TeslaMateChargeCostUpdateService (ILogger<TeslaMateChargeCostUpdateService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaMateDbContextWrapper teslaMateDbContextWrapper) : ITeslaMateChargeCostUpdateService
{
    public async Task UpdateTeslaMateChargeCosts()
    {
        logger.LogTrace("{method}()", nameof(UpdateTeslaMateChargeCosts));
        var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();

        if (!configurationWrapper.UseTeslaMateIntegration() || teslaMateContext == default)
        {
            return;
        }
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
            var teslaMateChargingProcesses = teslaMateContext.ChargingProcesses
                .Where(cp => cp.CarId == teslaMateCar.TeslaMateCarId)
                .OrderByDescending(cp => cp.StartDate)
                .ToList();
            var teslaSolarChargerChargingProcesses = teslaSolarChargerContext.ChargingProcesses
                .Where(cp => cp.CarId == teslaMateCar.Id)
                .OrderByDescending(cp => cp.StartDate)
                .ToList();
            logger.LogDebug("Update TeslaMate charge costs for car {carId} with TeslaMateCarId {teslaMateCarId}", teslaMateCar.Id, teslaMateCar.TeslaMateCarId);
            foreach (var teslaMateChargingProcess in teslaMateChargingProcesses)
            {
                logger.LogDebug("Update TeslaMate charge cost for process {processId}", teslaMateChargingProcess.Id);
                var overlappingTeslaSolarChargerProcesses = teslaSolarChargerChargingProcesses
                    .Where(tscp => tscp.StartDate < teslaMateChargingProcess.EndDate && tscp.EndDate > teslaMateChargingProcess.StartDate)
                    .ToList();
                logger.LogDebug("Found {count} overlapping TeslaSolarCharger charging processes", overlappingTeslaSolarChargerProcesses.Count);
                if (overlappingTeslaSolarChargerProcesses.Count == 0)
                {
                    continue;
                }
                var cost = 0m;
                foreach (var overlappingTeslaSolarChargerProcess in overlappingTeslaSolarChargerProcesses)
                {
                    var overlapDuration = GetOverlapDuration(overlappingTeslaSolarChargerProcess, teslaMateChargingProcess);
                    var tscChargingProcessDuration = overlappingTeslaSolarChargerProcess.EndDate - overlappingTeslaSolarChargerProcess.StartDate;
                    logger.LogDebug("Overlap duration: {overlapDuration}, TSC charging process duration: {tscChargingProcessDuration}, cost: {cost}", overlapDuration, tscChargingProcessDuration, overlappingTeslaSolarChargerProcess.Cost);
                    if (overlapDuration == default
                        || tscChargingProcessDuration == default
                        || tscChargingProcessDuration == TimeSpan.Zero
                        || overlappingTeslaSolarChargerProcess.Cost == default)
                    {
                        continue;
                    }

                    var overlappingCosts = (decimal)(overlapDuration.Value.TotalSeconds / tscChargingProcessDuration.Value.TotalSeconds) * overlappingTeslaSolarChargerProcess.Cost.Value;
                    logger.LogDebug("Add overlapping costs of {overlappingCosts} to teslamate charging process {chargingProcessId} ", overlappingCosts, teslaMateChargingProcess.Id);
                    cost += overlappingCosts;
                }
                teslaMateChargingProcess.Cost = cost;
            }
        }
        await teslaMateContext.SaveChangesAsync();
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
