using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Extras.Moq;
using Microsoft.EntityFrameworkCore;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using Xunit;
using Xunit.Abstractions;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingServiceV2;

public class SetCarChargingTargetsToFulFilledTests : TestBase
{
    public SetCarChargingTargetsToFulFilledTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    // --- HELPER METHODS ---

    private DtoCar CreateDtoCar(int id, int? soc, bool pluggedIn, bool isHome, int socLimit = 100)
    {
        return new DtoCar
        {
            Id = id,
            SoC = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, soc),
            PluggedIn = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, pluggedIn),
            IsHomeGeofence = new DtoTimeStampedValue<bool?>(DateTimeOffset.MinValue, isHome),
            SocLimit = new DtoTimeStampedValue<int?>(DateTimeOffset.MinValue, socLimit),
            ShouldBeManaged = true
        };
    }

    private CarChargingTarget CreateTarget(int id, int carId, int? targetSoc, DateOnly? targetDate = null, DateTimeOffset? lastFulfilled = null, bool dischargeHomeBattery = false)
    {
        return new CarChargingTarget
        {
            Id = id,
            CarId = carId,
            TargetSoc = targetSoc,
            TargetDate = targetDate,
            TargetTime = new TimeOnly(12, 0), // Default 12:00
            LastFulFilled = lastFulfilled,
            DischargeHomeBatteryToMinSoc = dischargeHomeBattery,
            // Default no repetition
            RepeatOnMondays = false,
            RepeatOnTuesdays = false,
            RepeatOnWednesdays = false,
            RepeatOnThursdays = false,
            RepeatOnFridays = false,
            RepeatOnSaturdays = false,
            RepeatOnSundays = false
        };
    }

    // --- TESTS ---

    [Theory]
    // Scenario: TargetSoc is default (null) and DischargeHomeBatteryToMinSoc is false -> Remove
    [InlineData(null, false, true)]
    // Scenario: Valid target -> Keep
    [InlineData(80, false, false)]
    // Scenario: Valid target (0%) -> Keep
    [InlineData(0, false, false)]
    public async Task SetCarChargingTargetsToFulFilled_RemovesInvalidTargets(int? targetSoc, bool dischargeHomeBattery, bool shouldRemove)
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);
        var target = CreateTarget(1, carId, targetSoc, DateOnly.FromDateTime(CurrentFakeDate.Date), null, dischargeHomeBattery);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstOrDefaultAsync(t => t.Id == target.Id);
        if (shouldRemove)
        {
            Assert.Null(dbTarget);
        }
        else
        {
            Assert.NotNull(dbTarget);
        }
    }

    [Theory]
    // Reached Target SoC
    [InlineData(80, 80, true, true, true)]
    [InlineData(81, 80, true, true, true)]
    [InlineData(79, 80, true, true, false)] // Not reached
    // Unplugged
    [InlineData(50, 80, false, true, true)]
    // Not Home
    [InlineData(50, 80, true, false, true)]
    public async Task SetCarChargingTargetsToFulFilled_MarksFulfilled_WhenConditionsMet(int carSoc, int targetSoc, bool pluggedIn, bool isHome, bool shouldFulfill)
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, carSoc, pluggedIn, isHome);
        var target = CreateTarget(1, carId, targetSoc, DateOnly.FromDateTime(CurrentFakeDate.Date));

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(targetSoc);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstAsync(t => t.Id == target.Id);
        if (shouldFulfill)
        {
            Assert.Equal(CurrentFakeDate, dbTarget.LastFulFilled);
        }
        else
        {
            Assert.Null(dbTarget.LastFulFilled);
        }
    }

    [Theory]
    // Home Battery Low -> Fulfill
    [InlineData(10, 20, true)]
    // Home Battery High -> Not Fulfill
    [InlineData(30, 20, false)]
    public async Task SetCarChargingTargetsToFulFilled_DischargeHomeBattery_Logic(int currentHomeBatterySoc, int minHomeBatterySoc, bool shouldFulfill)
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);
        // TargetSoc 0/null but DischargeHomeBattery = true. ActualTargetSoc will be default (0/null).
        var target = CreateTarget(1, carId, null, DateOnly.FromDateTime(CurrentFakeDate.Date), null, true);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.HomeBatterySoc).Returns(currentHomeBatterySoc);

        Mock.Mock<IConfigurationWrapper>()
            .Setup(c => c.HomeBatteryMinSoc())
            .Returns(minHomeBatterySoc);

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns((int?)null);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstAsync(t => t.Id == target.Id);
        if (shouldFulfill)
        {
            Assert.Equal(CurrentFakeDate, dbTarget.LastFulFilled);
        }
        else
        {
            Assert.Null(dbTarget.LastFulFilled);
        }
    }

    [Fact]
    public async Task SetCarChargingTargetsToFulFilled_RemovesExpiredNonRepeatingTargets()
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);

        // Target Date was YESTERDAY. Fulfilled TODAY (or later).
        // If LastFulFilled is TODAY, and TargetDate was YESTERDAY.
        // targetDate < LastFulFilled -> Removed.
        var targetDate = DateOnly.FromDateTime(CurrentFakeDate.Date.AddDays(-1));
        var lastFulfilled = CurrentFakeDate;

        var target = CreateTarget(1, carId, 80, targetDate, lastFulfilled);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(80);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstOrDefaultAsync(t => t.Id == target.Id);
        Assert.Null(dbTarget);
    }

    [Fact]
    public async Task SetCarChargingTargetsToFulFilled_KeepsNonExpiredNonRepeatingTargets()
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);

        // Target Date is TODAY. LastFulFilled is before target time.
        // targetDate (12:00) > LastFulFilled (11:00). Keep.

        var targetDate = DateOnly.FromDateTime(CurrentFakeDate.Date);
        var lastFulfilled = CurrentFakeDate.Date.AddHours(11); // 11:00 UTC

        var target = CreateTarget(1, carId, 80, targetDate, lastFulfilled);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(80);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstOrDefaultAsync(t => t.Id == target.Id);
        Assert.NotNull(dbTarget);
    }

    [Fact]
    public async Task SetCarChargingTargetsToFulFilled_RemovesTargetWithoutDateAndNoRepetition()
    {
        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);
        var target = CreateTarget(1, carId, 80, null); // No date

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(80);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstOrDefaultAsync(t => t.Id == target.Id);
        Assert.Null(dbTarget);
    }

    [Fact]
    public async Task SetCarChargingTargetsToFulFilled_PotentialBug_ExpiredUnfulfilledTargetsAreKept()
    {
        // This test documents existing behavior that might be a bug.
        // If a target is in the past but was never fulfilled, it remains in the DB.

        // Arrange
        var carId = 1;
        var car = CreateDtoCar(carId, 50, true, true);
        var targetDate = DateOnly.FromDateTime(CurrentFakeDate.Date.AddDays(-1));
        var target = CreateTarget(1, carId, 80, targetDate, null); // Never fulfilled

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.CarsToManage).Returns(new List<DtoCar> { car });
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { car });

        Mock.Mock<IChargingScheduleService>()
            .Setup(s => s.GetActualTargetSoc(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(80);

        Context.CarChargingTargets.Add(target);
        await Context.SaveChangesAsync();

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();

        // Act
        await service.SetCarChargingTargetsToFulFilled(CurrentFakeDate);

        // Assert
        var dbTarget = await Context.CarChargingTargets.FirstOrDefaultAsync(t => t.Id == target.Id);

        // Asserting that it is NOT removed (current behavior)
        Assert.NotNull(dbTarget);
    }
}
