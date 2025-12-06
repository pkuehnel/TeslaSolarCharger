using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AppendOptimalGridSchedulesTests : TestBase
{
    private const int MaxPower = 11040;

    public AppendOptimalGridSchedulesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task AppendOptimalGridSchedules_ReturnsOriginalSchedules_WhenNoPricesAvailable()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(5));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<Price>());

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, 10000, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task AppendOptimalGridSchedules_ReturnsOriginalSchedules_WhenLastPriceEndsBeforeTarget()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(5));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        var prices = new List<Price>
        {
            CreatePrice(currentDate, currentDate.AddHours(4), 0.30m) // Ends 1 hour before target
        };

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, 10000, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(0.10, 0.20, 0)] // Cheapest first
    [InlineData(0.20, 0.10, 1)] // Cheapest second
    public async Task AppendOptimalGridSchedules_SelectsCheapestSlots(decimal price1, decimal price2, int expectedStartOffsetHours)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(2));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        // Two 1-hour slots
        var prices = new List<Price>
        {
            CreatePrice(currentDate, currentDate.AddHours(1), price1),
            CreatePrice(currentDate.AddHours(1), currentDate.AddHours(2), price2)
        };

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.ChargingSwitchCosts())
            .Returns(0m); // No switch cost logic interference

        // Charge 1 hour worth of energy (MaxPower * 1h)
        var energyToCharge = MaxPower;

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, energyToCharge, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        Assert.Single(result);
        var schedule = result.First();
        Assert.Equal(currentDate.AddHours(expectedStartOffsetHours), schedule.ValidFrom);
        Assert.Equal(MaxPower, schedule.TargetMinPower);
        Assert.Contains(ScheduleReason.CheapGridPrice, schedule.ScheduleReasons);
    }

    [Fact]
    public async Task AppendOptimalGridSchedules_Overflows_WhenEnergyExceedsGridSlots()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(1));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        var prices = new List<Price>
        {
            CreatePrice(currentDate, currentDate.AddHours(1), 0.10m)
        };

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.ChargingSwitchCosts()).Returns(0m);

        // Charge 2 hours worth of energy, but only 1 hour available before target
        var energyToCharge = MaxPower * 2;

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, energyToCharge, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        // Should have 2 schedules:
        // 1. Grid based (0-1h)
        // 2. Overflow based (1-2h) (LatestPossibleTime)
        Assert.Equal(2, result.Count);

        var gridSchedule = result.Single(s => s.ValidFrom == currentDate);
        Assert.Equal(currentDate.AddHours(1), gridSchedule.ValidTo);
        // With only 1 price available, code logic selects LatestPossibleTime
        Assert.Contains(ScheduleReason.LatestPossibleTime, gridSchedule.ScheduleReasons);

        var overflowSchedule = result.Single(s => s.ValidFrom == currentDate.AddHours(1));
        Assert.Equal(currentDate.AddHours(2), overflowSchedule.ValidTo);
        Assert.Contains(ScheduleReason.LatestPossibleTime, overflowSchedule.ScheduleReasons);
    }

    [Theory]
    [InlineData(0, 2)] // No switch cost: Pick cheapest (Slot 3, gap)
    [InlineData(1.0, 1)] // High switch cost: Pick contiguous (Slot 2)
    public async Task AppendOptimalGridSchedules_RespectsSwitchCosts(decimal switchCost, int expectedSecondSlotStartOffset)
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(3));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        // 3 Slots:
        // 1. 0.10 (Cheap)
        // 2. 0.15 (Medium, Contiguous with 1)
        // 3. 0.12 (Cheap-ish, Gap from 1)
        var prices = new List<Price>
        {
            CreatePrice(currentDate, currentDate.AddHours(1), 0.10m),
            CreatePrice(currentDate.AddHours(1), currentDate.AddHours(2), 0.15m),
            CreatePrice(currentDate.AddHours(2), currentDate.AddHours(3), 0.12m)
        };

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.ChargingSwitchCosts()).Returns(switchCost);

        var energyToCharge = MaxPower * 2; // Need 2 slots

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, energyToCharge, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.ValidFrom == currentDate); // Slot 1 is always picked as it is cheapest

        // Verify second slot
        Assert.Contains(result, s => s.ValidFrom == currentDate.AddHours(expectedSecondSlotStartOffset));
    }

    [Fact]
    public async Task AppendOptimalGridSchedules_PrioritizesRemainingEnergyOverCost()
    {
        // Arrange
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var currentDate = CurrentFakeDate;
        var nextTarget = CreateTarget(currentDate.AddHours(3));
        var loadpoint = CreateLoadPoint();

        // 3 Slots available in prices
        var prices = new List<Price>
        {
            CreatePrice(currentDate, currentDate.AddHours(1), 0.05m),           // Slot 1: Cheap, but occupied
            CreatePrice(currentDate.AddHours(1), currentDate.AddHours(2), 0.10m), // Slot 2: Affordable
            CreatePrice(currentDate.AddHours(2), currentDate.AddHours(3), 0.20m)  // Slot 3: Expensive
        };

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.ChargingSwitchCosts()).Returns(0m);

        // Pre-fill schedules with Slot 1 occupied
        var schedules = new List<DtoChargingSchedule>
        {
            new DtoChargingSchedule(1, 1, MaxPower, new HashSet<ScheduleReason> { ScheduleReason.CheapGridPrice })
            {
                ValidFrom = currentDate,
                ValidTo = currentDate.AddHours(1),
                TargetMinPower = MaxPower
            }
        };

        // We need to charge 1 hour worth of energy
        var energyToCharge = MaxPower;

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, energyToCharge, MaxPower, new List<DtoChargingSchedule>(), 0, 3, 230);

        // Assert
        // We expect Slot 2 (1h-2h) to be picked.
        // If bug exists: Slot 2 and 3 might be skipped, leading to 0 cost and overflow schedule (LatestPossibleTime at end).

        // There should be 2 schedules total: the pre-existing one, and the new one.
        Assert.Equal(2, result.Count);

        var newSchedule = result.Single(s => s.ValidFrom != currentDate); // The one that is not Slot 1

        Assert.Equal(currentDate.AddHours(1), newSchedule.ValidFrom);
        Assert.Equal(currentDate.AddHours(2), newSchedule.ValidTo);
        Assert.DoesNotContain(ScheduleReason.LatestPossibleTime, newSchedule.ScheduleReasons);
    }

    // Helper methods
    private DtoLoadPointOverview CreateLoadPoint(int carId = 1)
    {
        return new DtoLoadPointOverview
        {
            CarId = carId,
            ChargingConnectorId = 1,
            ChargingPower = 0,
            EstimatedVoltageWhileCharging = 230
        };
    }

    private DtoTimeZonedChargingTarget CreateTarget(DateTimeOffset executionTime, int targetSoc = 80)
    {
        return new DtoTimeZonedChargingTarget
        {
            NextExecutionTime = executionTime,
            TargetSoc = targetSoc
        };
    }

    private Price CreatePrice(DateTimeOffset from, DateTimeOffset to, decimal price)
    {
        return new Price
        {
            ValidFrom = from,
            ValidTo = to,
            GridPrice = price,
            SolarPrice = 0
        };
    }
}
