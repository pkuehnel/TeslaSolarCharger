using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueImportService : IMeterValueImportService
{
    private readonly ILogger<MeterValueImportService> _logger;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly IServiceProvider _serviceProvider;

    private const string CarMeterValuesImportedKey = "CarMeterValuesImported";


    public MeterValueImportService(ILogger<MeterValueImportService> logger,
        ITscConfigurationService tscConfigurationService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _tscConfigurationService = tscConfigurationService;
        _serviceProvider = serviceProvider;
    }

    public async Task ImportMeterValuesFromChargingDetailsAsync()
    {
        _logger.LogTrace("{method}()", nameof(ImportMeterValuesFromChargingDetailsAsync));
        var valuesAlreadyImported = await _tscConfigurationService.GetConfigurationValueByKey(CarMeterValuesImportedKey).ConfigureAwait(false);
        const string alreadyUpdatedValue = "true";
        if (string.Equals(valuesAlreadyImported, alreadyUpdatedValue))
        {
            _logger.LogDebug("Charging Details Meter values already imported, skipping.");
            return;
        }

        await DeleteAllAlreadyImportedMeterValues().ConfigureAwait(false);
        await SetAllNullChargingProcessEndDates().ConfigureAwait(false);
        //Execute to reduce potential overlaps
        await UpdateStartAndStopTimesForAllChargingProcesses().ConfigureAwait(false);
        await CutOffChargingProcessesLongerThan24Hours().ConfigureAwait(false);
        await UpdateStartAndStopTimesForAllChargingProcesses().ConfigureAwait(false);
        await ForceChargingProcessesNotToOverlap().ConfigureAwait(false);
        await DeleteChargingDetailsOutsideBoundries().ConfigureAwait(false);
        //Reexecute to ensure that the start and stop times are correct after the overlaps are resolved
        await UpdateStartAndStopTimesForAllChargingProcesses().ConfigureAwait(false);


        using var methodWideScope = _serviceProvider.CreateScope();
        var methodWideContext = methodWideScope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var chargingProcesses = await methodWideContext.ChargingProcesses
            .AsNoTracking()
            .ToListAsync();

        chargingProcesses = chargingProcesses.OrderBy(cp => cp.StartDate).ToList();

        var latestCarMeterValues = new Dictionary<int, MeterValue>();
        var latestChargingStationMeterValues = new Dictionary<int, MeterValue>();
        foreach (var chargingProcess in chargingProcesses)
        {
            _logger.LogInformation("Convert meter data for: {chargingProcessID} ({chargingProcessStartDate})", chargingProcess.Id, chargingProcess.StartDate);
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var meterValueEstimationService = scope.ServiceProvider.GetRequiredService<IMeterValueEstimationService>();
            var tscOnlyChargingCostService = scope.ServiceProvider.GetRequiredService<ITscOnlyChargingCostService>();
            var chargingDetails = await context.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .AsNoTracking()
                .ToListAsync();
            //Do not log if only one charging detail is found as creates issues with adding 0 power dummies
            if (chargingDetails.Count < 2)
            {
                continue;
            }
            chargingDetails = chargingDetails.OrderBy(cd => cd.TimeStamp).ToList();
            var carMeterValuesToSave = new List<MeterValue>();
            var chargingStationMeterValuesToSave = new List<MeterValue>();
            var index = 0;
            foreach (var chargingDetail in chargingDetails)
            {
                if (chargingProcess.CarId != default)
                {
                    var meterValue = GenerateMeterValueFromChargingDetail(chargingDetail, MeterValueKind.Car);
                    if (index == 0)
                    {
                        var dummyMeterValue = tscOnlyChargingCostService.GenerateDefaultMeterValue(chargingProcess.CarId, null, meterValue.Timestamp);
                        carMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    }
                    meterValue.CarId = chargingProcess.CarId;
                    carMeterValuesToSave.Add(meterValue);

                    if ((index != 0) && (index == chargingDetails.Count - 1))
                    {
                        var dummyMeterValue = tscOnlyChargingCostService.GenerateDefaultMeterValue(chargingProcess.CarId, null, meterValue.Timestamp);
                        carMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(-1);
                    }

                }
                if (chargingProcess.OcppChargingStationConnectorId != default)
                {
                    var meterValue = GenerateMeterValueFromChargingDetail(chargingDetail, MeterValueKind.ChargingConnector);
                    if (index == 0)
                    {
                        var dummyMeterValue = tscOnlyChargingCostService.GenerateDefaultMeterValue(null, chargingProcess.OcppChargingStationConnectorId, meterValue.Timestamp);
                        chargingStationMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    }
                    meterValue.ChargingConnectorId = chargingProcess.OcppChargingStationConnectorId;
                    chargingStationMeterValuesToSave.Add(meterValue);

                    if ((index != 0) && (index == chargingDetails.Count - 1))
                    {
                        var dummyMeterValue = tscOnlyChargingCostService.GenerateDefaultMeterValue(null, chargingProcess.OcppChargingStationConnectorId, meterValue.Timestamp);
                        chargingStationMeterValuesToSave.Add(dummyMeterValue);
                        meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(-1);
                    }

                }

                index++;
            }
            foreach (var meterValue in carMeterValuesToSave)
            {
                latestCarMeterValues[chargingProcess.CarId!.Value] =
                    await meterValueEstimationService.UpdateMeterValueEstimation(meterValue, latestCarMeterValues.GetValueOrDefault(chargingProcess.CarId!.Value));
            }
            foreach (var meterValue in chargingStationMeterValuesToSave)
            {
                latestChargingStationMeterValues[chargingProcess.OcppChargingStationConnectorId!.Value] =
                    await meterValueEstimationService.UpdateMeterValueEstimation(meterValue, latestChargingStationMeterValues.GetValueOrDefault(chargingProcess.OcppChargingStationConnectorId!.Value));
            }
            context.MeterValues.AddRange(carMeterValuesToSave);
            context.MeterValues.AddRange(chargingStationMeterValuesToSave);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        await _tscConfigurationService.SetConfigurationValueByKey(CarMeterValuesImportedKey, alreadyUpdatedValue).ConfigureAwait(false);
    }

    private async Task CutOffChargingProcessesLongerThan24Hours()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var chargingProcesses = await context.ChargingProcesses
            .ToListAsync().ConfigureAwait(false);
        chargingProcesses = chargingProcesses.Where(cp => cp.EndDate != null && cp.EndDate.Value - cp.StartDate > TimeSpan.FromHours(24)).ToList();
        foreach (var chargingProcess in chargingProcesses)
        {
            using var innerScope = _serviceProvider.CreateScope();
            var innerContext = innerScope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var chargingDetails = await innerContext.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .OrderBy(cd => cd.TimeStamp)
                .ToListAsync().ConfigureAwait(false);
            for (var i = 1; i < chargingDetails.Count; i++)
            {
                var currentDetail = chargingDetails[i];
                var previousDetail = chargingDetails[i - 1];
                if (currentDetail.TimeStamp - previousDetail.TimeStamp > TimeSpan.FromMinutes(5))
                {
                    innerContext.ChargingDetails.RemoveRange(chargingDetails.Skip(i));
                    break;
                }
            }
            await innerContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private async Task SetAllNullChargingProcessEndDates()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var nullEndChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate == null)
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingProcess in nullEndChargingProcesses)
        {
            var lastChargingDetail = await context.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .OrderByDescending(cd => cd.TimeStamp)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (lastChargingDetail == default)
            {
                context.ChargingProcesses.Remove(chargingProcess);
            }
            else
            {
                chargingProcess.EndDate = lastChargingDetail.TimeStamp;
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task ForceChargingProcessesNotToOverlap()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        // Process overlaps for cars
        var carsWithProcesses = await context.ChargingProcesses
            .Where(cp => cp.CarId != null)
            .Select(cp => cp.CarId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var carId in carsWithProcesses)
        {
            await ResolveOverlapsForCarAsync(carId, context);
        }

        // Process overlaps for charging connectors
        var connectorsWithProcesses = await context.ChargingProcesses
            .Where(cp => cp.OcppChargingStationConnectorId != null)
            .Select(cp => cp.OcppChargingStationConnectorId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var connectorId in connectorsWithProcesses)
        {
            await ResolveOverlapsForConnectorAsync(connectorId, context);
        }
    }

    private async Task ResolveOverlapsForCarAsync(int carId, ITeslaSolarChargerContext outerContext)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var processes = await context.ChargingProcesses
            .Where(cp => cp.CarId == carId)
            .OrderBy(cp => cp.StartDate)
            .ToListAsync();

        await ResolveOverlapsInProcessListAsync(processes, outerContext);
    }


    private async Task ResolveOverlapsForConnectorAsync(int connectorId, ITeslaSolarChargerContext outerContext)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var processes = await context.ChargingProcesses
            .Where(cp => cp.OcppChargingStationConnectorId == connectorId)
            .OrderBy(cp => cp.StartDate)
            .ToListAsync();

        await ResolveOverlapsInProcessListAsync(processes, outerContext);
    }

    private async Task ResolveOverlapsInProcessListAsync(List<ChargingProcess> processes, ITeslaSolarChargerContext outerContext)
    {
        for (var i = 0; i < processes.Count - 1; i++)
        {
            var current = processes[i];
            var next = processes[i + 1];

            var currentEnd = current.EndDate ?? DateTime.MaxValue;

            // Check if there's an overlap
            if (currentEnd > next.StartDate)
            {
                // Find the optimal cut point
                var cutPoint = await FindOptimalCutPointAsync(current, next);

                // Apply the cut
                await ApplyCutAsync(current, next, cutPoint, outerContext);
            }
        }
    }

    private async Task<DateTime> FindOptimalCutPointAsync(ChargingProcess earlier, ChargingProcess later)
    {
        var overlapStart = later.StartDate;
        var overlapEnd = earlier.EndDate ?? DateTime.MaxValue;
        // If the overlap is small, use a simple midpoint
        if ((overlapEnd - overlapStart).TotalMinutes < 10)
        {
            return overlapStart.AddTicks((overlapEnd - overlapStart).Ticks / 2);
        }
        var bestCutPoint = await FindBestCutPointByDetailsAsync(earlier.Id, later.Id, overlapStart, overlapEnd);
        return bestCutPoint;
    }

    private async Task<DateTime> FindBestCutPointByDetailsAsync(
        int earlierProcessId,
        int laterProcessId,
        DateTime overlapStart,
        DateTime overlapEnd)
    {
        // Sample potential cut points (every 5 minutes in the overlap period)
        var samplePoints = new List<DateTime>();
        var current = overlapStart;
        var interval = TimeSpan.FromMinutes(5);

        while (current < overlapEnd)
        {
            samplePoints.Add(current);
            current = current.Add(interval);
        }

        if (samplePoints.Count == 0 || !samplePoints.Contains(overlapEnd))
        {
            samplePoints.Add(overlapEnd);
        }

        // Evaluate each cut point
        var bestCutPoint = overlapStart;
        var minOrphanedDetails = int.MaxValue;

        foreach (var cutPoint in samplePoints)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            // Count details that would be orphaned for earlier process (after the cut)
            var earlierOrphaned = await context.ChargingDetails
                .CountAsync(cd => cd.ChargingProcessId == earlierProcessId
                                  && cd.TimeStamp >= cutPoint);

            // Count details that would be orphaned for later process (before the cut)
            var laterOrphaned = await context.ChargingDetails
                .CountAsync(cd => cd.ChargingProcessId == laterProcessId
                                  && cd.TimeStamp < cutPoint);

            var totalOrphaned = earlierOrphaned + laterOrphaned;

            if (totalOrphaned < minOrphanedDetails)
            {
                minOrphanedDetails = totalOrphaned;
                bestCutPoint = cutPoint;
            }
        }

        return bestCutPoint;
    }

    private async Task ApplyCutAsync(ChargingProcess earlier, ChargingProcess later, DateTime cutPoint,
        ITeslaSolarChargerContext outerContext)
    {
        earlier.EndDate = cutPoint;
        later.StartDate = cutPoint;
        await outerContext.SaveChangesAsync();
    }

    private async Task DeleteChargingDetailsOutsideBoundries()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();

        var processBoundaries = await context.ChargingProcesses
            .Select(cp => new { cp.Id, cp.StartDate, cp.EndDate })
            .ToListAsync().ConfigureAwait(false);

        foreach (var process in processBoundaries)
        {
            await context.ChargingDetails
                .Where(c => c.ChargingProcessId == process.Id
                            && (c.TimeStamp < process.StartDate || c.TimeStamp > process.EndDate))
                .ExecuteDeleteAsync().ConfigureAwait(false);
        }
    }

    private async Task UpdateStartAndStopTimesForAllChargingProcesses()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var chargingProcesses = await context.ChargingProcesses
            .ToListAsync().ConfigureAwait(false);
        foreach (var chargingProcess in chargingProcesses)
        {
            using var innerScope = _serviceProvider.CreateScope();
            var innerContext = innerScope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var query = innerContext.ChargingDetails
                .Where(cd => cd.ChargingProcessId == chargingProcess.Id)
                .OrderBy(cd => cd.TimeStamp)
                .AsQueryable();
            var firstChargingDetail = await query.FirstOrDefaultAsync().ConfigureAwait(false);
            if (firstChargingDetail != default && chargingProcess.StartDate != firstChargingDetail.TimeStamp)
            {
                chargingProcess.StartDate = firstChargingDetail.TimeStamp;
            }
            var lastChargingDetail = await query.LastOrDefaultAsync().ConfigureAwait(false);
            if (lastChargingDetail != default && chargingProcess.EndDate != lastChargingDetail.TimeStamp)
            {
                chargingProcess.EndDate = lastChargingDetail.TimeStamp;
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task DeleteAllAlreadyImportedMeterValues()
    {
        _logger.LogTrace("{method}()", nameof(DeleteAllAlreadyImportedMeterValues));
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        await context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.Car
                             || mv.MeterValueKind == MeterValueKind.ChargingConnector)
            .ExecuteDeleteAsync().ConfigureAwait(false);
    }


    private MeterValue GenerateMeterValueFromChargingDetail(ChargingDetail chargingDetail, MeterValueKind meterValueKind)
    {
        return new MeterValue(new DateTimeOffset(chargingDetail.TimeStamp, TimeSpan.Zero),
            meterValueKind,
            chargingDetail.SolarPower + chargingDetail.HomeBatteryPower + chargingDetail.GridPower)
        {
            MeasuredHomeBatteryPower = chargingDetail.HomeBatteryPower,
            MeasuredGridPower = chargingDetail.GridPower,
        };
    }
}
