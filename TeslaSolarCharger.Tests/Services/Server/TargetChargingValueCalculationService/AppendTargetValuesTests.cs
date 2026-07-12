using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.ChargingServiceV2;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.TargetChargingValueCalculationService;

public class AppendTargetValuesTests : TestBase
{
    private const int CarId = 1;
    private const int Voltage = 233;
    private const int Phases = 3;
    private const int MaxCurrent = 16;
    private const int MinCurrent = 1;
    private const int MaxPower = Voltage * Phases * MaxCurrent; // 11_184W
    private const int ConfiguredHomeBatteryDischargePower = 12_000;

    // Values taken from the logfile:
    // GridPower = -868W (868W drawn from grid), HomeBatteryPower = -12030W (discharging),
    // car charging with 11184W => powerToControl = -868 + (-12030) + 11184 = -1714
    private const int PowerToControl = -1_714;
    private const int TargetHomeBatteryPower = MaxPower; // min(maxPower 11184, configured discharge power 12000)

    public AppendTargetValuesTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    /// <summary>
    /// Covers https://github.com/pkuehnel/TeslaSolarCharger logfile from 2026-05-12 04:53:27:
    /// A HomeBatteryDischarging schedule is active because the home battery has more energy than the car
    /// requires to reach the charging target SoC. The house consumes power in parallel, so the home battery
    /// (limited by its max discharge power) can not supply the car at full TargetHomeBatteryPower AND the house
    /// at the same time. powerToControl is negative (-1714W), meaning the difference is drawn from the grid.
    /// As the home battery can cover everything the car needs, no grid power must be used: the charging power
    /// assigned to the loadpoint must not exceed powerToControl + TargetHomeBatteryPower.
    /// </summary>
    [Fact]
    public async Task AppendTargetValues_HomeBatteryDischargingScheduleActive_DoesNotUseGridPower()
    {
        // Arrange
        await SetupLogfileScenario();
        var targetChargingValues = CreateTargetChargingValues();
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        await sut.AppendTargetValues(targetChargingValues, CreateActiveChargingSchedules(), CurrentFakeDate, PowerToControl, 0, CancellationToken.None);

        // Assert
        var estimatedChargingPower = GetEstimatedChargingPower(targetChargingValues[0]);
        var maxPowerWithoutGridConsumption = PowerToControl + TargetHomeBatteryPower; // 9_470W
        Assert.True(estimatedChargingPower <= maxPowerWithoutGridConsumption,
            $"Charging power {estimatedChargingPower}W exceeds the maximum power of {maxPowerWithoutGridConsumption}W that is possible without using grid power. " +
            $"The difference of {estimatedChargingPower - maxPowerWithoutGridConsumption}W is drawn from the grid even though the home battery has enough energy.");

        // The car should still charge as fast as possible from the home battery, so it must not be throttled
        // below the power the home battery can actually deliver to the car.
        Assert.True(estimatedChargingPower > maxPowerWithoutGridConsumption - (Voltage * Phases),
            $"Charging power {estimatedChargingPower}W is more than one ampere below the power of {maxPowerWithoutGridConsumption}W the home battery can deliver to the car.");
    }

    /// <summary>
    /// When the home battery is at or below its min SoC (with dynamic min SoC enabled HomeBatteryMinSoc is autoupdated
    /// to the current dynamic value), it can not deliver the discharge budget. Granting the budget anyway would result
    /// in the complete budget being drawn from the grid, as powerToControl only reflects what the battery actually
    /// delivers.
    /// </summary>
    [Fact]
    public async Task AppendTargetValues_DoesNotGrantDischargeBudget_WhenBatteryIsAtMinSoc()
    {
        // Arrange
        await SetupLogfileScenario();
        // Current min SoC is above the current battery SoC of 65, so the battery must not be discharged
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMinSoc()).Returns(70);
        var targetChargingValues = CreateTargetChargingValues();
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        await sut.AppendTargetValues(targetChargingValues, CreateActiveChargingSchedules(), CurrentFakeDate, PowerToControl, 0, CancellationToken.None);

