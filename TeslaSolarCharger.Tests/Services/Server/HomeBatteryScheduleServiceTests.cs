using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Server.Services.HomeBatteryControl;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.SharedModel.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class HomeBatteryScheduleServiceTests : TestBase
{
    //Evening, so the battery has to last through the night until solar self sufficiency at 08:00 the next day.
    private static readonly DateTimeOffset EveningDate = new(2023, 2, 2, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SelfSufficiencyTime = new(2023, 2, 3, 8, 0, 0, TimeSpan.Zero);

    public HomeBatteryScheduleServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CalculateWindows_EmptyBatteryAndFlatPrices_CreatesNoWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        AddHourlyPrices(input, Enumerable.Repeat(0.30m, 12).ToArray());

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    //With flat prices it does not matter when the stored energy is used, so the battery keeps its default behavior.
    [Fact]
    public void CalculateWindows_StoredEnergyAndFlatPrices_CreatesNoWindows()
    {
        var input = CreateNightInput(currentSocPercent: 30);
        AddHourlyPrices(input, Enumerable.Repeat(0.30m, 12).ToArray());

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    //2000 Wh above the min SoC cover the four most expensive hours, the cheaper hours before them are held.
    [Fact]
    public void CalculateWindows_StoredEnergyNotLastingTheNight_HoldsCheapestSlicesBeforeExpensiveOnes()
    {
        var input = CreateNightInput(currentSocPercent: 30);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.20m, 0.18m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.PreserveForDeficit, window.Reason);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(8), window.ValidTo);
        Assert.Equal(4000, window.PlannedEnergyWh);
        Assert.Null(window.OnlyWhileSocAtOrBelowPercent);
    }

    //Spot prices: the cheap slice can only cover demand after it. The expensive first hour stays on the grid.
    [Fact]
    public void CalculateWindows_SpotPrices_GridChargeOnlyCoversLaterDemand()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        AddHourlyPrices(input, new[] { 0.40m, 0.10m, 0.40m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.GridChargeForDeficit, window.Reason);
        Assert.Equal(EveningDate.AddHours(1), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(2), window.ValidTo);
        //Ten hours of 500 Wh after the cheap hour.
        Assert.Equal(5000, window.PlannedEnergyWh);
        Assert.Equal(60, window.TargetSocPercent);
        Assert.Equal(0.10m, window.GridPricePerKwh);
    }

    [Fact]
    public void CalculateWindows_CheapestSliceAfterAllDemand_CreatesNoChargeWindow()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        AddHourlyPrices(input, new[] { 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.10m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    [Fact]
    public void CalculateWindows_PriceDifferenceBelowUsageCosts_CreatesNoChargeWindow()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        //0.27 + 0.05 usage costs is not cheaper than 0.30.
        AddHourlyPrices(input, new[] { 0.27m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    [Fact]
    public void CalculateWindows_MaxChargeSoc_LimitsGridCharge()
    {
        var input = CreateNightInput(currentSocPercent: 10, maxChargeSocPercent: 30);
        AddHourlyPrices(input, new[] { 0.10m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //The battery is empty again once the bought energy is used, so nothing is held afterwards.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
        Assert.Equal(2000, window.PlannedEnergyWh);
        Assert.Equal(30, window.TargetSocPercent);
    }

    [Theory]
    //Six hours after the cheap hour need 3000 Wh, the budget of 3500 Wh (all seven hours) does not limit.
    [InlineData(0, 3000)]
    //A negative buffer of 10 % of the battery (1000 Wh) lowers the budget to 2500 Wh.
    [InlineData(-10, 2500)]
    //A positive buffer can not buy more than the house is predicted to consume after the cheap hour.
    [InlineData(10, 3000)]
    public void CalculateWindows_ChargeBuffer_LimitsGridChargeForHouseConsumption(int chargeBufferPercent, int expectedChargedWh)
    {
        var input = CreateNightInput(currentSocPercent: 10);
        input.ChargeBufferPercent = chargeBufferPercent;
        input.SelfSufficiencyTime = EveningDate.AddHours(7);
        AddHourlyPrices(input, new[] { 0.10m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(expectedChargedWh, window.PlannedEnergyWh);
    }

    //Stored energy reduces what is bought for the house: 3500 Wh consumption - 1000 Wh buffer - 1000 Wh stored = 1500 Wh.
    [Fact]
    public void CalculateWindows_ChargeBuffer_SubtractsStoredEnergy()
    {
        var input = CreateNightInput(currentSocPercent: 20);
        input.ChargeBufferPercent = -10;
        input.SelfSufficiencyTime = EveningDate.AddHours(7);
        AddHourlyPrices(input, new[] { 0.10m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(1500, window.PlannedEnergyWh);
    }

    //Production case 2026-11-03: a car needs more energy than fits into the cheap hours and also charges in expensive
    //hours. The battery is filled during the cheap hours and discharged into the car during the expensive hours.
    [Fact]
    public void CalculateWindows_CarChargingInExpensiveHours_ChargesBatteryInCheapHoursForTheCar()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        input.MaxDischargingPowerW = 5000;
        //Cheap from 00:00 to 05:00.
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.10m, 0.10m, 0.10m, 0.10m, 0.10m, 0.30m, 0.30m, 0.30m, });
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(2), SelfSufficiencyTime));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Equal(2, windows.Count);
        //Each expensive hour after 05:00 needs 500 Wh house + 4500 Wh car (discharge power limit). The battery can take
        //9000 Wh between the min SoC of 10 % and 100 %.
        var chargeWindow = windows[0];
        Assert.Equal(HomeBatteryMode.Charge, chargeWindow.Mode);
        Assert.Equal(EveningDate.AddHours(4), chargeWindow.ValidFrom);
        Assert.Equal(EveningDate.AddHours(6), chargeWindow.ValidTo);
        Assert.Equal(9000, chargeWindow.PlannedEnergyWh);
        Assert.Equal(100, chargeWindow.TargetSocPercent);
        //The bought energy is not discharged into the car while it charges at cheap prices.
        var holdWindow = windows[1];
        Assert.Equal(HomeBatteryMode.Hold, holdWindow.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, holdWindow.Reason);
        Assert.Equal(EveningDate.AddHours(6), holdWindow.ValidFrom);
        Assert.Equal(EveningDate.AddHours(9), holdWindow.ValidTo);
        Assert.Equal(15000, holdWindow.PlannedEnergyWh);
        //The expensive hours with car charging run in default mode, so the battery discharges into the car.
        Assert.DoesNotContain(windows, w => w.ValidTo > EveningDate.AddHours(9));
    }

    [Fact]
    public void CalculateWindows_GridChargeTimesUsageCosts_AccountForChargingLosses()
    {
        var input = CreateNightInput(currentSocPercent: 10, maxChargeSocPercent: 30);
        //1.25 Wh from the grid are needed per Wh the battery delivers.
        input.ChargingEnergyWh = 12500;
        AddHourlyPrices(input, new[] { 0.10m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(2500, window.PlannedEnergyWh);
        Assert.Equal(30, window.TargetSocPercent);
    }

    //Energy the house does not need is discharged into the car even at cheap prices, the rest is held.
    [Fact]
    public void CalculateWindows_StoredEnergyExceedingHouseNeed_IsUsedForCheapCarCharging()
    {
        var input = CreateNightInput(currentSocPercent: 100);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.10m, 0.10m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(4), EveningDate.AddHours(6)));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //9000 Wh stored - 5000 Wh for the expensive house hours = 4000 Wh for the car in the first cheap hour.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, window.Reason);
        Assert.Equal(EveningDate.AddHours(5), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(6), window.ValidTo);
    }

    [Fact]
    public void CalculateWindows_MinSoc_IsNeverPlannedToBeUndercut()
    {
        var input = CreateNightInput(currentSocPercent: 50);
        input.MinSocPercent = 40;
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.50m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //1000 Wh above the min SoC: 500 Wh for the most expensive hour, 500 Wh for the first hour.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate.AddHours(1), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(11), window.ValidTo);
        Assert.Equal(5000, window.PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_SocBelowMinSoc_CreatesNoHoldWindows()
    {
        var input = CreateNightInput(currentSocPercent: 5);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.10m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.50m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    //Below the usage costs, running the house on grid is cheaper than using the battery.
    [Fact]
    public void CalculateWindows_GridPriceBelowUsageCosts_HoldsStoredEnergy()
    {
        var input = CreateNightInput(currentSocPercent: 50);
        AddHourlyPrices(input, Enumerable.Repeat(0.04m, 12).ToArray());

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(SelfSufficiencyTime, window.ValidTo);
    }

    [Fact]
    public void CalculateWindows_DeliberateBatteryDischarge_ExcludesSliceFromPlanning()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        //Hour 0 is by far the cheapest but the battery is deliberately discharged into a car there.
        AddHourlyPrices(input, new[] { 0.10m, 0.12m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, 0.40m, });
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate, EveningDate.AddHours(1)));
        input.ChargingSchedules.Add(new DtoChargingSchedule
        {
            ValidFrom = EveningDate,
            ValidTo = EveningDate.AddHours(1),
            TargetHomeBatteryPower = 3000,
        });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate.AddHours(1), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(2), window.ValidTo);
        Assert.Equal(5000, window.PlannedEnergyWh);
    }

    //Deliberate discharging into a car drains the stored energy, so it is not planned for the house a second time.
    [Fact]
    public void CalculateWindows_DeliberateBatteryDischarge_DrainsStoredEnergy()
    {
        var input = CreateNightInput(currentSocPercent: 30);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.50m, });
        input.ChargingSchedules.Add(new DtoChargingSchedule
        {
            ValidFrom = EveningDate,
            ValidTo = EveningDate.AddHours(1),
            TargetHomeBatteryPower = 3000,
        });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Without the deliberate discharge the first hours would be held to keep energy for the expensive last hour.
        Assert.Empty(windows);
    }

    //Solar surplus charges the battery, the slice itself has no demand and is never held or charged.
    [Fact]
    public void CalculateWindows_PositiveSurplusSlice_ChargesBatteryAndIsNotHeld()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.05m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.50m, });
        input.SurplusPerSlice[EveningDate.AddHours(2)] = 1000;

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //1000 Wh solar: 500 Wh for the most expensive hour, 500 Wh for the first hour after the surplus.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate.AddHours(4), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(11), window.ValidTo);
        Assert.Equal(3500, window.PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_ClipsPlanningToKnownPrices()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        //Prices are only known for the first four hours even though self sufficiency is only reached at 08:00.
        AddHourlyPrices(input, new[] { 0.10m, 0.20m, 0.30m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
        Assert.Equal(1500, window.PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_NoPrices_CreatesNoWindows()
    {
        var input = CreateNightInput(currentSocPercent: 50);
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(6), EveningDate.AddHours(8)));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    //A price gap drops the affected slice entirely, while planning continues on the priced slices.
    [Fact]
    public void CalculateWindows_PriceGapMidPlanningHorizon_SkipsGapSliceWithoutBlockingOtherWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        for (var hour = 0; hour < 12; hour++)
        {
            if (hour == 1)
            {
                continue;
            }
            input.GridPrices.Add(new Price(EveningDate.AddHours(hour), EveningDate.AddHours(hour + 1), hour == 5 ? 0.10m : 0.30m, 0m, true));
        }

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate.AddHours(5), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(6), window.ValidTo);
        Assert.Equal(3000, window.PlannedEnergyWh);
    }

    //A price boundary within an hour splits the hour into sub hour slices whose energy is prorated by the duration.
    [Fact]
    public void CalculateWindows_SubHourPriceSlice_ProratesChargingCapacity()
    {
        var input = CreateNightInput(currentSocPercent: 10);
        input.GridPrices.Add(new Price(EveningDate, EveningDate.AddMinutes(30), 0.10m, 0m, true));
        input.GridPrices.Add(new Price(EveningDate.AddMinutes(30), SelfSufficiencyTime, 0.30m, 0m, true));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //5000 W for 30 minutes.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddMinutes(30), window.ValidTo);
        Assert.Equal(2500, window.PlannedEnergyWh);
        Assert.Equal(35, window.TargetSocPercent);
    }

    //An hour without surplus prediction data borrows the following hour's prediction; when that is also missing the
    //slice is treated as neutral (0 Wh).
    [Fact]
    public void CalculateWindows_MissingSurplusData_UsesFollowingHourOrTreatsSliceAsNeutral()
    {
        var input = CreateNightInput(currentSocPercent: 30);
        input.MaxChargingPowerW = null;
        input.SurplusPerSlice.Remove(EveningDate.AddHours(3));
        input.SurplusPerSlice.Remove(EveningDate.AddHours(4));
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.50m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.45m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Hour 3 is the most expensive but neutral. Hour 4 falls back to -500 Wh and is held with the following hours.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate.AddHours(4), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(11), window.ValidTo);
        Assert.Equal(3500, window.PlannedEnergyWh);
    }

    [Fact]
    public async Task PlanScheduleWindows_ToggleDisabled_ClearsWindowsWithoutFetchingData()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow> { new(), };

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        Assert.Empty(settingsMock.Object.HomeBatteryScheduleWindows);
        Mock.Mock<ITscOnlyChargingCostService>()
            .Verify(s => s.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task PlanScheduleWindows_UnknownSoc_ClearsWindowsWithoutFetchingPrices()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = null;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow> { new(), };

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        Assert.Empty(settingsMock.Object.HomeBatteryScheduleWindows);
        Mock.Mock<ITscOnlyChargingCostService>()
            .Verify(s => s.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task PlanScheduleWindows_StoredEnergyForExpensiveHour_StoresHoldWindowInSettings()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var settingsMock = SetupSettings(homeBatterySoc: 15);
        SetupTwoHourPrediction(EveningDate);

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        var window = Assert.Single(settingsMock.Object.HomeBatteryScheduleWindows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
    }

    //With dynamic min SoC the configured min SoC holds the SoC needed until sunrise, so its lower limit is the floor.
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task PlanScheduleWindows_DynamicMinSoc_UsesLowerLimitAsFloor(bool dynamicMinSoc, int expectedWindowCount)
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(80);
        configurationWrapperMock.Setup(c => c.DynamicHomeBatteryMinSoc()).Returns(dynamicMinSoc);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinDynamicMinSoc()).Returns(10);
        //Without grid charging the only possible window is a hold, which requires stored energy above the floor.
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns((int?)null);
        var settingsMock = SetupSettings(homeBatterySoc: 15);
        SetupTwoHourPrediction(EveningDate);

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        Assert.Equal(expectedWindowCount, settingsMock.Object.HomeBatteryScheduleWindows.Count);
    }

    [Fact]
    public async Task PlanScheduleWindows_PassesBatteryConfigurationIntoPlanning()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryMaxChargeSoc()).Returns(14);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(300);
        var settingsMock = SetupSettings(homeBatterySoc: 10);
        SetupTwoHourPrediction(EveningDate);

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        //The charging power limits the charge to 300 Wh, the max charge SoC would allow 400 Wh.
        var window = Assert.Single(settingsMock.Object.HomeBatteryScheduleWindows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(300, window.PlannedEnergyWh);
        Assert.Equal(13, window.TargetSocPercent);
    }

    //In production planning starts mid hour. The started hour has no own prediction slice, so its energy is
    //approximated with the following hour's prediction, prorated to the remaining duration.
    [Fact]
    public async Task PlanScheduleWindows_MidHourCurrentDate_CreatesPartialSliceFromCurrentDate()
    {
        var currentDate = EveningDate.AddMinutes(7);
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        //500 Wh above the min SoC: enough for the expensive hour only.
        var settingsMock = SetupSettings(homeBatterySoc: 15);
        var selfSufficiencyTime = EveningDate.AddHours(2);
        //As in production, the prediction only contains full hours starting after the current date.
        var surplus = new Dictionary<DateTimeOffset, int>
        {
            { EveningDate.AddHours(1), -500 },
        };
        SetupPrediction(currentDate, surplus, selfSufficiencyTime);

        await service.PlanScheduleWindows(currentDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        var window = Assert.Single(settingsMock.Object.HomeBatteryScheduleWindows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(currentDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
        //-500 Wh/h prorated to the remaining 53 minutes of the started hour: 500 * 53 / 60 = 441 Wh.
        Assert.Equal(441, window.PlannedEnergyWh);
    }

    private void SetupPlanningConfiguration()
    {
        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.GridPriceBasedHomeBatteryControl()).Returns(true);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryDischargingEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(5000);
        configurationWrapperMock.Setup(c => c.HomeBatteryUsageCostsPerKwh()).Returns(0.05m);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(10);
        configurationWrapperMock.Setup(c => c.HomeBatteryMaxChargeSoc()).Returns(100);
    }

    private Mock<ISettings> SetupSettings(int homeBatterySoc)
    {
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = homeBatterySoc;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow>();
        return settingsMock;
    }

    /// <summary>
    /// Two hours with 500 Wh house consumption each, cheap first hour and expensive second hour.
    /// </summary>
    private void SetupTwoHourPrediction(DateTimeOffset currentDate)
    {
        var surplus = new Dictionary<DateTimeOffset, int>
        {
            { EveningDate, -500 },
            { EveningDate.AddHours(1), -500 },
        };
        SetupPrediction(currentDate, surplus, EveningDate.AddHours(2));
    }

    private void SetupPrediction(DateTimeOffset currentDate, Dictionary<DateTimeOffset, int> surplus, DateTimeOffset selfSufficiencyTime)
    {
        Mock.Mock<IHomeBatteryEnergyCalculator>()
            .Setup(c => c.GetSurplusPrediction(currentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoHomeBatterySurplusPrediction(surplus, selfSufficiencyTime, true));
        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(s => s.GetPricesInTimeSpan(currentDate, selfSufficiencyTime))
            .ReturnsAsync(new List<Price>
            {
                new(EveningDate, EveningDate.AddHours(1), 0.10m, 0m, true),
                new(EveningDate.AddHours(1), EveningDate.AddHours(2), 0.40m, 0m, true),
            });
    }

    /// <summary>
    /// Creates an input covering 20:00 until 08:00 the next day with 500 Wh house consumption per hour, a 10 kWh battery
    /// with a min SoC of 10 % and no discharge power limit. By default grid charging may fill the whole battery.
    /// </summary>
    private static HomeBatteryPlanningInput CreateNightInput(int currentSocPercent, int maxChargeSocPercent = 100)
    {
        var input = new HomeBatteryPlanningInput
        {
            CurrentDate = EveningDate,
            CurrentSocPercent = currentSocPercent,
            ChargingEnergyWh = 10000,
            DischargingEnergyWh = 10000,
            MaxChargingPowerW = 5000,
            MinSocPercent = 10,
            MaxChargeSocPercent = maxChargeSocPercent,
            SelfSufficiencyTime = SelfSufficiencyTime,
            UsageCostsPerKwh = 0.05m,
        };
        for (var hour = 0; hour < 12; hour++)
        {
            input.SurplusPerSlice[EveningDate.AddHours(hour)] = -500;
        }
        return input;
    }

    private static void AddHourlyPrices(HomeBatteryPlanningInput input, decimal[] gridPricesPerHour)
    {
        for (var hour = 0; hour < gridPricesPerHour.Length; hour++)
        {
            input.GridPrices.Add(new Price(EveningDate.AddHours(hour), EveningDate.AddHours(hour + 1), gridPricesPerHour[hour], 0m, true));
        }
    }

    private static DtoChargingSchedule CreateGridChargeSchedule(DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        return new DtoChargingSchedule
        {
            ValidFrom = validFrom,
            ValidTo = validTo,
            TargetMinPower = 11000,
        };
    }
}
