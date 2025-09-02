using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingCost;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TscOnlyChargingCostService(ILogger<TscOnlyChargingCostService> logger,
    ITeslaSolarChargerContext context,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IServiceProvider serviceProvider,
    IConstants constants,
    ILoadPointManagementService loadPointManagementService,
    IDatabaseValueBufferService databaseValueBufferService,
    IMeterValueEstimationService meterValueEstimationService,
    IMeterValueLogService meterValueLogService) : ITscOnlyChargingCostService
{
    public async Task FinalizeFinishedChargingProcesses()
    {
        logger.LogTrace("{method}()", nameof(FinalizeFinishedChargingProcesses));
        var openChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate == null)
            .ToListAsync().ConfigureAwait(false);
        var timeSpanToHandleChargingProcessAsCompleted = TimeSpan.FromMinutes(constants.MeterValueDatabaseSaveIntervalMinutes) * 2;
        foreach (var chargingProcess in openChargingProcesses)
        {
            var latestMeterValueQuery = context.MeterValues.AsQueryable();
            if (chargingProcess.OcppChargingStationConnectorId != default)
            {
                latestMeterValueQuery = latestMeterValueQuery.Where(m => m.CarId == null
                                                                         && m.ChargingConnectorId == chargingProcess.OcppChargingStationConnectorId
                                                                         && m.MeterValueKind == MeterValueKind.ChargingConnector);
            }
            else
            {
                latestMeterValueQuery = latestMeterValueQuery.Where(m => m.CarId == chargingProcess.CarId
                                                                         && m.ChargingConnectorId == null
                                                                         && m.MeterValueKind == MeterValueKind.Car);
            }
            var latestMeterValue = await latestMeterValueQuery
                .OrderByDescending(cd => cd.Timestamp)
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (latestMeterValue == default)
            {
                logger.LogWarning("No meter value found for charging process with ID {chargingProcessId}.", chargingProcess.Id);
                continue;
            }

            if (latestMeterValue.Timestamp.Add(timeSpanToHandleChargingProcessAsCompleted) < dateTimeProvider.DateTimeOffSetUtcNow())
            {
                try
                {
                    await FinalizeChargingProcess(chargingProcess);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while finalizing charging process with ID {chargingProcessId}.", chargingProcess.Id);
                }
            }
        }
    }

    public async Task UpdateChargePricesOfAllChargingProcesses()
    {
        logger.LogTrace("{method}()", nameof(UpdateChargePricesOfAllChargingProcesses));
        var finalizedChargingProcesses = await context.ChargingProcesses
            .Where(cp => cp.EndDate != null)
            .ToListAsync().ConfigureAwait(false);
        settings.ChargePricesUpdateProgress = new() { Value = 0, MaxValue = finalizedChargingProcesses.Count, };
        try
        {
            foreach (var chargingProcess in finalizedChargingProcesses)
            {
                logger.LogTrace("Finalizing charging process with ID {chargingProcessId}", chargingProcess.Id);
                settings.ChargePricesUpdateProgress.Value = finalizedChargingProcesses.IndexOf(chargingProcess);
                await FinalizeChargingProcess(chargingProcess);
            }
        }
        finally
        {
            settings.ChargePricesUpdateProgress = null;
        }
    }

    public async Task<Dictionary<int, DtoChargeSummary>> GetChargeSummaries()
    {
        var chargingProcessGroups = (await context.ChargingProcesses
                //do not remove the carId not null filter as otherwise will crash
                .Where(h => h.Cost != null && h.CarId != null)
                .ToListAsync().ConfigureAwait(false))
                //CarId can not be null as is filtered above
                .GroupBy(h => h.CarId!.Value).ToList();
        var chargeSummaries = new Dictionary<int, DtoChargeSummary>();
        foreach (var chargingProcessGroup in chargingProcessGroups)
        {
            var list = chargingProcessGroup.ToList();
            chargeSummaries.Add(chargingProcessGroup.Key, GetChargeSummaryByChargingProcesses(list));
        }

        return chargeSummaries;
    }

    public async Task<DtoChargeSummary> GetChargeSummary(int? carId, int? chargingConnectorId)
    {
        logger.LogTrace("{method}({carId})", nameof(GetChargeSummary), carId);
        var chargingProcessQuery = context.ChargingProcesses
            .AsQueryable();
        if (carId != default)
        {
            chargingProcessQuery = chargingProcessQuery.Where(cp => cp.CarId == carId);
        }
        if (chargingConnectorId != default)
        {
            chargingProcessQuery = chargingProcessQuery.Where(cp => cp.OcppChargingStationConnectorId == chargingConnectorId);
        }

        var chargingProcesses = await chargingProcessQuery.AsNoTracking()
        .ToListAsync().ConfigureAwait(false);
        var chargeSummary = GetChargeSummaryByChargingProcesses(chargingProcesses);
        return chargeSummary;
    }

    public async Task<List<DtoHandledCharge>> GetFinalizedChargingProcesses(int? carId, int? chargingConnectorId, bool hideKnownCars, int minConsumedEnergyWh)
    {
        logger.LogTrace("{method}({carId}, {chargingConnectorId})", nameof(GetFinalizedChargingProcesses), carId, chargingConnectorId);
        var handledChargesQuery = context.ChargingProcesses
            .Where(h => h.Cost != null).AsQueryable();
        if (carId != default || hideKnownCars)
        {
            handledChargesQuery = handledChargesQuery.Where(h => h.CarId == carId);
        }

        if (chargingConnectorId != default)
        {
            handledChargesQuery = handledChargesQuery.Where(h => h.OcppChargingStationConnectorId == chargingConnectorId);
        }
        var handledCharges = await handledChargesQuery
            .OrderByDescending(h => h.StartDate)
            .Select(h => new DtoHandledCharge()
            {
                StartTime = h.StartDate.ToLocalTime(),
                EndTime = h.EndDate.HasValue ? h.EndDate.Value.ToLocalTime() : null,
                CalculatedPrice = h.Cost == null ? 0m : Math.Round(h.Cost.Value, 2),
                UsedGridEnergy = h.UsedGridEnergyKwh == null ? 0m : Math.Round(h.UsedGridEnergyKwh.Value, 2),
                UsedHomeBatteryEnergy = h.UsedHomeBatteryEnergyKwh == null ? 0m : Math.Round(h.UsedHomeBatteryEnergyKwh.Value, 2),
                UsedSolarEnergy = h.UsedSolarEnergyKwh == null ? 0m : Math.Round(h.UsedSolarEnergyKwh.Value, 2),
            })
            .ToListAsync().ConfigureAwait(false);

        handledCharges.RemoveAll(c => (c.UsedGridEnergy + c.UsedSolarEnergy + c.UsedHomeBatteryEnergy) < (minConsumedEnergyWh / 1000m));
        foreach (var dtoHandledCharge in handledCharges)
        {
            var usedEnergy = dtoHandledCharge.UsedGridEnergy + dtoHandledCharge.UsedSolarEnergy + dtoHandledCharge.UsedHomeBatteryEnergy;
            if (usedEnergy > 0)
            {
                dtoHandledCharge.PricePerKwh = Math.Round(dtoHandledCharge.CalculatedPrice / usedEnergy, 3);
            }
        }
        return handledCharges;
    }

    private static DtoChargeSummary GetChargeSummaryByChargingProcesses(List<ChargingProcess> chargingProcesses)
    {
        var chargeSummary = new DtoChargeSummary();
        foreach (var chargingProcess in chargingProcesses)
        {
            chargeSummary.ChargeCost += chargingProcess.Cost ?? 0;
            chargeSummary.ChargedGridEnergy += chargingProcess.UsedGridEnergyKwh ?? 0;
            chargeSummary.ChargedHomeBatteryEnergy += chargingProcess.UsedHomeBatteryEnergyKwh ?? 0;
            chargeSummary.ChargedSolarEnergy += chargingProcess.UsedSolarEnergyKwh ?? 0;
        }

        return chargeSummary;
    }

    private async Task FinalizeChargingProcess(ChargingProcess chargingProcess)
    {
        logger.LogTrace("{method}({chargingProcessId})", nameof(FinalizeChargingProcess), chargingProcess.Id);
        var endDate = chargingProcess.EndDate == default
            ? dateTimeProvider.DateTimeOffSetUtcNow()
            : new(chargingProcess.EndDate.Value, TimeSpan.Zero);
        var startDate = new DateTimeOffset(chargingProcess.StartDate, TimeSpan.Zero);
        using var scope = serviceProvider.CreateScope();
        var localContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var latestMeterValuesQuery = localContext.MeterValues
            .Where(m => m.Timestamp >= startDate
            && m.Timestamp <= endDate).AsQueryable();
        if (chargingProcess.OcppChargingStationConnectorId != default)
        {
            latestMeterValuesQuery = latestMeterValuesQuery.Where(m => m.CarId == null
                                                                     && m.ChargingConnectorId == chargingProcess.OcppChargingStationConnectorId
                                                                     && m.MeterValueKind == MeterValueKind.ChargingConnector);
        }
        else
        {
            latestMeterValuesQuery = latestMeterValuesQuery.Where(m => m.CarId == chargingProcess.CarId
                                                                     && m.ChargingConnectorId == null
                                                                     && m.MeterValueKind == MeterValueKind.Car);
        }
        var meterValues = await latestMeterValuesQuery
            .OrderBy(cd => cd.Timestamp)
            .AsNoTracking()
            .ToListAsync().ConfigureAwait(false);
        if (meterValues.Count == 0)
        {
            logger.LogWarning("No meter values found for charging process with ID {chargingProcessId}. Setting default values.", chargingProcess.Id);
            chargingProcess.UsedSolarEnergyKwh = 0;
            chargingProcess.UsedHomeBatteryEnergyKwh = 0;
            chargingProcess.UsedGridEnergyKwh = 0;
            chargingProcess.Cost = 0;
            await context.SaveChangesAsync().ConfigureAwait(false);
            return;
        }
        var lastMeterValue = meterValues.LastOrDefault();
        if (lastMeterValue != default && lastMeterValue.MeasuredPower != 0)
        {
            var fakeMeterValueTimestamp = lastMeterValue.Timestamp.AddMilliseconds(1);
            var fakeMeterValue = GenerateDefaultMeterValue(chargingProcess.CarId, chargingProcess.OcppChargingStationConnectorId,
                fakeMeterValueTimestamp);
            context.MeterValues.Add(fakeMeterValue);
            meterValues.Add(fakeMeterValue);
        }
        decimal usedSolarEnergyWh = 0;
        decimal usedHomeBatteryEnergyWh = 0;
        decimal usedGridEnergyWh = 0;
        decimal cost = 0;
        chargingProcess.EndDate = meterValues.Last().Timestamp.UtcDateTime;
        var prices = await GetGridPricesInTimeSpan(meterValues.First().Timestamp.UtcDateTime, chargingProcess.EndDate.Value);        //When a charging process is stopped and resumed later, the last charging detail is too old and should not be used because it would use the last value dring the whole time althoug the car was not charging
        var maxChargingDetailsDuration = TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds).Add(TimeSpan.FromSeconds(10));
        for (var index = 1; index < meterValues.Count; index++)
        {
            var price = GetPriceByTimeStamp(prices, meterValues[index].Timestamp.UtcDateTime);
            logger.LogTrace("Price for timestamp {timeStamp}: {@price}", meterValues[index].Timestamp, price);
            var meterValue = meterValues[index];
            var timeSpanSinceLastDetail = meterValue.Timestamp - meterValues[index - 1].Timestamp;

            if (timeSpanSinceLastDetail > maxChargingDetailsDuration)
            {
                logger.LogWarning("Do not use charging detail as last charging detail ist too old");
                continue;
            }
            var usedSolarWhSinceLastChargingDetail = (decimal)((meterValue.MeasuredPower - meterValue.MeasuredHomeBatteryPower - meterValue.MeasuredGridPower) * timeSpanSinceLastDetail.TotalHours);
            usedSolarEnergyWh += usedSolarWhSinceLastChargingDetail;
            var usedHomeBatteryWhSinceLastChargingDetail = (decimal)(meterValue.MeasuredHomeBatteryPower * timeSpanSinceLastDetail.TotalHours);
            usedHomeBatteryEnergyWh += usedHomeBatteryWhSinceLastChargingDetail;
            var usedGridPowerSinceLastChargingDetail = (decimal)(meterValue.MeasuredGridPower * timeSpanSinceLastDetail.TotalHours);
            usedGridEnergyWh += usedGridPowerSinceLastChargingDetail;
            cost += usedGridPowerSinceLastChargingDetail * price.GridPrice;
            cost += usedSolarWhSinceLastChargingDetail * price.SolarPrice;
            cost += usedHomeBatteryWhSinceLastChargingDetail * price.SolarPrice;
        }
        chargingProcess.UsedSolarEnergyKwh = usedSolarEnergyWh / 1000m;
        chargingProcess.UsedHomeBatteryEnergyKwh = usedHomeBatteryEnergyWh / 1000m;
        chargingProcess.UsedGridEnergyKwh = usedGridEnergyWh / 1000m;
        chargingProcess.Cost = cost / 1000m;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private Price GetPriceByTimeStamp(List<Price> prices, DateTime timeStamp)
    {
        return prices.First(p => p.ValidFrom <= timeStamp && p.ValidTo > timeStamp);
    }

    public async Task<List<Price>> GetPricesInTimeSpan(DateTimeOffset from, DateTimeOffset to)
    {
        logger.LogTrace("{method}({from}, {to})", nameof(GetGridPricesInTimeSpan), from, to);
        var prices = await GetGridPricesInTimeSpan(from.ToUniversalTime().DateTime, to.ToUniversalTime().DateTime).ConfigureAwait(false);
        return prices;
    }

    /// <summary>
    /// Gets prices in a given time span.
    /// </summary>
    /// <param name="from">DateTime in UTC format</param>
    /// <param name="to">DateTime in UTC format</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    private async Task<List<Price>> GetGridPricesInTimeSpan(DateTime from, DateTime to)
    {
        logger.LogTrace("{method}({from}, {to})", nameof(GetGridPricesInTimeSpan), from, to);
        var chargePrice = await context.ChargePrices
            .Where(c => c.ValidSince < from)
            .OrderByDescending(c => c.ValidSince)
            .FirstAsync();
        var fromDateTimeOffset = new DateTimeOffset(from, TimeSpan.Zero);
        var toDateTimeOffset = new DateTimeOffset(to.AddMilliseconds(1), TimeSpan.Zero);
        IPriceDataService priceDataService;
        List<Price> prices;
        switch (chargePrice.EnergyProvider)
        {
            case EnergyProvider.Octopus:
                break;
            case EnergyProvider.Tibber:
                break;
            case EnergyProvider.FixedPrice:
                priceDataService = serviceProvider.GetRequiredService<IFixedPriceService>();
                prices = (await priceDataService.GetPriceData(fromDateTimeOffset, toDateTimeOffset, chargePrice.EnergyProviderConfiguration).ConfigureAwait(false)).ToList();
                prices = AddDefaultChargePrices(prices, fromDateTimeOffset, toDateTimeOffset, chargePrice.GridPrice, chargePrice.SolarPrice);
                return prices;
            case EnergyProvider.Awattar:
                break;
            case EnergyProvider.Energinet:
                break;
            case EnergyProvider.HomeAssistant:
                break;
            case EnergyProvider.OldTeslaSolarChargerConfig:
                priceDataService = serviceProvider.GetRequiredService<IOldTscConfigPriceService>();
                prices = (await priceDataService.GetPriceData(fromDateTimeOffset, toDateTimeOffset, chargePrice.Id.ToString()).ConfigureAwait(false)).ToList();
                return prices;
            default:
                throw new ArgumentOutOfRangeException();
        }
        throw new NotImplementedException($"Energyprovider {chargePrice.EnergyProvider} is not implemented.");
    }

    private List<Price> AddDefaultChargePrices(List<Price> prices, DateTimeOffset from, DateTimeOffset to, decimal defaultValue, decimal defaultSolarPrice)
    {
        var updatedPrices = new List<Price>();

        // Sort the list by ValidFrom
        prices = prices.OrderBy(p => p.ValidFrom).ToList();

        // Initialize the start of the uncovered period
        var currentStart = from;

        foreach (var price in prices)
        {
            // If there's a gap between currentStart and the next price.ValidFrom
            if (currentStart < price.ValidFrom)
            {
                updatedPrices.Add(new Price
                {
                    GridPrice = defaultValue,
                    SolarPrice = defaultSolarPrice,
                    ValidFrom = currentStart,
                    ValidTo = price.ValidFrom,
                });
            }

            // Update currentStart to the end of the current price's ValidTo
            currentStart = price.ValidTo;
        }

        // Check for a gap after the last price.ValidTo to the 'to' date
        if (currentStart < to)
        {
            updatedPrices.Add(new Price
            {
                GridPrice = defaultValue,
                SolarPrice = defaultSolarPrice,
                ValidFrom = currentStart,
                ValidTo = to,
            });
        }

        // Add all original prices to the updated list
        updatedPrices.AddRange(prices);

        return updatedPrices.OrderBy(p => p.ValidFrom).ToList();
    }

    public async Task AddChargingDetailsForAllCars()
    {
        logger.LogTrace("{method}()", nameof(AddChargingDetailsForAllCars));
        var powerBuffer = configurationWrapper.PowerBuffer();
        var overage = settings.Overage ?? (settings.InverterPower - (powerBuffer < 0 ? 0 : powerBuffer));
        var homeBatteryDischargingPower = (-settings.HomeBatteryPower) ?? 0;
        if (homeBatteryDischargingPower < 0)
        {
            homeBatteryDischargingPower = 0;
        }
        var gridPower = (overage < 0) ? (-overage.Value) : 0;
        var solarPower = overage ?? 0;
        logger.LogTrace("SolarPower: {solarPower}", solarPower);
        logger.LogTrace("HomeBatteryDischargingPower: {homeBatteryDischargingPower}", homeBatteryDischargingPower);
        var loadPoints = await loadPointManagementService.GetLoadPointsWithChargingDetails().ConfigureAwait(false);
        var combinedChargingPowerAtHome = loadPoints.Select(l => l.ChargingPower).Sum();
        int usedGridPower;
        var usedHomeBatteryPower = 0;
        var usedSolarPower = 0;
        logger.LogTrace("Combined charging power at home: {combinedChargingPowerAtHome}", combinedChargingPowerAtHome);
        if (overage == default)
        {
            logger.LogTrace("Overage and inverterpower unknown using int max as usedGridPower.");
            usedGridPower = int.MaxValue;
        }
        else if (gridPower > combinedChargingPowerAtHome)
        {
            logger.LogTrace("Grid power is enough for all cars.");
            usedGridPower = combinedChargingPowerAtHome;
        }
        else
        {
            usedGridPower = gridPower;
            combinedChargingPowerAtHome -= gridPower;
            logger.LogTrace("Using {usedGridPower} W from grid", usedGridPower);
            if (homeBatteryDischargingPower > combinedChargingPowerAtHome)
            {
                logger.LogTrace("Home battery power is enough for all cars.");
                usedHomeBatteryPower = combinedChargingPowerAtHome;
            }
            else
            {
                usedHomeBatteryPower = homeBatteryDischargingPower;
                logger.LogTrace("Using {usedHomeBatteryPower} W from home battery", usedHomeBatteryPower);
                combinedChargingPowerAtHome -= homeBatteryDischargingPower;
                usedSolarPower = combinedChargingPowerAtHome;
                logger.LogTrace("Using {usedSolarPower} W from solar", usedSolarPower);
            }
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        //ToDo: order loadpoints by chargingPriority, so the most important car gets the least amount of Grid Power in its calculation
        foreach (var loadPoint in loadPoints)
        {
            logger.LogTrace("Adding meter values for loadpoint {@loadpoint}", loadPoint);
            if (!loadPoint.IsCharging)
            {
                continue;
            }
            var chargingPowerAtHome = loadPoint.ChargingPower;
            await AddNewChargingProcessIfRequired(loadPoint.CarId, loadPoint.ChargingConnectorId, currentDate);
            var dtoChargingValue = new DtoChargingValue();
            if (chargingPowerAtHome < usedGridPower)
            {
                dtoChargingValue.GridPower = chargingPowerAtHome;
                usedGridPower -= chargingPowerAtHome;
            }
            else
            {
                dtoChargingValue.GridPower = usedGridPower;
                usedGridPower = 0;
                chargingPowerAtHome -= dtoChargingValue.GridPower;
                if (chargingPowerAtHome < usedHomeBatteryPower)
                {
                    dtoChargingValue.HomeBatteryPower = chargingPowerAtHome;
                    usedHomeBatteryPower -= chargingPowerAtHome;
                }
                else
                {
                    dtoChargingValue.HomeBatteryPower = usedHomeBatteryPower;
                    usedHomeBatteryPower = 0;
                    chargingPowerAtHome -= dtoChargingValue.HomeBatteryPower;
                    dtoChargingValue.SolarPower = chargingPowerAtHome;
                }
            }
            logger.LogTrace("Solar power after charging detail: {solarPower}", usedSolarPower);
            logger.LogTrace("Home battery power after charging detail: {homeBatteryDischargingPower}", usedHomeBatteryPower);
            logger.LogTrace("Grid power after charging detail: {gridPower}", usedGridPower);
            logger.LogTrace("Created charging detail: {@chargingDetail}", dtoChargingValue);
            if (loadPoint.CarId != default)
            {
                var meterValue = new MeterValue(currentDate,
                    MeterValueKind.Car,
                    dtoChargingValue.SolarPower + dtoChargingValue.HomeBatteryPower + dtoChargingValue.GridPower)
                {
                    CarId = loadPoint.CarId,
                    MeasuredHomeBatteryPower = dtoChargingValue.HomeBatteryPower,
                    MeasuredGridPower = dtoChargingValue.GridPower,
                };
                if (!settings.CarsWithNonZeroMeterValueAddedLastCycle.ContainsKey(loadPoint.CarId.Value))
                {
                    logger.LogTrace("Adding default meter value for car {carId} at {timestamp}", loadPoint.CarId, meterValue.Timestamp);
                    meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    databaseValueBufferService.Add(GenerateDefaultMeterValue(loadPoint.CarId, null, meterValue.Timestamp));
                }
                settings.CarsWithNonZeroMeterValueAddedLastCycle[loadPoint.CarId.Value] = currentDate;
                databaseValueBufferService.Add(meterValue);
            }
            if (loadPoint.ChargingConnectorId != default)
            {
                var meterValue = new MeterValue(currentDate,
                    MeterValueKind.ChargingConnector,
                    dtoChargingValue.SolarPower + dtoChargingValue.HomeBatteryPower + dtoChargingValue.GridPower)
                {
                    ChargingConnectorId = loadPoint.ChargingConnectorId,
                    MeasuredHomeBatteryPower = dtoChargingValue.HomeBatteryPower,
                    MeasuredGridPower = dtoChargingValue.GridPower,
                };
                if (!settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle.ContainsKey(loadPoint.ChargingConnectorId.Value))
                {
                    logger.LogTrace("Adding default meter value for charging connector {chargingConnectorId} at {timestamp}", loadPoint.ChargingConnectorId, meterValue.Timestamp);
                    meterValue.Timestamp = meterValue.Timestamp.AddMilliseconds(1);
                    databaseValueBufferService.Add(GenerateDefaultMeterValue(null, loadPoint.ChargingConnectorId, meterValue.Timestamp));
                }
                settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle[loadPoint.ChargingConnectorId.Value] = currentDate;
                databaseValueBufferService.Add(meterValue);
                databaseValueBufferService.Add(new OcppChargingStationConnectorValueLog()
                {
                    Timestamp = currentDate,
                    Type = OcppChargingStationConnectorValueType.ChargerVoltage,
                    IntValue = loadPoint.ChargingVoltage,
                    OcppChargingStationConnectorId = loadPoint.ChargingConnectorId.Value,
                });
            }
        }
        var maxLatestNonZeroAge = TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds) * 2;
        var minDate = currentDate - maxLatestNonZeroAge;
        var nonZeroCarsCopy = settings.CarsWithNonZeroMeterValueAddedLastCycle.ToDictionary();
        foreach (var carLastNonZeroMeterValue in nonZeroCarsCopy)
        {
            if (carLastNonZeroMeterValue.Value < minDate)
            {
                logger.LogTrace("Adding ending meter value for car {carId} with last non zero meter value at {lastNonZeroMeterValue}", carLastNonZeroMeterValue.Key, carLastNonZeroMeterValue.Value);
                var endingMeterValue = GenerateDefaultMeterValue(carLastNonZeroMeterValue.Key, null, carLastNonZeroMeterValue.Value.AddMilliseconds(1));
                databaseValueBufferService.Add(endingMeterValue);
                settings.CarsWithNonZeroMeterValueAddedLastCycle.Remove(carLastNonZeroMeterValue.Key, out _);
            }
        }
        var nonZeroChargingConnectorsCopy = settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle.ToDictionary();
        foreach (var chargingConnectorLastNonZeroMeterValue in nonZeroChargingConnectorsCopy)
        {
            if (chargingConnectorLastNonZeroMeterValue.Value < minDate)
            {
                logger.LogTrace("Adding ending meter value for charging connector {chargingConnectorId} with last non zero meter value at {lastNonZeroMeterValue}", chargingConnectorLastNonZeroMeterValue.Key, chargingConnectorLastNonZeroMeterValue.Value);
                var endingMeterValue = GenerateDefaultMeterValue(null, chargingConnectorLastNonZeroMeterValue.Key, chargingConnectorLastNonZeroMeterValue.Value.AddMilliseconds(1));
                databaseValueBufferService.Add(endingMeterValue);
                settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle.Remove(chargingConnectorLastNonZeroMeterValue.Key, out _);
            }
        }
    }

    public MeterValue GenerateDefaultMeterValue(int? carId, int? chargingConnectorId, DateTimeOffset timestamp)
    {
        logger.LogTrace("{method}({carId}, {chargingConnectorId}, {timestamp})", nameof(GenerateDefaultMeterValue), carId, chargingConnectorId, timestamp);
        if (carId == default && chargingConnectorId == default)
        {
            throw new ArgumentException("Either carId or chargingConnectorId must be provided.");
        }
        var meterValue = new MeterValue(timestamp,
            carId != default ? MeterValueKind.Car : MeterValueKind.ChargingConnector,
            0)
        {
            CarId = carId,
            ChargingConnectorId = chargingConnectorId,
        };
        return meterValue;
    }

    public async Task AddNonZeroMeterValuesCarsAndChargingStationsToSettings()
    {
        logger.LogTrace("{method}()", nameof(AddNonZeroMeterValuesCarsAndChargingStationsToSettings));
        var carIds = await context.Cars
            .Where(c => c.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);

        foreach (var carId in carIds)
        {
            var latestMeterValue =
                await meterValueEstimationService.GetLatestMeterValueFromDatabase(MeterValueKind.Car, carId, null, false).ConfigureAwait(false);
            logger.LogTrace("Latest meter value for car {carId}: {@meterValue}", carId, latestMeterValue);
            if (latestMeterValue != default && latestMeterValue.MeasuredPower != 0)
            {
                settings.CarsWithNonZeroMeterValueAddedLastCycle[carId] = latestMeterValue.Timestamp;
            }
        }

        var chargingStationConnectorIds = await context.OcppChargingStationConnectors
            .Where(c => c.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);

        foreach (var chargingStationConnectorId in chargingStationConnectorIds)
        {
            var latestMeterValue =
                await meterValueEstimationService.GetLatestMeterValueFromDatabase(MeterValueKind.ChargingConnector, null, chargingStationConnectorId, false).ConfigureAwait(false);
            logger.LogTrace("Latest meter value for chargingConnector {chargingConnectorId}: {@meterValue}", chargingStationConnectorId, latestMeterValue);
            if (latestMeterValue != default && latestMeterValue.MeasuredPower != 0)
            {
                settings.ChargingConnectorsWithNonZeroMeterValueAddedLastCycle[chargingStationConnectorId] = latestMeterValue.Timestamp;
            }
        }

    }

    private class DtoChargingValue
    {
        public int GridPower { get; set; }
        public int HomeBatteryPower { get; set; }
        public int SolarPower { get; set; }
    }

    private async Task AddNewChargingProcessIfRequired(int? carId, int? chargingConnectorId, DateTimeOffset currentDate)
    {
        logger.LogTrace("{method}({carId}, {chargingConnectorId}, {currentDate})", nameof(AddNewChargingProcessIfRequired), carId, chargingConnectorId, currentDate);
        var latestOpenChargingProcessId = await context.ChargingProcesses
            .Where(cp => cp.CarId == carId
                         && cp.OcppChargingStationConnectorId == chargingConnectorId
                         && cp.EndDate == null)
            .OrderByDescending(cp => cp.StartDate)
            .Select(cp => cp.Id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (latestOpenChargingProcessId == default)
        {
            logger.LogTrace("No open charging process found for car {carId} and charging connector {chargingConnectorId}, creating new one.", carId, chargingConnectorId);
            if (carId != default)
            {
                logger.LogTrace("Checking for open charging process for car {carId}", carId);
                var openCarChargingProcess = await context.ChargingProcesses
                    .Where(cp => cp.CarId == carId && cp.EndDate == null)
                    .OrderByDescending(cp => cp.StartDate)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (openCarChargingProcess != default)
                {
                    logger.LogTrace("Found open charging process for car {carId}, finalizing it.", carId);
                    await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
                    await FinalizeChargingProcess(openCarChargingProcess).ConfigureAwait(false);
                }
            }
            if (chargingConnectorId != default)
            {
                logger.LogTrace("Checking for open charging process for charging connector {chargingConnectorId}", chargingConnectorId);
                var openChargingConnectorProcess = await context.ChargingProcesses
                    .Where(cp => cp.OcppChargingStationConnectorId == chargingConnectorId && cp.EndDate == null)
                    .OrderByDescending(cp => cp.StartDate)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (openChargingConnectorProcess != default)
                {
                    logger.LogTrace("Found open charging process for charging connector {chargingConnectorId}, finalizing it.", chargingConnectorId);
                    await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
                    await FinalizeChargingProcess(openChargingConnectorProcess).ConfigureAwait(false);
                }
            }
            var chargingProcess = new ChargingProcess
            {
                StartDate = currentDate.UtcDateTime,
                CarId = carId,
                OcppChargingStationConnectorId = chargingConnectorId,
            };
            context.ChargingProcesses.Add(chargingProcess);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