        // Assert
        var estimatedChargingPower = GetEstimatedChargingPower(targetChargingValues[0]);
        var minChargingPower = MinCurrent * Voltage * Phases;
        Assert.True(estimatedChargingPower <= minChargingPower,
            $"Charging power {estimatedChargingPower}W exceeds the minimum charging power of {minChargingPower}W even though the home battery is below its min SoC and can not deliver the discharge budget.");
    }

    /// <summary>
    /// The additional home battery discharge power of the discharge during day feature and the discharge budget of an
    /// active charging schedule are delivered by the same home battery. When both are active at the same time only the
    /// larger one may be granted, as otherwise the loadpoint consumes more than the home battery can deliver, resulting
    /// in grid usage.
    /// </summary>
    [Fact]
    public async Task AppendTargetValues_DoesNotDoubleCountDayDischargePowerAndScheduleBudget()
    {
        // Arrange
        await SetupLogfileScenario();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.DischargeHomeBatteryToMinSocDuringDay()).Returns(true);
        Mock.Mock<ISettings>().Setup(s => s.NextSunEvent).Returns(NextSunEvent.Sunset);
        var targetChargingValues = CreateTargetChargingValues();
        var sut = Mock.Create<TeslaSolarCharger.Server.Services.TargetChargingValueCalculationService>();

        // Act
        await sut.AppendTargetValues(targetChargingValues, CreateActiveChargingSchedules(), CurrentFakeDate, PowerToControl, 0, CancellationToken.None);

        // Assert
        var estimatedChargingPower = GetEstimatedChargingPower(targetChargingValues[0]);
        var maxPowerWithoutGridConsumption = PowerToControl + Math.Max(ConfiguredHomeBatteryDischargePower, TargetHomeBatteryPower); // 10_286W
        Assert.True(estimatedChargingPower <= maxPowerWithoutGridConsumption,
            $"Charging power {estimatedChargingPower}W exceeds the maximum power of {maxPowerWithoutGridConsumption}W the home battery can deliver. " +
            "The day discharge power and the schedule discharge budget must not be added up as both are delivered by the same home battery.");
    }

    private decimal GetEstimatedChargingPower(DtoTargetChargingValues targetChargingValues)
    {
        var targetValues = targetChargingValues.TargetValues;
        Assert.NotNull(targetValues);
        Assert.False(targetValues.StopCharging);
        Assert.NotNull(targetValues.TargetCurrent);
        Assert.True(targetValues.TargetCurrent.Value >= MinCurrent);
        // Rounded to whole watts as converting power -> current -> power introduces decimal representation noise.
        return Math.Round(targetValues.TargetCurrent.Value * Voltage * Phases);
    }

    private async Task SetupLogfileScenario()
    {
        Context.Cars.Add(new Car
        {
            Id = CarId,
            Name = "Teslarossa",
            MinimumAmpere = MinCurrent,
            MaximumAmpere = MaxCurrent,
            ChargeMode = ChargeModeV2.Auto,
            MaximumSoc = 80,
            MinimumSoc = 40,
            CarType = CarType.Tesla,
            MaximumPhases = Phases,
        });
        await Context.SaveChangesAsync();

        var currentDate = CurrentFakeDate;
        var dtoCar = new DtoCar
        {
            Id = CarId,
            Name = "Teslarossa",
            ChargeModeV2 = ChargeModeV2.Auto,
            SoC = new(currentDate.AddMinutes(-2), 54),
            SocLimit = new(currentDate.AddMinutes(-2), 80),
            IsCharging = new(currentDate.AddMinutes(-1), true),
            ShouldStartCharging = new(currentDate.AddSeconds(-30), true),
            ShouldStopCharging = new(currentDate.AddSeconds(-30), false),
            ChargerPhases = new(currentDate.AddMinutes(-1), Phases),
            LastSetAmp = new(currentDate.AddMinutes(-1), MaxCurrent),
        };

        var settingsMock = Mock.Mock<ISettings>();
        settingsMock.Setup(s => s.Cars).Returns(new List<DtoCar> { dtoCar });
        settingsMock.Setup(s => s.HomeBatterySoc).Returns(65);
        settingsMock.Setup(s => s.NextSunEvent).Returns(NextSunEvent.Sunrise);

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.MaxCombinedCurrent()).Returns(32);
        configurationWrapperMock.Setup(c => c.DischargeHomeBatteryToMinSocDuringDay()).Returns(false);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(0);
        configurationWrapperMock.Setup(c => c.HomeBatteryDischargingPower()).Returns(ConfiguredHomeBatteryDischargePower);
        configurationWrapperMock.Setup(c => c.TimespanUntilSwitchOn()).Returns(TimeSpan.FromMinutes(5));
        configurationWrapperMock.Setup(c => c.TimespanUntilSwitchOff()).Returns(TimeSpan.FromMinutes(5));

        var shouldStartStopChargingCalculatorMock = Mock.Mock<IShouldStartStopChargingCalculator>();
        shouldStartStopChargingCalculatorMock.Setup(s => s.GetCarElements()).ReturnsAsync(new List<DtoStartStopChargingHelper>());
        shouldStartStopChargingCalculatorMock.Setup(s => s.GetOcppElements()).ReturnsAsync(new List<DtoStartStopChargingHelper>());
    }

    private List<DtoTargetChargingValues> CreateTargetChargingValues()
    {
        var loadPoint = new DtoLoadPointOverview
        {
            CarId = CarId,
            ChargingConnectorId = null,
            ActualCurrent = MaxCurrent,
            MaxCurrent = MaxCurrent,
            MinCurrent = MinCurrent,
            ActualPhases = Phases,
            EstimatedVoltageWhileCharging = Voltage,
            MaxPhases = Phases,
            ChargingPower = MaxPower,
            IsHome = true,
            IsPluggedIn = true,
            ChargingPriority = 1,
            ManageChargingPowerByCar = true,
            CarType = CarType.Tesla,
        };
        return new List<DtoTargetChargingValues> { new(loadPoint) };
    }

    private List<DtoChargingSchedule> CreateActiveChargingSchedules()
    {
        return new List<DtoChargingSchedule>
        {
            new(CarId, null, MaxPower, Voltage, Phases, new() { ScheduleReason.HomeBatteryDischarging, })
            {
                ValidFrom = CurrentFakeDate,
                ValidTo = CurrentFakeDate.AddMinutes(96),
                TargetMinPower = 0,
                TargetHomeBatteryPower = TargetHomeBatteryPower,
            },
        };
    }
}
