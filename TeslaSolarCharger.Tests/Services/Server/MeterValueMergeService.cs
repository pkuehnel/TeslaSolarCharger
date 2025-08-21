using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class MeterValueMergeService : TestBase
{
    public MeterValueMergeService(ITestOutputHelper outputHelper) 
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task MergeOldMeterValuesAsync_ShouldMergeOldValuesOnly()
    {
        // Arrange
        var service = CreateMeterValueMergeService();
        var olderThanDays = 21;
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        
        // Add old meter values (older than cutoff)
        var oldValues = new[]
        {
            new MeterValue(cutoffDate.AddHours(-2), MeterValueKind.SolarGeneration, 1000),
            new MeterValue(cutoffDate.AddHours(-2).AddMinutes(2), MeterValueKind.SolarGeneration, 1100),
            new MeterValue(cutoffDate.AddHours(-2).AddMinutes(4), MeterValueKind.SolarGeneration, 1200),
            new MeterValue(cutoffDate.AddHours(-2).AddMinutes(8), MeterValueKind.SolarGeneration, 1300),
        };

        // Add recent meter values (newer than cutoff) - should not be touched
        var recentValues = new[]
        {
            new MeterValue(cutoffDate.AddHours(1), MeterValueKind.SolarGeneration, 2000),
            new MeterValue(cutoffDate.AddHours(1).AddMinutes(2), MeterValueKind.SolarGeneration, 2100),
        };

        // Add car-related values (should not be touched)
        var carValues = new[]
        {
            new MeterValue(cutoffDate.AddHours(-3), MeterValueKind.Car, 500) { CarId = 1 },
            new MeterValue(cutoffDate.AddHours(-3).AddMinutes(2), MeterValueKind.Car, 600) { CarId = 1 },
        };

        await Context.MeterValues.AddRangeAsync([..oldValues, ..recentValues, ..carValues]);
        await Context.SaveChangesAsync();

        var originalCount = await Context.MeterValues.CountAsync();

        // Act
        await service.MergeOldMeterValuesAsync(olderThanDays);

        // Assert
        var finalCount = await Context.MeterValues.CountAsync();
        
        // Should have fewer meter values due to merging
        Assert.True(finalCount < originalCount);
        
        // Recent values should remain untouched
        var recentCount = await Context.MeterValues
            .Where(mv => mv.Timestamp >= cutoffDate)
            .CountAsync();
        Assert.Equal(recentValues.Length, recentCount);
        
        // Car values should remain untouched
        var carCount = await Context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.Car)
            .CountAsync();
        Assert.Equal(carValues.Length, carCount);
        
        // Old non-car values should be merged (fewer in count)
        var oldNonCarCount = await Context.MeterValues
            .Where(mv => mv.Timestamp < cutoffDate 
                && mv.MeterValueKind != MeterValueKind.Car 
                && mv.MeterValueKind != MeterValueKind.ChargingConnector)
            .CountAsync();
        Assert.True(oldNonCarCount < oldValues.Length);
    }

    [Fact]
    public async Task MergeOldMeterValuesAsync_ShouldMergeIntoFiveMinuteWindows()
    {
        // Arrange
        var service = CreateMeterValueMergeService();
        var olderThanDays = 21;
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        
        // Create values within the same 5-minute window
        var baseTime = cutoffDate.AddHours(-1);
        var valuesInSameWindow = new[]
        {
            new MeterValue(baseTime, MeterValueKind.HouseConsumption, 1000),
            new MeterValue(baseTime.AddMinutes(1), MeterValueKind.HouseConsumption, 1100),
            new MeterValue(baseTime.AddMinutes(3), MeterValueKind.HouseConsumption, 1200),
        };

        // Create values in different 5-minute window
        var valuesInDifferentWindow = new[]
        {
            new MeterValue(baseTime.AddMinutes(6), MeterValueKind.HouseConsumption, 2000),
            new MeterValue(baseTime.AddMinutes(7), MeterValueKind.HouseConsumption, 2100),
        };

        await Context.MeterValues.AddRangeAsync([..valuesInSameWindow, ..valuesInDifferentWindow]);
        await Context.SaveChangesAsync();

        // Act
        await service.MergeOldMeterValuesAsync(olderThanDays);

        // Assert
        var remainingValues = await Context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.HouseConsumption)
            .OrderBy(mv => mv.Timestamp)
            .ToListAsync();

        // Should have 2 values (one per 5-minute window)
        Assert.Equal(2, remainingValues.Count);
        
        // First value should be at the 5-minute boundary of the first window
        var firstWindow = new DateTimeOffset(baseTime.Year, baseTime.Month, baseTime.Day, 
            baseTime.Hour, (baseTime.Minute / 5) * 5, 0, baseTime.Offset);
        Assert.Equal(firstWindow, remainingValues[0].Timestamp);
        
        // Second value should be at the 5-minute boundary of the second window  
        var secondWindow = new DateTimeOffset(baseTime.AddMinutes(6).Year, baseTime.AddMinutes(6).Month, baseTime.AddMinutes(6).Day,
            baseTime.AddMinutes(6).Hour, (baseTime.AddMinutes(6).Minute / 5) * 5, 0, baseTime.AddMinutes(6).Offset);
        Assert.Equal(secondWindow, remainingValues[1].Timestamp);
    }

    [Fact]
    public async Task MergeOldMeterValuesAsync_ShouldNotTouchCarAndChargingConnectorValues()
    {
        // Arrange
        var service = CreateMeterValueMergeService();
        var olderThanDays = 21;
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        
        var carValues = new[]
        {
            new MeterValue(cutoffDate.AddHours(-1), MeterValueKind.Car, 1000) { CarId = 1 },
            new MeterValue(cutoffDate.AddHours(-1).AddMinutes(1), MeterValueKind.Car, 1100) { CarId = 1 },
        };

        var chargingConnectorValues = new[]
        {
            new MeterValue(cutoffDate.AddHours(-1), MeterValueKind.ChargingConnector, 2000) { ChargingConnectorId = 1 },
            new MeterValue(cutoffDate.AddHours(-1).AddMinutes(1), MeterValueKind.ChargingConnector, 2100) { ChargingConnectorId = 1 },
        };

        await Context.MeterValues.AddRangeAsync([..carValues, ..chargingConnectorValues]);
        await Context.SaveChangesAsync();

        var originalCount = await Context.MeterValues.CountAsync();

        // Act
        await service.MergeOldMeterValuesAsync(olderThanDays);

        // Assert
        var finalCount = await Context.MeterValues.CountAsync();
        
        // Count should remain the same as these values should not be touched
        Assert.Equal(originalCount, finalCount);
        
        // All values should still exist
        var carCount = await Context.MeterValues.Where(mv => mv.MeterValueKind == MeterValueKind.Car).CountAsync();
        var connectorCount = await Context.MeterValues.Where(mv => mv.MeterValueKind == MeterValueKind.ChargingConnector).CountAsync();
        
        Assert.Equal(carValues.Length, carCount);
        Assert.Equal(chargingConnectorValues.Length, connectorCount);
    }

    [Fact]
    public async Task MergeOldMeterValuesAsync_ShouldCorrectlyAdjustEnergyValuesWhenTimestampChanges()
    {
        // Arrange
        var service = CreateMeterValueMergeService();
        var olderThanDays = 21;
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        
        // Create a base time that will be rounded down to a 5-minute boundary
        var baseTime = cutoffDate.AddHours(-1);
        var windowBoundary = new DateTimeOffset(baseTime.Year, baseTime.Month, baseTime.Day, 
            baseTime.Hour, (baseTime.Minute / 5) * 5, 0, baseTime.Offset);
        
        // Create meter values within the same 5-minute window with energy values
        var valuesInSameWindow = new[]
        {
            new MeterValue(windowBoundary.AddMinutes(1), MeterValueKind.SolarGeneration, 1000) 
            { 
                EstimatedEnergyWs = 500000,         // 500,000 Ws at window+1min
                EstimatedHomeBatteryEnergyWs = 200000,
                EstimatedGridEnergyWs = 100000
            },
            new MeterValue(windowBoundary.AddMinutes(3), MeterValueKind.SolarGeneration, 1200) 
            { 
                EstimatedEnergyWs = 620000,         // 620,000 Ws at window+3min
                EstimatedHomeBatteryEnergyWs = 260000,
                EstimatedGridEnergyWs = 130000
            },
            new MeterValue(windowBoundary.AddMinutes(4), MeterValueKind.SolarGeneration, 1100) 
            { 
                EstimatedEnergyWs = 700000,         // 700,000 Ws at window+4min (latest)
                EstimatedHomeBatteryEnergyWs = 300000,
                EstimatedGridEnergyWs = 150000
            },
        };

        await Context.MeterValues.AddRangeAsync(valuesInSameWindow);
        await Context.SaveChangesAsync();

        // Act
        await service.MergeOldMeterValuesAsync(olderThanDays);

        // Assert
        var mergedValue = await Context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.SolarGeneration)
            .FirstAsync();

        // Verify timestamp is set to window boundary
        Assert.Equal(windowBoundary, mergedValue.Timestamp);
        
        // Average power should be (1000 + 1200 + 1100) / 3 = 1100
        Assert.Equal(1100, mergedValue.MeasuredPower);
        
        // Energy values should be adjusted based on actual power readings between timestamps
        // From 12:03 to 12:04: 1200W for 60s = 72,000 Ws
        // From 12:01 to 12:03: 1000W for 120s = 120,000 Ws  
        // From 12:00 to 12:01: 1000W for 60s = 60,000 Ws
        // Total energy consumed: 252,000 Ws
        
        // Expected energy at window boundary = latest - consumed based on raw meter values
        // 700,000 - 252,000 = 448,000
        Assert.Equal(448000, mergedValue.EstimatedEnergyWs);
        
        // Similar calculation for battery: 300,000 - 252,000 = 48,000
        Assert.Equal(48000, mergedValue.EstimatedHomeBatteryEnergyWs);
        
        // Similar calculation for grid: 150,000 - 252,000 = -102,000
        Assert.Equal(-102000, mergedValue.EstimatedGridEnergyWs);
    }

    private TeslaSolarCharger.Server.Services.MeterValueMergeService CreateMeterValueMergeService()
    {
        var dateTimeProvider = Mock.Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.DateTimeOffSetUtcNow()).Returns(DateTimeOffset.UtcNow);
        
        return new TeslaSolarCharger.Server.Services.MeterValueMergeService(
            Mock.Mock<ILogger<TeslaSolarCharger.Server.Services.MeterValueMergeService>>().Object,
            Context,
            dateTimeProvider.Object);
    }
}
