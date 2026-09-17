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
using TeslaSolarCharger.Shared.Dtos.Settings;
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
    public void CalculateWindows_CarGridCharging_CreatesHoldWindowWithSocGuard()
    {
        var input = CreateNightInput(currentSocPercent: 40, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        AddHourlyPrices(input, Enumerable.Repeat(0.30m, 12).ToArray());
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(6), EveningDate.AddHours(8)));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, window.Reason);
        Assert.Equal(EveningDate.AddHours(6), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(8), window.ValidTo);
        Assert.Equal(40, window.OnlyWhileSocAtOrBelowPercent);
        Assert.Equal(1000, window.PlannedEnergyWh);
        Assert.Equal(0.30m, window.GridPricePerKwh);
    }

    [Fact]
    public void CalculateWindows_DeliberateBatteryDischarge_SuppressesCarHoldOverlap()
    {
        var input = CreateNightInput(currentSocPercent: 40, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        AddHourlyPrices(input, Enumerable.Repeat(0.30m, 12).ToArray());
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(6), EveningDate.AddHours(8)));
        input.ChargingSchedules.Add(new DtoChargingSchedule
        {
            ValidFrom = EveningDate.AddHours(6),
            ValidTo = EveningDate.AddHours(7),
            TargetHomeBatteryPower = 3000,
        });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, window.Reason);
        Assert.Equal(EveningDate.AddHours(7), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(8), window.ValidTo);
        Assert.Equal(500, window.PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_Deficit_HoldsCheapSlicesFirstAndChargesRemainderAtCheapestSlice()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        //01:00 and 02:00 are below the battery energy costs of 0.18 + 0.05 = 0.23, all other prices are above.
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.20m, 0.18m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Equal(2, windows.Count);
        var holdWindow = windows[0];
        Assert.Equal(HomeBatteryMode.Hold, holdWindow.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.PreserveForDeficit, holdWindow.Reason);
        Assert.Equal(EveningDate.AddHours(5), holdWindow.ValidFrom);
        Assert.Equal(EveningDate.AddHours(6), holdWindow.ValidTo);
        Assert.Equal(500, holdWindow.PlannedEnergyWh);
        Assert.Equal(0.20m, holdWindow.GridPricePerKwh);
        Assert.Equal(40, holdWindow.OnlyWhileSocAtOrBelowPercent);
        //The cheapest slice is upgraded from hold to charge: charging implies holding.
        var chargeWindow = windows[1];
        Assert.Equal(HomeBatteryMode.Charge, chargeWindow.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.GridChargeForDeficit, chargeWindow.Reason);
        Assert.Equal(EveningDate.AddHours(6), chargeWindow.ValidFrom);
        Assert.Equal(EveningDate.AddHours(7), chargeWindow.ValidTo);
        //Hold deficit 3000 Wh - 1000 Wh preserved by holds = 2000 Wh, charge target deficit 2000 Wh - 1000 Wh = 1000 Wh.
        Assert.Equal(1000, chargeWindow.PlannedEnergyWh);
        Assert.Equal(30, chargeWindow.TargetSocPercent);
        Assert.Equal(0.18m, chargeWindow.GridPricePerKwh);
    }

    [Fact]
    public void CalculateWindows_FlatPrices_CreatesNoEconomicWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        AddHourlyPrices(input, Enumerable.Repeat(0.30m, 12).ToArray());

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    [Fact]
    public void CalculateWindows_NoChargingPower_HoldsAreNotLimitedByBatteryEnergyCosts()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        input.MaxChargingPowerW = null;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.20m, 0.18m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //3000 Wh deficit -> six held slices: the two cheap ones and the four earliest 0.30 slices.
        Assert.Equal(2, windows.Count);
        Assert.All(windows, w => Assert.Equal(HomeBatteryMode.Hold, w.Mode));
        Assert.Equal(EveningDate, windows[0].ValidFrom);
        Assert.Equal(EveningDate.AddHours(4), windows[0].ValidTo);
        Assert.Equal(2000, windows[0].PlannedEnergyWh);
        Assert.Equal(EveningDate.AddHours(5), windows[1].ValidFrom);
        Assert.Equal(EveningDate.AddHours(7), windows[1].ValidTo);
        Assert.Equal(1000, windows[1].PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_PositiveSurplusSlices_AreNeverHeldOrCharged()
    {
        var input = CreateNightInput(currentSocPercent: 35, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        //22:00 is by far the cheapest slice but has positive surplus, so holding there would block solar charging.
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.05m, 0.30m, 0.30m, 0.30m, 0.10m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });
        input.SurplusPerSlice[EveningDate.AddHours(2)] = 1000;

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate.AddHours(6), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(7), window.ValidTo);
    }

    [Fact]
    public void CalculateWindows_ClipsPlanningToKnownPrices()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        //Prices are only known for the first four hours even though self sufficiency is only reached at 08:00.
        AddHourlyPrices(input, new[] { 0.10m, 0.20m, 0.30m, 0.40m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //The cheapest slice is held (500 Wh) and upgraded to charge covering the demand at 0.40, 0.30 and 0.20.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
        Assert.Equal(1500, window.PlannedEnergyWh);
    }

    [Fact]
    public void CalculateWindows_NoPrices_StillCreatesCarHoldWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(6), EveningDate.AddHours(8)));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, window.Reason);
        Assert.Equal(EveningDate.AddHours(6), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(8), window.ValidTo);
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
    public async Task PlanScheduleWindows_StaleSocTargets_ClearsWindowsWithoutFetchingPrices()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = 50;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow> { new(), };
        //Targets are refreshed every 8 minutes, values older than three intervals are considered stale.
        settingsMock.Object.HomeBatteryHoldTarget = CreateSocTarget(40, EveningDate.AddMinutes(-30));
        settingsMock.Object.HomeBatteryChargeTarget = CreateSocTarget(30, EveningDate.AddMinutes(-30));

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        Assert.Empty(settingsMock.Object.HomeBatteryScheduleWindows);
        Mock.Mock<ITscOnlyChargingCostService>()
            .Verify(s => s.GetPricesInTimeSpan(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task PlanScheduleWindows_WithDeficit_StoresHoldWindowInSettings()
    {
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = 30;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow>();
        settingsMock.Object.HomeBatteryHoldTarget = CreateSocTarget(40, EveningDate);
        settingsMock.Object.HomeBatteryChargeTarget = CreateSocTarget(25, EveningDate);

        var selfSufficiencyTime = EveningDate.AddHours(2);
        var surplus = new Dictionary<DateTimeOffset, int>
        {
            { EveningDate, -500 },
            { EveningDate.AddHours(1), -500 },
        };
        Mock.Mock<IHomeBatteryEnergyCalculator>()
            .Setup(c => c.GetSurplusPrediction(EveningDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoHomeBatterySurplusPrediction(surplus, selfSufficiencyTime, true));
        Mock.Mock<ITscOnlyChargingCostService>()
            .Setup(s => s.GetPricesInTimeSpan(EveningDate, selfSufficiencyTime))
            .ReturnsAsync(new List<Price>
            {
                new(EveningDate, EveningDate.AddHours(1), 0.10m, 0m, true),
                new(EveningDate.AddHours(1), EveningDate.AddHours(2), 0.40m, 0m, true),
            });

        await service.PlanScheduleWindows(EveningDate, new List<DtoChargingSchedule>(), CancellationToken.None);

        var window = Assert.Single(settingsMock.Object.HomeBatteryScheduleWindows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(1), window.ValidTo);
    }

    //Test plan case 5: a price gap in the middle of the planning horizon drops the affected slice entirely: it is
    //neither held nor charged nor counted as coverable demand, while planning continues on the priced slices.
    [Fact]
    public void CalculateWindows_PriceGapMidPlanningHorizon_SkipsGapSliceWithoutBlockingOtherWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        //Hour 1 has no known price, hour 5 is the only slice below the battery energy costs of 0.10 + 0.05.
        for (var hour = 0; hour < 12; hour++)
        {
            if (hour == 1)
            {
                continue;
            }
            input.GridPrices.Add(new Price(EveningDate.AddHours(hour), EveningDate.AddHours(hour + 1), hour == 5 ? 0.10m : 0.30m, 0m, true));
        }

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Hour 5 is held (500 Wh) and upgraded to charge for the remaining charge deficit of 2000 - 500 = 1500 Wh.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate.AddHours(5), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(6), window.ValidTo);
        Assert.Equal(1500, window.PlannedEnergyWh);
        //The gap slice is not covered by any window as no economic decision is possible without a price.
        Assert.DoesNotContain(windows, w => w.ValidFrom < EveningDate.AddHours(2) && w.ValidTo > EveningDate.AddHours(1));
    }

    //Test plan case 6: when the cheapest slice lies inside a car grid charging interval, the documented simplification
    //produces two overlapping windows: a car hold window and an economic charge window. The mode service resolves the
    //overlap at apply time (charge wins, see AutomaticMode_ChargeWinsOverOverlappingHold).
    [Fact]
    public void CalculateWindows_CheapestSliceDuringCarGridCharging_CreatesOverlappingHoldAndChargeWindows()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.10m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });
        input.ChargingSchedules.Add(CreateGridChargeSchedule(EveningDate.AddHours(6), EveningDate.AddHours(7)));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Equal(2, windows.Count);
        Assert.All(windows, w =>
        {
            Assert.Equal(EveningDate.AddHours(6), w.ValidFrom);
            Assert.Equal(EveningDate.AddHours(7), w.ValidTo);
        });
        var holdWindow = Assert.Single(windows, w => w.Mode == HomeBatteryMode.Hold);
        Assert.Equal(HomeBatteryScheduleWindowReason.CarGridCharging, holdWindow.Reason);
        Assert.Equal(500, holdWindow.PlannedEnergyWh);
        //Charge deficit 2000 Wh - 500 Wh preserved by the car hold = 1500 Wh, all charged in the cheap car hold hour.
        var chargeWindow = Assert.Single(windows, w => w.Mode == HomeBatteryMode.Charge);
        Assert.Equal(HomeBatteryScheduleWindowReason.GridChargeForDeficit, chargeWindow.Reason);
        Assert.Equal(1500, chargeWindow.PlannedEnergyWh);
    }

    //Test plan case 7: slices with deliberate battery discharge into cars are excluded from grid charge candidates,
    //not only from hold candidates. The charge is planned on the cheapest remaining slice instead.
    [Fact]
    public void CalculateWindows_DeliberateBatteryDischarge_ExcludesSliceFromGridChargeCandidates()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        //Hour 6 is by far the cheapest but the battery is deliberately discharged into a car there.
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.12m, 0.30m, 0.30m, 0.10m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });
        input.ChargingSchedules.Add(new DtoChargingSchedule
        {
            ValidFrom = EveningDate.AddHours(6),
            ValidTo = EveningDate.AddHours(7),
            TargetHomeBatteryPower = 3000,
        });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Hour 3 is held (500 Wh) and upgraded to charge for the remaining charge deficit of 2000 - 500 = 1500 Wh.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Charge, window.Mode);
        Assert.Equal(EveningDate.AddHours(3), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(4), window.ValidTo);
        Assert.Equal(1500, window.PlannedEnergyWh);
        //No window may cover the deliberate discharge hour.
        Assert.DoesNotContain(windows, w => w.ValidFrom < EveningDate.AddHours(7) && w.ValidTo > EveningDate.AddHours(6));
    }

    //Test plan case 8: a configured charging power of 0 W disables grid charge planning exactly like null does.
    [Fact]
    public void CalculateWindows_ZeroChargingPower_BehavesLikeNoChargingPower()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        input.MaxChargingPowerW = 0;
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.20m, 0.18m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Same expectation as CalculateWindows_NoChargingPower_HoldsAreNotLimitedByBatteryEnergyCosts.
        Assert.Equal(2, windows.Count);
        Assert.All(windows, w => Assert.Equal(HomeBatteryMode.Hold, w.Mode));
        Assert.Equal(EveningDate, windows[0].ValidFrom);
        Assert.Equal(EveningDate.AddHours(4), windows[0].ValidTo);
        Assert.Equal(2000, windows[0].PlannedEnergyWh);
        Assert.Equal(EveningDate.AddHours(5), windows[1].ValidFrom);
        Assert.Equal(EveningDate.AddHours(7), windows[1].ValidTo);
        Assert.Equal(1000, windows[1].PlannedEnergyWh);
    }

    //Test plan case 9: the hold and charge targets are independent floors. When holds can not fully close the (higher)
    //hold target deficit but already preserve enough for the (lower) charge target, no grid charge is planned.
    [Fact]
    public void CalculateWindows_HoldsAlreadyCoverChargeTarget_CreatesNoChargeWindow()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 40, chargeTargetSocPercent: 20);
        //Three slices below the battery energy costs of 0.10 + 0.05 preserve 1500 Wh: not enough for the hold
        //deficit of 3000 Wh, but enough for the charge deficit of 1000 Wh.
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.10m, 0.12m, 0.14m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //The three adjacent held slices are merged into a single hold window with a duration weighted price.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(HomeBatteryScheduleWindowReason.PreserveForDeficit, window.Reason);
        Assert.Equal(EveningDate.AddHours(4), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(7), window.ValidTo);
        Assert.Equal(1500, window.PlannedEnergyWh);
        Assert.Equal(0.12m, window.GridPricePerKwh);
    }

    //Test plan case 10: with the current SoC above both targets there is no deficit, so no windows are planned even
    //with a large price spread.
    [Fact]
    public void CalculateWindows_SocAboveTargets_CreatesNoWindows()
    {
        var input = CreateNightInput(currentSocPercent: 50, holdTargetSocPercent: 40, chargeTargetSocPercent: 30);
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.20m, 0.18m, 0.30m, 0.32m, 0.33m, 0.34m, 0.35m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        Assert.Empty(windows);
    }

    //Test plan case 11: a price boundary within an hour splits the hour into sub hour slices whose predicted energy is
    //prorated by the slice duration.
    [Fact]
    public void CalculateWindows_SubHourPriceSlice_ProratesPredictedEnergy()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 20, chargeTargetSocPercent: 5);
        //Only the first half hour is below the battery energy costs of 0.10 + 0.05.
        input.GridPrices.Add(new Price(EveningDate, EveningDate.AddMinutes(30), 0.10m, 0m, true));
        input.GridPrices.Add(new Price(EveningDate.AddMinutes(30), SelfSufficiencyTime, 0.30m, 0m, true));

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //500 Wh/h consumption prorated to 30 minutes => 250 Wh preserved.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate, window.ValidFrom);
        Assert.Equal(EveningDate.AddMinutes(30), window.ValidTo);
        Assert.Equal(250, window.PlannedEnergyWh);
    }

    //Test plan case 11: an hour without surplus prediction data borrows the following hour's prediction; when that is
    //also missing the slice is treated as neutral (0 Wh) and can not be held as there is nothing to preserve.
    [Fact]
    public void CalculateWindows_MissingSurplusData_UsesFollowingHourOrTreatsSliceAsNeutral()
    {
        var input = CreateNightInput(currentSocPercent: 10, holdTargetSocPercent: 20, chargeTargetSocPercent: 5);
        //Hour 3: both its own and the following hour's prediction are missing => neutral, never held.
        //Hour 4: its own prediction is missing but hour 5 exists => falls back to -500 Wh.
        input.SurplusPerSlice.Remove(EveningDate.AddHours(3));
        input.SurplusPerSlice.Remove(EveningDate.AddHours(4));
        AddHourlyPrices(input, new[] { 0.30m, 0.30m, 0.30m, 0.08m, 0.10m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, 0.30m, });

        var windows = HomeBatteryScheduleService.CalculateWindows(input);

        //Hour 3 is the cheapest slice but has no energy to preserve, so hour 4 is held instead.
        var window = Assert.Single(windows);
        Assert.Equal(HomeBatteryMode.Hold, window.Mode);
        Assert.Equal(EveningDate.AddHours(4), window.ValidFrom);
        Assert.Equal(EveningDate.AddHours(5), window.ValidTo);
        Assert.Equal(500, window.PlannedEnergyWh);
    }

    
    //Test plan case 12: in production planning starts mid hour (whenever ChargingServiceV2 runs). The started hour has
    //no own prediction slice, so its energy is approximated with the following hour's prediction, prorated to the
    //remaining duration.
    [Fact]
    public async Task PlanScheduleWindows_MidHourCurrentDate_CreatesPartialSliceFromCurrentDate()
    {
        var currentDate = EveningDate.AddMinutes(7);
        var service = Mock.Create<HomeBatteryScheduleService>();
        SetupPlanningConfiguration();
        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.SetupAllProperties();
        settingsMock.Object.HomeBatterySoc = 30;
        settingsMock.Object.HomeBatteryScheduleWindows = new ConcurrentBag<DtoHomeBatteryScheduleWindow>();
        settingsMock.Object.HomeBatteryHoldTarget = CreateSocTarget(40, currentDate);
        settingsMock.Object.HomeBatteryChargeTarget = CreateSocTarget(25, currentDate);

        var selfSufficiencyTime = EveningDate.AddHours(2);
        //As in production, the prediction only contains full hours starting after the current date.
        var surplus = new Dictionary<DateTimeOffset, int>
        {
            { EveningDate.AddHours(1), -500 },
        };
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
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(5000);
        configurationWrapperMock.Setup(c => c.HomeBatteryUsageCostsPerKwh()).Returns(0.05m);
    }

    private static DtoHomeBatterySocTarget CreateSocTarget(int requiredInitialSocPercent, DateTimeOffset calculatedAt)
    {
        return new DtoHomeBatterySocTarget
        {
            RequiredInitialSocPercent = requiredInitialSocPercent,
            SelfSufficiencyTime = SelfSufficiencyTime,
            CalculatedAt = calculatedAt,
        };
    }

    /// <summary>
    /// Creates an input covering 20:00 until 08:00 the next day with 500 Wh house consumption per hour.
    /// </summary>
    private static HomeBatteryPlanningInput CreateNightInput(int currentSocPercent, int holdTargetSocPercent, int chargeTargetSocPercent)
    {
        var input = new HomeBatteryPlanningInput
        {
            CurrentDate = EveningDate,
            CurrentSocPercent = currentSocPercent,
            UsableEnergyWh = 10000,
            MaxChargingPowerW = 5000,
            HoldTargetSocPercent = holdTargetSocPercent,
            ChargeTargetSocPercent = chargeTargetSocPercent,
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
