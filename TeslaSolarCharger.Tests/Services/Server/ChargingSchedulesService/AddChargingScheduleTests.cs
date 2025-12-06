using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Wrappers;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class AddChargingScheduleTests : TestBase
{
    public AddChargingScheduleTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Fact]
    public void AddChargingSchedule_ConsidersMaxCombinedCurrent()
    {
        // Arrange
        var scheduleService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Mock Configuration: Max Combined Current = 20A
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.MaxCombinedCurrent()).Returns(20);

        var existingSchedules = new List<DtoChargingSchedule>();
        var newSchedule = new DtoChargingSchedule(1, 1, 11000, [ScheduleReason.LatestPossibleTime])
        {
            ValidFrom = DateTimeOffset.UtcNow,
            ValidTo = DateTimeOffset.UtcNow.AddHours(1),
            TargetMinPower = 11000, // Requesting ~16A (11kW / 230V / 3 phases = 15.94A)
            Phases = 3,
            Voltage = 230
        };

        // Other schedule uses 10A (10A * 3 phases * 230V = 6900W)
        var otherSchedules = new List<DtoChargingSchedule>
        {
            new DtoChargingSchedule(2, 2, 22000, [ScheduleReason.CheapGridPrice])
            {
                ValidFrom = DateTimeOffset.UtcNow,
                ValidTo = DateTimeOffset.UtcNow.AddHours(1),
                TargetMinPower = 6900,
                Phases = 3,
                Voltage = 230,
                EstimatedSolarPower = 6900 // Ensure EstimatedChargingPower returns this
            }
        };

        // Expected available current: 20A - 10A = 10A
        // Expected max power: 10A * 3 * 230V = 6900W
        // newSchedule requests 11000W, should be capped at 6900W

        // Min Charging Power (e.g. 6A * 1 * 230 = 1380W)
        int minChargingPower = 1380;

        // Act
        var result = scheduleService.AddChargingSchedule(
            existingSchedules,
            newSchedule,
            11000,
            20000, // Enough energy allowed
            otherSchedules,
            minChargingPower
        );

        // Assert
        Assert.Single(result.chargingSchedulesAfterPowerAdd);
        var addedSchedule = result.chargingSchedulesAfterPowerAdd.First();
        Assert.Equal(6900, addedSchedule.TargetMinPower);
    }

    [Fact]
    public void AddChargingSchedule_ConsidersMaxCombinedCurrent_LimitsToZeroIfBelowMin()
    {
        // Arrange
        var scheduleService = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        // Mock Configuration: Max Combined Current = 12A
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.MaxCombinedCurrent()).Returns(12);

        var existingSchedules = new List<DtoChargingSchedule>();
        var newSchedule = new DtoChargingSchedule(1, 1, 11000, [ScheduleReason.LatestPossibleTime])
        {
            ValidFrom = DateTimeOffset.UtcNow,
            ValidTo = DateTimeOffset.UtcNow.AddHours(1),
            TargetMinPower = 4140, // 6A * 3 * 230
            Phases = 3,
            Voltage = 230
        };

        // Other schedule uses 10A
        var otherSchedules = new List<DtoChargingSchedule>
        {
            new DtoChargingSchedule(2, 2, 22000, [ScheduleReason.CheapGridPrice])
            {
                ValidFrom = DateTimeOffset.UtcNow,
                ValidTo = DateTimeOffset.UtcNow.AddHours(1),
                TargetMinPower = 6900, // 10A * 3 * 230
                Phases = 3,
                Voltage = 230,
                EstimatedSolarPower = 6900
            }
        };

        // Expected available current: 12A - 10A = 2A
        // Car min current is 6A (assumed via minChargingPower = 4140W for 3ph)
        // 2A < 6A, so should be 0.

        int minChargingPower = 4140;

        // Act
        var result = scheduleService.AddChargingSchedule(
            existingSchedules,
            newSchedule,
            11000,
            20000,
            otherSchedules,
            minChargingPower
        );

        // Assert
        Assert.Empty(result.chargingSchedulesAfterPowerAdd); // Should be empty as we couldn't add valid power
    }
}
