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
        Assert.Equal(14_000, schedule.EstimatedEnergy);
        Assert.Equal(0, schedule.TargetMinPower);
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

    private List<DtoTimeZonedChargingTarget> CreateTargets(int targetSoc, bool dischargeHomeBatteryToMinSoc = true)
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
