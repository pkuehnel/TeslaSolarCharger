using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class HomeBatteryEnergyCalculatorTests : TestBase
{
    public HomeBatteryEnergyCalculatorTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_NoDeficit_ReturnsMinSocAndNoBreach()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000; // 10kWh usable
        var minSoc = 5;

        // All positive surplus, no deficit
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 1000 },
            { new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), 1000 },
            { new DateTimeOffset(2023, 2, 2, 11, 0, 0, TimeSpan.Zero), 1000 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Act
        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        // Assert
        Assert.Equal(minSoc, result.RequiredInitialSocPercent);
        Assert.Null(result.FirstBreachTime);
        Assert.Equal(0, result.AdditionalEnergyRequiredWh);
        Assert.Equal(targetTime, result.SelfSufficiencyTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_SimpleDeficit_CalculatesBreachAndAdditionalEnergy()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5; // 500 Wh min
        var targetSoc = 5;

        // One big deficit slice
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -2000 }, // consume 2000 net
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Starting at 500, apply -2000 -> -1500, missing = 500 - (-1500) = 2000

        // Act
        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, targetSoc, targetTime, buffer);

        // Assert
        Assert.Equal(25, result.RequiredInitialSocPercent); // 500 + 2000 = 2500 / 10000 = 25%
        Assert.Equal(new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), result.FirstBreachTime);
        Assert.Equal(2000, result.AdditionalEnergyRequiredWh);
        Assert.Equal(targetTime, result.SelfSufficiencyTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_WithBuffer_IncludesBufferInAdditionalAndSoc()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -1000 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 50; // factor 1.5

        // missing = 1000 (raw), final = 1500
        // required = (500 + 1500)/10000 *100 = 20%

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        Assert.Equal(20, result.RequiredInitialSocPercent);
        Assert.Equal(1500, result.AdditionalEnergyRequiredWh);
        Assert.NotNull(result.FirstBreachTime);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_MultipleSlices_FirstBreachIsEarliest()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 500 },   // no breach
            { new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), -800 }, // breach here
            { new DateTimeOffset(2023, 2, 2, 11, 0, 0, TimeSpan.Zero), -300 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 12, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, minSoc, targetTime, buffer);

        Assert.Equal(new DateTimeOffset(2023, 2, 2, 10, 0, 0, TimeSpan.Zero), result.FirstBreachTime);
        // Starting 500
        // t9: 500+500=1000, missing=0
        // t10: 1000-800=200 , missing=500-200=300
        // t11: 200-300= -100 , missing=600
        // maxMissing=600
        Assert.Equal(600, result.AdditionalEnergyRequiredWh);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_TargetNotReached_IncreasesMissing()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var capacity = 10000;
        var minSoc = 5;
        var targetSoc = 50; // want to reach 50% by targetTime

        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 0 },
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Start at min 500, +0 -> 500 at targetTime
        // target 5000 > 500 => maxMissing at least 4500
        // required = (500 + 4500) /10000 *100 = 50%

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, targetSoc, targetTime, buffer);

        Assert.Equal(50, result.RequiredInitialSocPercent);
    }

    [Fact]
    public void CalculateRequiredInitialStateOfChargePercent_ChargingPowerLimited_CapsAddition()
    {
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(500); // low charge rate

        var capacity = 10000;
        var minSoc = 5;
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), 10000 }, // huge surplus, but capped at 500
        };
        var targetTime = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var buffer = 0;

        // Starts at 500, +500 (capped) -> 1000
        // No missing created.

        var result = calculator.CalculateRequiredInitialStateOfChargePercent(
            slices, capacity, minSoc, 5, targetTime, buffer);

        Assert.Equal(5, result.RequiredInitialSocPercent);
        Assert.Equal(0, result.AdditionalEnergyRequiredWh);
    }

    [Fact]
    public async Task RefreshHomeBatteryMinSoc_StoresHoldAndChargeTargetsInSettings_FetchesPredictionOnlyOnce()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ISettings>().SetupAllProperties();

        var nextSunset = new DateTimeOffset(2023, 2, 2, 17, 0, 0, TimeSpan.Zero);
        var nextSunrise = new DateTimeOffset(2023, 2, 3, 7, 0, 0, TimeSpan.Zero);
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunset(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(nextSunset);
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunrise(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(nextSunrise);

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinDynamicMinSoc()).Returns(5);
        configurationWrapperMock.Setup(c => c.HomeBatteryMaxDynamicMinSoc()).Returns(95);
        configurationWrapperMock.Setup(c => c.HoldHomeBatteryChargeSocBufferInPercent()).Returns(50);
        configurationWrapperMock.Setup(c => c.ChargeHomeBatterySocBufferInPercent()).Returns(100);
        // DynamicHomeBatteryMinSoc and ForceFullHomeBatteryBySunset stay at their mock default (false)

        // Deficit before sunrise, first positive surplus one hour after sunrise
        var breachSlice = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var expectedSelfSufficiencyTime = new DateTimeOffset(2023, 2, 3, 8, 0, 0, TimeSpan.Zero);
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { breachSlice, -1000 },
            { expectedSelfSufficiencyTime, 2000 },
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slices);

        // Act
        await calculator.RefreshHomeBatteryMinSoc(CancellationToken.None);

        // Assert
        Mock.Mock<IEnergyDataService>().Verify(
            e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var settings = Mock.Mock<ISettings>().Object;
        // Floor 500 Wh, -1000 -> -500 => 1000 Wh missing at the breach slice
        var holdTarget = settings.HomeBatteryHoldTarget;
        Assert.NotNull(holdTarget);
        Assert.Equal(20, holdTarget.RequiredInitialSocPercent); // (500 + 1000 * 1.5) / 10000
        Assert.Equal(1500, holdTarget.AdditionalEnergyRequiredWh);
        Assert.Equal(breachSlice, holdTarget.FirstBreachTime);
        Assert.Equal(expectedSelfSufficiencyTime, holdTarget.SelfSufficiencyTime);
        Assert.Equal(CurrentFakeDate, holdTarget.CalculatedAt);

        var chargeTarget = settings.HomeBatteryChargeTarget;
        Assert.NotNull(chargeTarget);
        Assert.Equal(25, chargeTarget.RequiredInitialSocPercent); // (500 + 1000 * 2) / 10000
        Assert.Equal(2000, chargeTarget.AdditionalEnergyRequiredWh);
        Assert.Equal(breachSlice, chargeTarget.FirstBreachTime);
        Assert.Equal(expectedSelfSufficiencyTime, chargeTarget.SelfSufficiencyTime);
        Assert.Equal(CurrentFakeDate, chargeTarget.CalculatedAt);
    }

    //Test plan case 1: with dynamic min SoC enabled the min SoC branch shares the single prediction fetch with the
    //hold and charge targets, updates the base configuration and fires exactly one state notification.
    [Fact]
    public async Task RefreshHomeBatteryMinSoc_DynamicMinSocEnabled_UpdatesMinSocAndStillFetchesPredictionOnlyOnce()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ISettings>().SetupAllProperties();
        SetupSunEvents();

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinDynamicMinSoc()).Returns(5);
        configurationWrapperMock.Setup(c => c.HomeBatteryMaxDynamicMinSoc()).Returns(95);
        configurationWrapperMock.Setup(c => c.DynamicHomeBatteryMinSoc()).Returns(true);
        configurationWrapperMock.Setup(c => c.HomeBatteryMinSoc()).Returns(10);
        var baseConfiguration = new DtoBaseConfiguration();
        configurationWrapperMock.Setup(c => c.GetBaseConfigurationAsync()).ReturnsAsync(baseConfiguration);
        //DynamicMinSocCalculationBufferInPercent and the hold/charge buffers stay at their mock default (0)

        var breachSlice = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { breachSlice, -1000 },
            { new DateTimeOffset(2023, 2, 3, 8, 0, 0, TimeSpan.Zero), 2000 },
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slices);

        // Act
        await calculator.RefreshHomeBatteryMinSoc(CancellationToken.None);

        // Assert
        Mock.Mock<IEnergyDataService>().Verify(
            e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // Floor 500 Wh, -1000 -> 1000 Wh missing, no buffer => (500 + 1000) / 10000 = 15%
        Assert.Equal(15, baseConfiguration.HomeBatteryMinSoc);
        configurationWrapperMock.Verify(c => c.UpdateBaseConfigurationAsync(baseConfiguration), Times.Once);
        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(It.IsAny<StateUpdateDto>()), Times.Once);
        var settings = Mock.Mock<ISettings>().Object;
        Assert.NotNull(settings.HomeBatteryHoldTarget);
        Assert.NotNull(settings.HomeBatteryChargeTarget);
    }

    //Test plan case 4: hold and charge targets are stored in settings only. Unlike the previous implementation that
    //persisted them to the base configuration, no configuration update and no state notification happens for them:
    //schedule windows are replanned every charging cycle and the support page pulls the state on demand.
    [Fact]
    public async Task RefreshHomeBatteryMinSoc_OnlyHoldAndChargeTargetsChange_DoesNotUpdateConfigurationOrNotify()
    {
        // Arrange: dynamic min SoC stays disabled, so only the hold and charge targets are calculated
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<ISettings>().SetupAllProperties();
        SetupSunEvents();

        var configurationWrapperMock = Mock.Mock<IConfigurationWrapper>();
        configurationWrapperMock.Setup(c => c.HomeBatteryUsableEnergy()).Returns(10000);
        configurationWrapperMock.Setup(c => c.HomeBatteryChargingPower()).Returns(10000);

        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -1000 },
            { new DateTimeOffset(2023, 2, 3, 8, 0, 0, TimeSpan.Zero), 2000 },
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slices);

        // Act
        await calculator.RefreshHomeBatteryMinSoc(CancellationToken.None);

        // Assert
        var settings = Mock.Mock<ISettings>().Object;
        Assert.NotNull(settings.HomeBatteryHoldTarget);
        Assert.NotNull(settings.HomeBatteryChargeTarget);
        configurationWrapperMock.Verify(c => c.UpdateBaseConfigurationAsync(It.IsAny<DtoBaseConfiguration>()), Times.Never);
        Mock.Mock<IAppStateNotifier>().Verify(n => n.NotifyStateUpdateAsync(It.IsAny<StateUpdateDto>()), Times.Never);
    }

    //Test plan case 2: when the required SoC exceeds the configured maximum dynamic SoC, only the SoC is clamped while
    //breach time and additional energy keep the full deficit visible for the schedule planning.
    [Fact]
    public void CalculateDynamicBatteryTargetSoc_RequiredSocAboveMaximum_ClampsSocButKeepsFullDeficit()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        Mock.Mock<IDateTimeProvider>().Setup(d => d.DateTimeOffSetUtcNow()).Returns(CurrentFakeDate);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryChargingPower()).Returns(10000);
        Mock.Mock<IConfigurationWrapper>().Setup(c => c.HomeBatteryMaxDynamicMinSoc()).Returns(30);

        var breachSlice = new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero);
        var slices = new Dictionary<DateTimeOffset, int> { { breachSlice, -5000 }, };

        // Act: unclamped the result would be (500 + 5000) / 10000 = 55%
        var result = calculator.CalculateDynamicBatteryTargetSoc(slices, breachSlice, true, 10000, 5, 5, 0);

        // Assert
        Assert.Equal(30, result.RequiredInitialSocPercent);
        Assert.Equal(5000, result.AdditionalEnergyRequiredWh);
        Assert.Equal(breachSlice, result.FirstBreachTime);
        Assert.Equal(breachSlice, result.SelfSufficiencyTime);
        Assert.Equal(CurrentFakeDate, result.CalculatedAt);
    }

    //Test plan case 3: GetSurplusPrediction returns null when sun events can not be calculated and does not fetch any
    //prediction data.
    [Fact]
    public async Task GetSurplusPrediction_SunEventsUnknown_ReturnsNullWithoutFetchingData()
    {
        // Arrange: ISunCalculator stays at its mock default and returns null for sunrise and sunset
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();

        // Act
        var result = await calculator.GetSurplusPrediction(CurrentFakeDate, CancellationToken.None);

        // Assert
        Assert.Null(result);
        Mock.Mock<IEnergyDataService>().Verify(
            e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    //Test plan case 3: GetSurplusPrediction exposes the surplus slices and the sunrise adjusted self sufficiency time
    //that the schedule service plans against.
    [Fact]
    public async Task GetSurplusPrediction_ReturnsSlicesAndSunriseAdjustedSelfSufficiencyTime()
    {
        // Arrange
        var calculator = Mock.Create<HomeBatteryEnergyCalculator>();
        SetupSunEvents();

        //Deficit before sunrise (07:00), first positive surplus one hour after sunrise
        var expectedSelfSufficiencyTime = new DateTimeOffset(2023, 2, 3, 8, 0, 0, TimeSpan.Zero);
        var slices = new Dictionary<DateTimeOffset, int>
        {
            { new DateTimeOffset(2023, 2, 2, 9, 0, 0, TimeSpan.Zero), -1000 },
            { expectedSelfSufficiencyTime, 2000 },
        };
        Mock.Mock<IEnergyDataService>()
            .Setup(e => e.GetPredictedSurplusPerSlice(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(slices);

        // Act
        var result = await calculator.GetSurplusPrediction(CurrentFakeDate, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(slices, result.SurplusPerSlice);
        Assert.Equal(expectedSelfSufficiencyTime, result.SelfSufficiencyTime);
        Assert.True(result.IsTargetDateSunrise);
    }

    private void SetupSunEvents()
    {
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunset(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(new DateTimeOffset(2023, 2, 2, 17, 0, 0, TimeSpan.Zero));
        Mock.Mock<ISunCalculator>()
            .Setup(s => s.NextSunrise(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>()))
            .Returns(new DateTimeOffset(2023, 2, 3, 7, 0, 0, TimeSpan.Zero));
    }
}
