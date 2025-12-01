using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AppendOptimalGridSchedulesReproductionTests : TestBase
{
    private const int MaxPower = 1000; // 1 kW

    public AppendOptimalGridSchedulesReproductionTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task AppendOptimalGridSchedules_PrioritizesCompletingScheduleOverPartialLowCost_WithExistingSchedules()
    {
        // Arrange
        var currentDate = CurrentFakeDate;
        // Target is 5 hours from now
        var nextTarget = CreateTarget(currentDate.AddHours(5));
        var loadpoint = CreateLoadPoint();
        var schedules = new List<DtoChargingSchedule>();

        // 5 Slots of 1 hour each, all same price
        var prices = new List<Price>();
        for (int i = 0; i < 5; i++)
        {
            prices.Add(CreatePrice(currentDate.AddHours(i), currentDate.AddHours(i + 1), 0.10m));
        }

        // Add an existing schedule for the first slot (e.g. Home Battery)
        schedules.Add(new DtoChargingSchedule(loadpoint.CarId.Value, loadpoint.ChargingConnectorId, MaxPower, new HashSet<ScheduleReason> { ScheduleReason.HomeBatteryDischarging })
        {
            ValidFrom = currentDate,
            ValidTo = currentDate.AddHours(1),
            TargetMinPower = MaxPower
        });

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(x => x.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(prices);

        // REMOVED explicit splitter setup. Should use default from TestBase.

        Mock.Mock<IConfigurationWrapper>()
            .Setup(x => x.ChargingSwitchCosts()).Returns(0m);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Need 3 hours of charging (3000 Wh)
        var energyToCharge = 3000;

        // Act
        var result = await service.AppendOptimalGridSchedules(currentDate, nextTarget, loadpoint, schedules, energyToCharge, MaxPower);

        // Assert

        var scheduledEnergyBeforeTargetFromGrid = result
            .Where(s => s.ValidTo <= nextTarget.NextExecutionTime)
            .Where(s => !s.ScheduleReasons.Contains(ScheduleReason.HomeBatteryDischarging)) // Filter out original
            .Sum(s => (s.ValidTo - s.ValidFrom).TotalHours * s.TargetMinPower);

        var scheduledEnergyAfterTarget = result
            .Where(s => s.ValidTo > nextTarget.NextExecutionTime)
            .Sum(s => (s.ValidTo - s.ValidFrom).TotalHours * s.TargetMinPower);

        Assert.Equal(3000, scheduledEnergyBeforeTargetFromGrid);
        Assert.Equal(0, scheduledEnergyAfterTarget);
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
