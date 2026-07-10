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
