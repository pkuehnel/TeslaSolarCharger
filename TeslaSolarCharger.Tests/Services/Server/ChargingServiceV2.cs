using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using Xunit;
using Xunit.Abstractions;
using DateOnly = System.DateOnly;

namespace TeslaSolarCharger.Tests.Services.Server;

public class ChargingServiceV2 : TestBase
{
    public ChargingServiceV2(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    public static IEnumerable<object?[]> NextTargetUtcTestData
    {
        get
        {
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                new DateOnly(2025, 5, 26),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                new DateOnly(2025, 5, 27),
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 5,  0, 10, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 26, 12, 0,  0, TimeSpan.Zero),
            ];
            yield return
            [
                new DateTimeOffset(2025, 5, 26, 12, 0,  1, TimeSpan.Zero),
                null,
                new DateTimeOffset(2025, 5, 28, 12, 0,  0, TimeSpan.Zero),
            ];
        }
    }

    [Theory]
    [MemberData(nameof(NextTargetUtcTestData))]
    public void CanGetNextTargetDateRepeating(DateTimeOffset currentDate, DateOnly? targetDate, DateTimeOffset expectedResult)
    {
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(currentDate);
        var carValueLog = new CarChargingTarget()
        {
            Id = 1,
            CarId = 1,
            ClientTimeZone = "Europe/Berlin",
            RepeatOnMondays = true,
            RepeatOnWednesdays = true,
            TargetSoc = 20,
            TargetDate = targetDate,
            TargetTime = new TimeOnly(14, 0, 0),
        };
        var chargingServiceV2 = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var nextTargetUtc = chargingServiceV2.GetNextTargetUtc(carValueLog, currentDate);
        Assert.Equal(expectedResult, nextTargetUtc);
    }

    [Fact]
    public async Task PrepareChargingPlanContext_ComputesExpectedValues()
    {
        var currentDate = new DateTimeOffset(2025, 1, 6, 6, 0, 0, TimeSpan.Zero);
        var loadpoint = new DtoLoadPointOverview
        {
            CarId = 1,
            EstimatedVoltageWhileCharging = 230,
        };

        Context.Cars.Add(new Car
        {
            Id = 1,
            CarType = CarType.Tesla,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            MaximumPhases = 3,
            UsableEnergy = 75,
        });
        Context.CarChargingTargets.Add(new CarChargingTarget
        {
            Id = 1,
            CarId = 1,
            TargetSoc = 80,
            TargetTime = new TimeOnly(9, 0),
            RepeatOnMondays = true,
            ClientTimeZone = "UTC",
        });
        await Context.SaveChangesAsync();
        DetachAllEntities();

        var carDto = new DtoCar
        {
            Id = 1,
            ChargeModeV2 = ChargeModeV2.Auto,
            MaximumAmpere = 16,
            MinimumAmpere = 6,
            UsableEnergy = 75,
        };
        carDto.SoC.Update(currentDate, 50);
        carDto.SocLimit.Update(currentDate, 90);
        carDto.IsCharging.Update(currentDate, false);
        carDto.ChargerPhases.Update(currentDate, 3);
        carDto.PluggedIn.Update(currentDate, true);

        var cars = new List<DtoCar> { carDto };
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.Cars = cars;
        settingsMock.SetupGet(s => s.CarsToManage).Returns(cars);

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.CarChargeLoss()).Returns(0);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var context = await service.PrepareChargingPlanContext(loadpoint, carDto, currentDate, CancellationToken.None);

        context.Should().NotBeNull();
        var expectedEnergy = (80 - 50) * 75 * 10;
        context.EnergyToCharge.Should().Be(expectedEnergy);
        context.MinimumEnergyToCharge.Should().Be(expectedEnergy);
        context.MaxPower.Should().Be(3 * 16 * 230);
        context.NextTarget.NextExecutionTime.Should().Be(new DateTimeOffset(2025, 1, 6, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateSolarChargingSchedulesAsync_SplitsSchedulesUntilEnergyCovered()
    {
        var currentDate = new DateTimeOffset(2025, 1, 6, 6, 0, 0, TimeSpan.Zero);
        var loadpoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 2,
            EstimatedVoltageWhileCharging = 230,
        };
        var car = new DtoCar { Id = 1, ChargeModeV2 = ChargeModeV2.Auto };
        var target = new DtoTimeZonedChargingTarget
        {
            CarId = 1,
            NextExecutionTime = currentDate.AddHours(3),
        };
        var context = new TeslaSolarCharger.Server.Services.ChargingServiceV2.ChargingPlanContext(
            loadpoint,
            car,
            target,
            EnergyToCharge: 9000,
            MinimumEnergyToCharge: 6000,
            HomeBatteryEnergyToCharge: 0,
            MaxPower: 6000,
            MinPhases: 1,
            MinCurrent: 6);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.Cars = new List<DtoCar>();
        settingsMock.SetupGet(s => s.CarsToManage).Returns(new List<DtoCar>());

        var energyData = new Dictionary<DateTimeOffset, int>
        {
            [currentDate] = 6000,
            [currentDate.AddHours(1)] = 6000,
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(service => service.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(energyData);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var schedules = await service.CreateSolarChargingSchedulesAsync(context, currentDate, CancellationToken.None);

        schedules.Should().HaveCount(2);
        schedules[0].ValidFrom.Should().Be(currentDate);
        schedules[0].ValidTo.Should().Be(currentDate.AddHours(1));
        schedules[1].ValidTo.Should().Be(schedules[1].ValidFrom.AddMinutes(30));
        var deliveredEnergy = schedules.Sum(s => (s.ValidTo - s.ValidFrom).TotalHours * s.ChargingPower);
        deliveredEnergy.Should().Be(context.EnergyToCharge);
    }

    [Fact]
    public void CreateHomeBatteryDischargeSchedule_CreatesScheduleWithinBounds()
    {
        var currentDate = new DateTimeOffset(2025, 1, 6, 6, 0, 0, TimeSpan.Zero);
        var loadpoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 2,
        };
        var car = new DtoCar { Id = 1, ChargeModeV2 = ChargeModeV2.Auto };
        var target = new DtoTimeZonedChargingTarget
        {
            CarId = 1,
            NextExecutionTime = currentDate.AddHours(1),
            DischargeHomeBatteryToMinSoc = true,
        };
        var context = new TeslaSolarCharger.Server.Services.ChargingServiceV2.ChargingPlanContext(
            loadpoint,
            car,
            target,
            EnergyToCharge: 5000,
            MinimumEnergyToCharge: 0,
            HomeBatteryEnergyToCharge: 5000,
            MaxPower: 6000,
            MinPhases: null,
            MinCurrent: null);

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.Cars = new List<DtoCar>();
        settingsMock.SetupGet(s => s.CarsToManage).Returns(new List<DtoCar>());

        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryDischargingPower()).Returns(4000);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var schedule = service.CreateHomeBatteryDischargeSchedule(context, currentDate);

        schedule.Should().NotBeNull();
        schedule.ValidFrom.Should().Be(currentDate);
        schedule.ValidTo.Should().Be(target.NextExecutionTime);
        schedule.TargetGridPower.Should().Be(4000);
    }

    [Fact]
    public async Task PlanGridChargingSchedulesAsync_AddsScheduleForCheapestPrice()
    {
        var currentDate = new DateTimeOffset(2025, 1, 6, 6, 0, 0, TimeSpan.Zero);
        var loadpoint = new DtoLoadPointOverview
        {
            CarId = 1,
            ChargingConnectorId = 2,
        };
        var car = new DtoCar { Id = 1, ChargeModeV2 = ChargeModeV2.Auto };
        var target = new DtoTimeZonedChargingTarget
        {
            CarId = 1,
            NextExecutionTime = currentDate.AddHours(1),
        };
        var context = new TeslaSolarCharger.Server.Services.ChargingServiceV2.ChargingPlanContext(
            loadpoint,
            car,
            target,
            EnergyToCharge: 4000,
            MinimumEnergyToCharge: 4000,
            HomeBatteryEnergyToCharge: 0,
            MaxPower: 4000,
            MinPhases: null,
            MinCurrent: null);

        var price = new Price
        {
            ValidFrom = currentDate,
            ValidTo = currentDate.AddHours(1),
            GridPrice = 0.2m,
            SolarPrice = 0.0m,
        };

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.Cars = new List<DtoCar>();
        settingsMock.SetupGet(s => s.CarsToManage).Returns(new List<DtoCar>());

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(service => service.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<Price> { price });
        Mock.Mock<IValidFromToSplitter>()
            .Setup(splitter => splitter.SplitByBoundaries(It.IsAny<List<Price>>(), It.IsAny<List<DtoChargingSchedule>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .Returns((List<Price> left, List<DtoChargingSchedule> right, DateTimeOffset _, DateTimeOffset _) => (left, right.ToList()));
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.ChargingSwitchCosts()).Returns(0);

        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingServiceV2>();
        var schedulesForLoadPoint = new List<DtoChargingSchedule>();
        await service.PlanGridChargingSchedulesAsync(context, currentDate, schedulesForLoadPoint, CancellationToken.None);

        schedulesForLoadPoint.Should().ContainSingle();
        var schedule = schedulesForLoadPoint.Single();
        schedule.ValidFrom.Should().Be(price.ValidFrom);
        schedule.ValidTo.Should().Be(price.ValidTo);
        schedule.ChargingPower.Should().Be(context.MaxPower);
    }
}
