using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Contracts;
using Quartz;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server;

public class MeterValueMergeJobIntegrationTest : TestBase
{
    public MeterValueMergeJobIntegrationTest(ITestOutputHelper outputHelper) 
        : base(outputHelper)
    {
    }

    [Fact]
    public async Task MeterValueMergeJob_Execute_ShouldMergeOldMeterValues()
    {
        // Arrange
        var job = CreateMeterValueMergeJob();
        var jobContext = Mock.Mock<IJobExecutionContext>();
        jobContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        
        // Create test data that's older than 21 days
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-21);
        var oldValues = new[]
        {
            // Multiple values in same 5-minute window for SolarGeneration
            new MeterValue(cutoffDate.AddHours(-1), MeterValueKind.SolarGeneration, 1000),
            new MeterValue(cutoffDate.AddHours(-1).AddMinutes(2), MeterValueKind.SolarGeneration, 1100),
            new MeterValue(cutoffDate.AddHours(-1).AddMinutes(4), MeterValueKind.SolarGeneration, 1200),
            
            // Values for HouseConsumption
            new MeterValue(cutoffDate.AddHours(-2), MeterValueKind.HouseConsumption, 2000),
            new MeterValue(cutoffDate.AddHours(-2).AddMinutes(1), MeterValueKind.HouseConsumption, 2100),
            
            // Recent values that should not be touched
            new MeterValue(cutoffDate.AddDays(1), MeterValueKind.SolarGeneration, 3000),
            
            // Car values that should not be touched (even if old)
            new MeterValue(cutoffDate.AddHours(-3), MeterValueKind.Car, 4000) { CarId = 1 },
            
            // ChargingConnector values that should not be touched (even if old)
            new MeterValue(cutoffDate.AddHours(-3), MeterValueKind.ChargingConnector, 5000) { ChargingConnectorId = 1 },
        };

        await Context.MeterValues.AddRangeAsync(oldValues);
        await Context.SaveChangesAsync();

        var originalCount = await Context.MeterValues.CountAsync();

        // Act
        await job.Execute(jobContext.Object);

        // Assert
        var finalCount = await Context.MeterValues.CountAsync();
        
        // Should have fewer total values due to merging
        Assert.True(finalCount < originalCount);
        
        // Verify that recent values are untouched
        var recentCount = await Context.MeterValues
            .Where(mv => mv.Timestamp >= cutoffDate)
            .CountAsync();
        Assert.Equal(1, recentCount); // Only one recent value was added
        
        // Verify that car values are untouched
        var carCount = await Context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.Car)
            .CountAsync();
        Assert.Equal(1, carCount);
        
        // Verify that charging connector values are untouched
        var connectorCount = await Context.MeterValues
            .Where(mv => mv.MeterValueKind == MeterValueKind.ChargingConnector)
            .CountAsync();
        Assert.Equal(1, connectorCount);
        
        // Verify that old mergeable values have been reduced
        var oldMergeableCount = await Context.MeterValues
            .Where(mv => mv.Timestamp < cutoffDate 
                && mv.MeterValueKind != MeterValueKind.Car 
                && mv.MeterValueKind != MeterValueKind.ChargingConnector)
            .CountAsync();
        
        // Should have only 2 values left (one for each meter kind: SolarGeneration and HouseConsumption)
        Assert.Equal(2, oldMergeableCount);
    }

    private MeterValueMergeJob CreateMeterValueMergeJob()
    {
        var dateTimeProvider = Mock.Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.DateTimeOffSetUtcNow()).Returns(DateTimeOffset.UtcNow);
        
        var meterValueMergeService = new MeterValueMergeService(
            Mock.Mock<ILogger<MeterValueMergeService>>().Object,
            Context,
            dateTimeProvider.Object);

        return new MeterValueMergeJob(
            Mock.Mock<ILogger<MeterValueMergeJob>>().Object,
            meterValueMergeService);
    }
}