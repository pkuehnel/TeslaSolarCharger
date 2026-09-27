using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.ChargingSchedulesService;

public class GenerateChargingSchedulesForLoadPointTests : TestBase
{
    private const int CarId = 1;
    private const int Voltage = 230;
    private const int Phases = 3;
    private const int MaxCurrent = 16;
    private const int MaxPower = Voltage * Phases * MaxCurrent; // 11_040W
    private const int HomeBatteryDischargePower = 8_000;
    private const int PredictedHouseConsumptionPower = 1_000;

    public GenerateChargingSchedulesForLoadPointTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    /// <summary>
    /// The house consumes part of the home battery discharge power, so during a home battery discharge schedule only
    /// dischargePower - houseConsumption reaches the car. The schedule must keep the full discharge power as
    /// TargetHomeBatteryPower (the execution side controls the battery with it) but only credit the car with the
    /// remaining power, as otherwise the car reaches the target SoC later than planned.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_CreditsOnlyPowerReachingTheCar()
    {
        // Car needs 12000Wh, home battery discharge can deliver (8000 - 1000) * 2h = 14000Wh to the car => no grid needed
        await SetupScenario(carSoc: 50);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 70),
            CreatePredictedSurplusSlices(), CurrentFakeDate, CancellationToken.None, new());

        var schedule = Assert.Single(schedules);
        Assert.Contains(ScheduleReason.HomeBatteryDischarging, schedule.ScheduleReasons);
        Assert.Equal(HomeBatteryDischargePower, schedule.TargetHomeBatteryPower);
        Assert.Equal(HomeBatteryDischargePower - PredictedHouseConsumptionPower, schedule.EstimatedHomeBatteryPowerForCar);
        Assert.Equal(HomeBatteryDischargePower - PredictedHouseConsumptionPower, schedule.EstimatedChargingPower);
        Assert.Equal(12_000, schedule.EstimatedEnergy);
        Assert.Equal(0, schedule.TargetMinPower);
    }

    /// <summary>
    /// With a target SoC set, discharging the home battery only supports reaching the target SoC. When the home battery
    /// holds much more energy than the car needs, only the energy the car needs must be planned, otherwise the schedule
    /// covers the complete time until the target (e.g. the whole night) although the car reaches its target much earlier.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_PlansOnlyEnergyNeededForTargetSoc_WhenHomeBatteryHasMoreEnergy()
    {
        // Car needs 6000Wh, home battery holds 30000Wh and could deliver (8000 - 1000) * 2h = 14000Wh to the car until the target
        // => only 6000Wh at 7000W must be planned, i.e. the last 6/7 hours before the target
        await SetupScenario(carSoc: 50);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 60),
            CreatePredictedSurplusSlices(), CurrentFakeDate, CancellationToken.None, new());

        var schedule = Assert.Single(schedules);
        Assert.Contains(ScheduleReason.HomeBatteryDischarging, schedule.ScheduleReasons);
        Assert.Equal(6_000, schedule.EstimatedEnergy);
        Assert.Equal(CurrentFakeDate.AddHours(2), schedule.ValidTo);
        var expectedValidFrom = CurrentFakeDate.AddHours(2).AddHours(-6_000d / (HomeBatteryDischargePower - PredictedHouseConsumptionPower));
        Assert.InRange(schedule.ValidFrom, expectedValidFrom.AddSeconds(-1), expectedValidFrom.AddSeconds(1));
        Assert.DoesNotContain(schedules, s => s.TargetMinPower == MaxPower);
    }

    /// <summary>
    /// Without a target SoC the home battery must still be discharged as far as possible until the target time.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_DischargesAsMuchAsPossible_WhenNoTargetSocIsSet()
    {
        // Home battery holds 30000Wh, (8000 - 1000) * 2h = 14000Wh reach the car until the target
        await SetupScenario(carSoc: 50);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: null),
            CreatePredictedSurplusSlices(), CurrentFakeDate, CancellationToken.None, new());

        var schedule = Assert.Single(schedules);
        Assert.Contains(ScheduleReason.HomeBatteryDischarging, schedule.ScheduleReasons);
        Assert.Equal(14_000, schedule.EstimatedEnergy);
        Assert.Equal(CurrentFakeDate, schedule.ValidFrom);
        Assert.Equal(CurrentFakeDate.AddHours(2), schedule.ValidTo);
    }

    /// <summary>
    /// When the home battery holds less energy than the car needs, all of it must be planned and the rest must come
    /// from the grid.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_PlansCompleteHomeBatteryEnergyAndGrid_WhenCarNeedsMore()
    {
        // Car needs 12000Wh, home battery only holds (100 - 90) * 30000 / 100 = 3000Wh above its min SoC
        // => 3000Wh from the home battery, 9000Wh from the grid
        await SetupScenario(carSoc: 50);
        Mock.Mock<IHomeBatteryEnergyCalculator>()
            .Setup(h => h.GetHomeBatteryMinSocAtTime(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(90);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 70),
            CreatePredictedSurplusSlices(), CurrentFakeDate, CancellationToken.None, new());

        //The grid part is partly merged into the discharge schedule, so only the discharge schedule's existence can be checked
        Assert.Single(schedules, s => s.TargetHomeBatteryPower == HomeBatteryDischargePower);
        Assert.Contains(schedules, s => s.TargetMinPower == MaxPower);
        //Only 3000Wh can come from the home battery, so the energy planned without grid backing must not exceed it
        var homeBatteryOnlyEnergy = schedules.Where(s => s.TargetMinPower == 0).Sum(s => s.EstimatedEnergy);
        Assert.InRange(homeBatteryOnlyEnergy, 0, 3_000);
        var totalPlannedEnergy = schedules
            .Where(s => s.ValidTo <= CurrentFakeDate.AddHours(2))
            .Sum(s => s.EstimatedEnergy);
        Assert.InRange(totalPlannedEnergy, 11_900, 12_100);
    }

    /// <summary>
    /// When the predicted solar surplus already covers the energy the car needs for its target SoC, no home battery
    /// discharge must be planned.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_NotPlanned_WhenSolarCoversTargetSoc()
    {
        // Car needs 6000Wh, predicted surplus is 2 x 5000Wh and fully available as the battery is above min SoC
        await SetupScenario(carSoc: 50);
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(65);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(3_000);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 5_000 },
            { CurrentFakeDate.AddHours(1), 5_000 },
        };

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 60),
            surplusSlices, CurrentFakeDate, CancellationToken.None, new());

        Assert.Contains(schedules, s => s.EstimatedSolarPower == 5_000);
        Assert.DoesNotContain(schedules, s => s.TargetHomeBatteryPower > 0);
    }

    /// <summary>
    /// When the power reaching the car during home battery discharge is not enough to reach the target SoC in time,
    /// the missing energy must be scheduled from the grid upfront instead of being detected too late.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_AppendsGridSchedule_WhenPowerReachingCarIsNotEnough()
    {
        // Car needs 15000Wh, home battery discharge can deliver (8000 - 1000) * 2h = 14000Wh to the car
        // => 1000Wh must be planned from the grid even though 8000 * 2h = 16000Wh would suggest otherwise
        await SetupScenario(carSoc: 50);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 75),
            CreatePredictedSurplusSlices(), CurrentFakeDate, CancellationToken.None, new());

        var gridBackedSchedule = Assert.Single(schedules, s => s.TargetMinPower == MaxPower);
        Assert.True(gridBackedSchedule.ValidTo <= CurrentFakeDate.AddHours(2),
            "Grid backed schedule must be planned before the target time and not after it.");
        var totalPlannedEnergy = schedules
            .Where(s => s.ValidTo <= CurrentFakeDate.AddHours(2))
            .Sum(s => s.EstimatedEnergy);
        Assert.InRange(totalPlannedEnergy, 14_900, 15_100);
        // The home battery only part must still contain the unchanged power values
        var batteryOnlySchedule = Assert.Single(schedules, s => s.TargetMinPower == 0);
        Assert.Equal(HomeBatteryDischargePower, batteryOnlySchedule.TargetHomeBatteryPower);
        Assert.Equal(HomeBatteryDischargePower - PredictedHouseConsumptionPower, batteryOnlySchedule.EstimatedHomeBatteryPowerForCar);
    }

    /// <summary>
    /// When the home battery is below its min SoC, the execution side reserves the home battery charging power from the
    /// solar surplus before the car gets anything. Solar based planning must mirror this: the energy the home battery
    /// needs to reach its min SoC must not be credited to the car, otherwise too little grid charging is planned and
    /// the car misses its target SoC.
    /// </summary>
    [Fact]
    public async Task SolarSchedules_ReserveSurplusForHomeBattery_WhenBelowMinSoc()
    {
        // Car needs 6000Wh. Predicted surplus is 5000Wh in each of the two slices until the target.
        // Home battery deficit: (80 - 65) * 30000 / 100 = 4500Wh, charging power cap 3000Wh per slice
        // => slice 1: 5000 - 3000 = 2000Wh for the car, slice 2: 5000 - 1500 = 3500Wh for the car
        // => 5500Wh solar credited, 500Wh must be planned from the grid
        await SetupScenario(carSoc: 50);
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(80);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(3_000);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 5_000 },
            { CurrentFakeDate.AddHours(1), 5_000 },
        };

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(),
            CreateTargets(targetSoc: 60, dischargeHomeBatteryToMinSoc: false), surplusSlices, CurrentFakeDate,
            CancellationToken.None, new());

        Assert.Contains(schedules, s => s.EstimatedSolarPower == 2_000);
        Assert.Contains(schedules, s => s.EstimatedSolarPower == 3_500);
        Assert.DoesNotContain(schedules, s => s.EstimatedSolarPower == 5_000);
        //The 500Wh not covered by solar must be planned from the grid before the target time
        Assert.Contains(schedules, s => s.TargetMinPower == MaxPower);
        var totalPlannedEnergy = schedules
            .Where(s => s.ValidTo <= CurrentFakeDate.AddHours(2))
            .Sum(s => s.EstimatedEnergy);
        Assert.InRange(totalPlannedEnergy, 5_900, 6_100);
    }

    /// <summary>
    /// When the home battery is at or above its min SoC, the full predicted surplus is available for the car and no
    /// grid charging must be planned when the surplus covers the energy to charge.
    /// </summary>
    [Fact]
    public async Task SolarSchedules_UseFullSurplus_WhenHomeBatteryAtMinSoc()
    {
        // Car needs 6000Wh, predicted surplus is 2 x 5000Wh and fully available as the battery is above min SoC
        await SetupScenario(carSoc: 50);
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(65);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(3_000);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 5_000 },
            { CurrentFakeDate.AddHours(1), 5_000 },
        };

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(),
            CreateTargets(targetSoc: 60, dischargeHomeBatteryToMinSoc: false), surplusSlices, CurrentFakeDate,
            CancellationToken.None, new());

        Assert.Contains(schedules, s => s.EstimatedSolarPower == 5_000);
        Assert.DoesNotContain(schedules, s => s.TargetMinPower == MaxPower);
        var totalPlannedEnergy = schedules
            .Where(s => s.ValidTo <= CurrentFakeDate.AddHours(2))
            .Sum(s => s.EstimatedEnergy);
        Assert.InRange(totalPlannedEnergy, 5_900, 6_100);
    }

    /// <summary>
    /// With solar based scheduling enabled, a predicted daytime surplus is already credited to the car via a solar
    /// schedule. The same surplus must not additionally cancel out the predicted house consumption of other hours when
    /// estimating how much home battery discharge power reaches the car, otherwise the surplus is credited twice
    /// (once as solar schedule energy and once as reduced house consumption) and the car misses its target SoC.
    /// Scenario: +5000Wh surplus in hour 1 (credited as a solar schedule), -1000Wh net house consumption in hour 2.
    /// During hour 1 the house is fully covered by the surplus (0W taken from the discharge power), during hour 2 the
    /// house consumes 1000W of the discharge power => on average 500W of the discharge power do not reach the car.
    /// </summary>
    [Fact]
    public async Task HomeBatteryDischargeSchedule_DoesNotCreditSolarScheduledSurplusAgainstHouseConsumption()
    {
        await SetupScenario(carSoc: 50);
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.UsePredictedSolarPowerGenerationForChargingSchedules()).Returns(true);
        //Battery is at min SoC, so the full surplus is credited to the car by the solar based scheduling
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(65);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(3_000);
        var service = Mock.Create<TeslaSolarCharger.Server.Services.ChargingScheduleService>();
        var surplusSlices = new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, 5_000 },
            { CurrentFakeDate.AddHours(1), -1_000 },
        };

        var schedules = await service.GenerateChargingSchedulesForLoadPoint(CreateLoadPoint(), CreateTargets(targetSoc: 70),
            surplusSlices, CurrentFakeDate, CancellationToken.None, new());

        //The surplus of hour 1 is already credited to the car as a solar schedule
        Assert.Contains(schedules, s => s.EstimatedSolarPower == 5_000);
        //Hour 1: house fully covered by the surplus, hour 2: 1000W house consumption
        //=> on average 500W of the discharge power are consumed by the house and must not be credited to the car.
        var dischargeSchedules = schedules.Where(s => s.TargetHomeBatteryPower > 0).ToList();
        Assert.NotEmpty(dischargeSchedules);
        Assert.All(dischargeSchedules, s =>
            Assert.Equal(HomeBatteryDischargePower - 500, s.EstimatedHomeBatteryPowerForCar));
    }

    private async Task SetupScenario(int carSoc)
    {
        Context.Cars.Add(new Car
        {
            Id = CarId,
            Name = "Test Car",
            MinimumAmpere = 1,
            MaximumAmpere = MaxCurrent,
            UsableEnergy = 60,
            MaximumPhases = Phases,
            CarType = CarType.Tesla,
            ChargeMode = ChargeModeV2.Auto,
        });
        await Context.SaveChangesAsync();

        var dtoCar = new DtoCar
        {
            Id = CarId,
            Name = "Test Car",
            ChargeModeV2 = ChargeModeV2.Auto,
            SoC = new(CurrentFakeDate, carSoc),
            SocLimit = new(CurrentFakeDate, 100),
            ChargerPhases = new(CurrentFakeDate, Phases),
        };

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        settingsMock.Setup(s => s.HomeBatterySoc).Returns(65);

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryDischargingPower()).Returns(HomeBatteryDischargePower);
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(30_000);
        configurationWrapperMock.Setup(c => c.CarChargeLoss()).Returns(0);
        configurationWrapperMock.Setup(c => c.ChargingSwitchCosts()).Returns(0m);
        configurationWrapperMock.Setup(c => c.MaxCombinedCurrent()).Returns(32);

        var homeBatteryEnergyCalculatorMock = Mock.Mock<IHomeBatteryEnergyCalculator>();
        homeBatteryEnergyCalculatorMock
            .Setup(h => h.GetHomeBatteryMinSocAtTime(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        homeBatteryEnergyCalculatorMock
            .Setup(h => h.GetEstimatedHomeBatterySocAtTime(It.IsAny<DateTimeOffset>(), It.IsAny<int>(),
                It.IsAny<List<DtoChargingSchedule>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(t => t.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(new List<Price>
            {
                new()
                {
                    ValidFrom = CurrentFakeDate.AddHours(-1),
                    ValidTo = CurrentFakeDate.AddHours(3),
                    GridPrice = 0.30m,
                    SolarPrice = 0m,
                    IsSpotPriceBased = false,
                },
            });
    }

    private DtoLoadPointOverview CreateLoadPoint()
    {
        return new DtoLoadPointOverview
        {
            CarId = CarId,
            ChargingConnectorId = null,
            ChargingPower = 0,
            EstimatedVoltageWhileCharging = Voltage,
        };
    }

    private List<DtoTimeZonedChargingTarget> CreateTargets(int? targetSoc, bool dischargeHomeBatteryToMinSoc = true)
    {
        return new List<DtoTimeZonedChargingTarget>
        {
            new()
            {
                Id = 1,
                CarId = CarId,
                TargetSoc = targetSoc,
                DischargeHomeBatteryToMinSoc = dischargeHomeBatteryToMinSoc,
                NextExecutionTime = CurrentFakeDate.AddHours(2),
            },
        };
    }

    private Dictionary<DateTimeOffset, int> CreatePredictedSurplusSlices()
    {
        // 1000Wh net house consumption per one hour slice
        return new Dictionary<DateTimeOffset, int>
        {
            { CurrentFakeDate, -PredictedHouseConsumptionPower },
            { CurrentFakeDate.AddHours(1), -PredictedHouseConsumptionPower },
        };
    }
}
